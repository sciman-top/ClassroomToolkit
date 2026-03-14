param(
    [string]$SourceRepoRoot = ".",
    [string]$ManifestPath = "scripts/refactor/refactor-adapter.manifest.json",
    [string]$OutputDir = ".codex/artifacts/refactor-adapter",
    [string]$Mode = "",
    [string]$TemplateProjectName = "TEMPLATE_PROJECT",
    [switch]$Zip,
    [switch]$Clean
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
$outputPath = Resolve-AbsolutePath -Path $OutputDir -BasePath $sourceRoot

if ($Clean -and (Test-Path -LiteralPath $outputPath)) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

if (-not (Test-Path -LiteralPath $outputPath)) {
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
}

$installerPath = Resolve-AbsolutePath -Path "scripts/refactor/install-refactor-adapter.ps1" -BasePath $sourceRoot
$installResultRaw = & powershell -ExecutionPolicy Bypass -File $installerPath `
    -SourceRepoRoot $sourceRoot `
    -TargetRepoRoot $outputPath `
    -ManifestPath $ManifestPath `
    -Mode $Mode `
    -ProjectName $TemplateProjectName `
    -Force `
    -AsJson

if ($LASTEXITCODE -ne 0) {
    throw "Adapter export failed while installing into output directory. Exit code: $LASTEXITCODE"
}

$installResult = [string]::Join([Environment]::NewLine, @($installResultRaw)) | ConvertFrom-Json

$zipPath = $null
if ($Zip) {
    $parentDir = Split-Path -Parent $outputPath
    $leaf = Split-Path -Leaf $outputPath
    $zipPath = Join-Path $parentDir "$leaf.zip"

    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $outputPath "*") -DestinationPath $zipPath -Force
}

$result = [ordered]@{
    status = "ok"
    source_repo_root = $sourceRoot
    output_dir = $outputPath
    mode = $Mode
    zip_path = $zipPath
    install_summary = $installResult
}

$result | ConvertTo-Json -Depth 100
exit 0
