param(
    [string]$SourceRepoRoot = ".",
    [string]$OutputRoot = ".codex/artifacts/portability-regression",
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
$outputDir = Resolve-AbsolutePath -Path $OutputRoot -BasePath $sourceRoot
$targetRepo = Join-Path $outputDir "target-repo"

if ($Clean -and (Test-Path -LiteralPath $outputDir)) {
    Remove-Item -LiteralPath $outputDir -Recurse -Force
}

if (-not (Test-Path -LiteralPath $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$transferScript = Join-Path $sourceRoot "scripts/refactor/transfer-refactor-adapter.ps1"
if (-not (Test-Path -LiteralPath $transferScript)) {
    throw "Missing transfer script: $transferScript"
}

$transferRaw = & powershell -ExecutionPolicy Bypass -File $transferScript `
    -SourceRepoRoot $sourceRoot `
    -TargetRepoRoot $targetRepo `
    -Force `
    -AsJson
if ($LASTEXITCODE -ne 0) {
    throw "Transfer script failed with exit code $LASTEXITCODE"
}

$transferJson = [string]::Join([Environment]::NewLine, @($transferRaw))
$transfer = $transferJson | ConvertFrom-Json
if ([string]$transfer.status -ne "ok") {
    throw "Transfer status is not ok: $([string]$transfer.status)"
}

Push-Location $targetRepo
try {
    git init | Out-Null
    git -c user.name="codex" -c user.email="codex@example.com" add -A | Out-Null
    git -c user.name="codex" -c user.email="codex@example.com" commit -m "bootstrap portability regression" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create bootstrap commit in target repo."
    }
}
finally {
    Pop-Location
}

$smokeScript = Join-Path $targetRepo "scripts/unattended/test-checklist-loop-smoke.ps1"
if (-not (Test-Path -LiteralPath $smokeScript)) {
    throw "Missing smoke script in target repo: $smokeScript"
}

$smokeOutput = & powershell -ExecutionPolicy Bypass -File $smokeScript -RepoRoot $targetRepo
if ($LASTEXITCODE -ne 0) {
    throw "Smoke script failed in target repo with exit code $LASTEXITCODE"
}

$smokeText = [string]::Join([Environment]::NewLine, @($smokeOutput))
if ($smokeText -notlike "*CHECKLIST_SMOKE: PASS*") {
    throw "Smoke output missing PASS marker."
}

$result = [ordered]@{
    status = "ok"
    source_repo_root = $sourceRoot
    output_root = $outputDir
    target_repo_root = $targetRepo
    transfer_copied_count = [int]$transfer.install_summary.copied_count
    smoke_status = "passed"
}

$result | ConvertTo-Json -Depth 20
