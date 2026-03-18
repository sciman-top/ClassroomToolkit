param(
    [switch]$Apply,
    [switch]$IncludeRuntimeCache,
    [switch]$IncludeAllReleaseArtifacts
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Add-IfExists {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.Generic.List[string]]$List,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (Test-Path -LiteralPath $Path) {
        $List.Add($Path)
    }
}

function Remove-Targets {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Targets
    )

    foreach ($target in $Targets) {
        if (-not (Test-Path -LiteralPath $target)) {
            continue
        }

        Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction Stop
        Write-Host "Removed: $target"
    }
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot "..\..")).Path
$releaseRoot = Join-Path $repoRoot "artifacts\release"
$preReqRoot = Join-Path $scriptRoot "prereq"

$targets = New-Object System.Collections.Generic.List[string]

if ($IncludeAllReleaseArtifacts) {
    Add-IfExists -List $targets -Path $releaseRoot
}
else {
    if (Test-Path -LiteralPath $releaseRoot) {
        $preflightDirs = Get-ChildItem -LiteralPath $releaseRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like "preflight-*" }
        foreach ($dir in $preflightDirs) {
            $targets.Add($dir.FullName)
        }
    }
}

if ($IncludeRuntimeCache -and (Test-Path -LiteralPath $preReqRoot)) {
    $runtimeInstallers = Get-ChildItem -LiteralPath $preReqRoot -File -Filter "*desktop-runtime*win-x64*.exe" -ErrorAction SilentlyContinue
    foreach ($installer in $runtimeInstallers) {
        $targets.Add($installer.FullName)
    }
}

$distinctTargets = $targets |
    Select-Object -Unique |
    Sort-Object

if ($distinctTargets.Count -eq 0) {
    Write-Host "No cleanup targets found."
    exit 0
}

Write-Host "Cleanup targets:"
foreach ($target in $distinctTargets) {
    Write-Host " - $target"
}

if (-not $Apply) {
    Write-Host ""
    Write-Host "Preview mode only. Re-run with -Apply to delete."
    exit 0
}

Remove-Targets -Targets $distinctTargets
Write-Host "Cleanup completed."
