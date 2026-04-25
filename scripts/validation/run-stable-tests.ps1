[CmdletBinding()]
param(
    [ValidateSet("quick", "standard", "full")]
    [string]$Profile = "standard",
    [string]$Configuration = "Debug",
    [string]$TestProject = "tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj",
    [switch]$SkipBuild,
    [switch]$DryRun,
    [string]$SummaryPath = "artifacts/TestResults/stable-tests-summary.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$environmentBootstrap = Join-Path $PSScriptRoot "..\env\Initialize-WindowsProcessEnvironment.ps1"
if (Test-Path -LiteralPath $environmentBootstrap) {
    . $environmentBootstrap
}

function Resolve-StableFilter {
    param([Parameter(Mandatory = $true)][string]$StableProfile)

    switch ($StableProfile) {
        "quick" {
            return "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests|FullyQualifiedName~FileLoggerProviderTests|FullyQualifiedName~StudentPhotoResolverTests|FullyQualifiedName~SafeTaskRunnerTests|FullyQualifiedName~WindowInteropRetryExecutorTests|FullyQualifiedName~InteropBackgroundDispatchExecutorTests|FullyQualifiedName~RollCallViewModelPreloadConcurrencyTests|FullyQualifiedName~ImageManagerThumbnailCacheWarmupContractTests|FullyQualifiedName~DiagnosticsBundleExportServiceTests"
        }
        "standard" { return "" }
        "full" { return "" }
        default { throw "Unsupported stable test profile: $StableProfile" }
    }
}

function Resolve-AbsolutePath {
    param([Parameter(Mandatory = $true)][string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }
    return Join-Path (Get-Location) $Path
}

function Write-SummaryFileBestEffort {
    param(
        [Parameter(Mandatory = $true)][string]$PreferredPath,
        [Parameter(Mandatory = $true)][string]$JsonContent
    )

    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            [System.IO.File]::WriteAllText($PreferredPath, $JsonContent, [System.Text.Encoding]::UTF8)
            return $PreferredPath
        }
        catch [System.IO.IOException] {
            Start-Sleep -Milliseconds (40 * $attempt)
        }
    }

    $fallbackPath = Join-Path (Split-Path -Parent $PreferredPath) (
        "{0}-{1:yyyyMMddHHmmssfff}-{2}.json" -f
        [System.IO.Path]::GetFileNameWithoutExtension($PreferredPath),
        [DateTime]::UtcNow,
        [Guid]::NewGuid().ToString("N"))

    [System.IO.File]::WriteAllText($fallbackPath, $JsonContent, [System.Text.Encoding]::UTF8)
    Write-Host "[stable-tests] Preferred summary path busy. Fallback summary used: $fallbackPath" -ForegroundColor Yellow
    return $fallbackPath
}

$resolvedProjectPath = Resolve-AbsolutePath -Path $TestProject
if (-not (Test-Path -LiteralPath $resolvedProjectPath)) {
    throw "Stable tests project not found: $resolvedProjectPath"
}

$resolvedSummaryPath = Resolve-AbsolutePath -Path $SummaryPath
$summaryDir = Split-Path -Parent $resolvedSummaryPath
if (-not [string]::IsNullOrWhiteSpace($summaryDir)) {
    New-Item -Path $summaryDir -ItemType Directory -Force | Out-Null
}

$filter = Resolve-StableFilter -StableProfile $Profile
$command = @(
    "test",
    $resolvedProjectPath,
    "-c",
    $Configuration,
    "-m:1"
)
if ($SkipBuild.IsPresent) {
    $command += "--no-build"
}
if (-not [string]::IsNullOrWhiteSpace($filter)) {
    $command += "--filter"
    $command += $filter
}

Write-Host "[stable-tests] Using profile: $Profile"
Write-Host "[stable-tests] Command: dotnet $($command -join ' ')"

$started = [DateTimeOffset]::UtcNow
$exitCode = 0
if (-not $DryRun.IsPresent) {
    & dotnet @command
    $exitCode = $LASTEXITCODE
}
$finished = [DateTimeOffset]::UtcNow

$summary = [ordered]@{
    generated_at_utc = $finished.ToString("o")
    profile = $Profile
    configuration = $Configuration
    test_project = $resolvedProjectPath
    skip_build = $SkipBuild.IsPresent
    dry_run = $DryRun.IsPresent
    filter = $filter
    command = "dotnet $($command -join ' ')"
    started_utc = $started.ToString("o")
    finished_utc = $finished.ToString("o")
    duration_ms = [Math]::Round(($finished - $started).TotalMilliseconds)
    exit_code = $exitCode
}

$summaryJson = $summary | ConvertTo-Json -Depth 5
$writtenSummaryPath = Write-SummaryFileBestEffort -PreferredPath $resolvedSummaryPath -JsonContent $summaryJson
Write-Host "[stable-tests] Summary: $writtenSummaryPath"

if ($exitCode -ne 0) {
    throw "[stable-tests] Failed with exit code: $exitCode"
}

Write-Host "[stable-tests] PASS"
