param(
    [Parameter(Mandatory = $true)]
    [string]$TargetRepoRoot,
    [string]$SourceRepoRoot = ".",
    [string]$ManifestPath = "scripts/refactor/refactor-adapter.manifest.json",
    [string]$Mode = "",
    [switch]$SkipTaskGraph,
    [switch]$CopyStateFromSource,
    [string]$ProjectName = "",
    [switch]$Force,
    [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param(
        [string]$Path,
        [string]$BasePath = ""
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        if (Test-Path -LiteralPath $Path) {
            return (Resolve-Path -LiteralPath $Path).Path
        }
        return [System.IO.Path]::GetFullPath($Path)
    }

    if ([string]::IsNullOrWhiteSpace($BasePath)) {
        if (Test-Path -LiteralPath $Path) {
            return (Resolve-Path -LiteralPath $Path).Path
        }
        return [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $Path))
    }

    $combined = Join-Path $BasePath $Path
    if (Test-Path -LiteralPath $combined) {
        return (Resolve-Path -LiteralPath $combined).Path
    }
    return [System.IO.Path]::GetFullPath($combined)
}

$sourceRoot = Resolve-AbsolutePath -Path $SourceRepoRoot
$targetRoot = Resolve-AbsolutePath -Path $TargetRepoRoot -BasePath $sourceRoot
$installerPath = Resolve-AbsolutePath -Path "scripts/refactor/install-refactor-adapter.ps1" -BasePath $sourceRoot

if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "Installer script not found: $installerPath"
}

$installArgs = @(
    "-SourceRepoRoot", $sourceRoot,
    "-TargetRepoRoot", $targetRoot,
    "-ManifestPath", $ManifestPath
)
if (-not [string]::IsNullOrWhiteSpace($Mode)) { $installArgs += @("-Mode", $Mode) }
if ($SkipTaskGraph.IsPresent) { $installArgs += "-SkipTaskGraph" }
if ($CopyStateFromSource.IsPresent) { $installArgs += "-CopyStateFromSource" }
if (-not [string]::IsNullOrWhiteSpace($ProjectName)) { $installArgs += @("-ProjectName", $ProjectName) }
if ($Force.IsPresent) { $installArgs += "-Force" }
$installArgs += "-AsJson"

$installResultRaw = & powershell -ExecutionPolicy Bypass -File $installerPath @installArgs
$exitCode = $LASTEXITCODE

if ($exitCode -ne 0 -and @($installResultRaw).Count -eq 0) {
    throw "Adapter transfer failed without JSON output. Exit code: $exitCode"
}

$installResult = [string]::Join([Environment]::NewLine, @($installResultRaw)) | ConvertFrom-Json

$result = [ordered]@{
    status = [string]$installResult.status
    source_repo_root = $sourceRoot
    target_repo_root = $targetRoot
    manifest_path = $ManifestPath
    mode = $Mode
    transfer_kind = "direct_install"
    install_summary = $installResult
}

if ($AsJson) {
    $result | ConvertTo-Json -Depth 100
}
else {
    $result
}

if ([string]$installResult.status -eq "ok") {
    exit 0
}

if ([string]$installResult.status -eq "needs_force_or_manual") {
    exit 2
}

exit 1
