[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TargetRoot,
    [string]$PackageRoot = $PSScriptRoot,
    [switch]$BackupExisting
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param([Parameter(Mandatory = $true)][string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

function Assert-PackageIntegrity {
    param(
        [Parameter(Mandatory = $true)]$Manifest,
        [Parameter(Mandatory = $true)][string]$PayloadRoot
    )

    foreach ($file in $Manifest.files) {
        $relativePath = [string]$file.relative_path
        if ([System.IO.Path]::IsPathRooted($relativePath) -or $relativePath.Contains("..")) {
            throw "Invalid manifest path: $relativePath"
        }

        $path = Join-Path $PayloadRoot ($relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Package payload is missing: $relativePath"
        }

        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($hash -ne [string]$file.sha256) {
            throw "Package payload hash mismatch: $relativePath"
        }
    }
}

$resolvedPackageRoot = Resolve-AbsolutePath -Path $PackageRoot
$manifestPath = Join-Path $resolvedPackageRoot "migration-manifest.json"
$payloadRoot = Join-Path $resolvedPackageRoot "payload"
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or -not (Test-Path -LiteralPath $payloadRoot -PathType Container)) {
    throw "Migration package is incomplete: $resolvedPackageRoot"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
Assert-PackageIntegrity -Manifest $manifest -PayloadRoot $payloadRoot

$resolvedTargetRoot = Resolve-AbsolutePath -Path $TargetRoot
if ($resolvedTargetRoot -eq [System.IO.Path]::GetPathRoot($resolvedTargetRoot)) {
    throw "TargetRoot must not be a filesystem root."
}

$targetHasContent = (Test-Path -LiteralPath $resolvedTargetRoot) -and $null -ne (Get-ChildItem -LiteralPath $resolvedTargetRoot -Force | Select-Object -First 1)
$backupRoot = $null
if ($targetHasContent) {
    if (-not $BackupExisting) {
        throw "Target root is not empty. Re-run with -BackupExisting to move its current contents to a recoverable sibling backup."
    }

    $backupRoot = "{0}.backup-{1}" -f $resolvedTargetRoot, (Get-Date -Format "yyyyMMdd-HHmmss")
    Move-Item -LiteralPath $resolvedTargetRoot -Destination $backupRoot
}

New-Item -ItemType Directory -Path $resolvedTargetRoot -Force | Out-Null
Get-ChildItem -LiteralPath $payloadRoot -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $resolvedTargetRoot -Recurse -Force
}
Assert-PackageIntegrity -Manifest $manifest -PayloadRoot $resolvedTargetRoot

$receipt = [ordered]@{
    migration_id = [string]$manifest.migration_id
    version = [string]$manifest.version
    restored_at_utc = [DateTimeOffset]::UtcNow.ToString("o")
    target_root = $resolvedTargetRoot
    backup_root = $backupRoot
}
$receipt | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $resolvedTargetRoot "migration-restore-receipt.json") -Encoding UTF8
Write-Host "[private-migration] RESTORED target=$resolvedTargetRoot backup=$backupRoot"
