param(
    [Parameter(Mandatory = $true)]
    [string]$TargetRepoRoot,
    [string]$SourceRepoRoot = ".",
    [ValidateSet("checklist", "refactor")]
    [string]$Mode = "checklist",
    [string]$TaskFile = "docs/unattended/tasks.json",
    [ValidateSet("aggressive", "balanced", "conservative")]
    [string]$GuardProfilePreset = "balanced",
    [string]$StartFromTaskId = "",
    [string]$SyncScript = "",
    [string]$RuntimeSkillPath = "",
    [string]$OverrideSkillPath = "E:\CODE\skills-manager\overrides\autonomous-execution-loop\SKILL.md",
    [string]$SolutionPath = "",
    [string]$TestProjectPath = "",
    [switch]$SkipTaskFileBootstrap,
    [switch]$SkipRun,
    [switch]$SkipManualValidation,
    [switch]$ForceReleaseWithoutManual,
    [switch]$SkipReleaseValidation,
    [switch]$SkipManualGates,
    [switch]$PreferOverrideSkill,
    [switch]$SkipGuardSync,
    [switch]$AllowDirtyWorkingTree,
    [switch]$EnableCompatibilityArtifacts,
    [switch]$DryRun
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

function Resolve-RelativePathFromRoot {
    param(
        [string]$RootPath,
        [string]$AbsolutePath
    )

    $rootWithSep = [System.IO.Path]::GetFullPath($RootPath)
    if (-not $rootWithSep.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $rootWithSep += [System.IO.Path]::DirectorySeparatorChar
    }

    $targetFull = [System.IO.Path]::GetFullPath($AbsolutePath)
    if ($targetFull.StartsWith($rootWithSep, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $targetFull.Substring($rootWithSep.Length).Replace('\', '/')
    }

    return $AbsolutePath
}

function Resolve-DefaultSolutionPath {
    param([string]$RepoRootPath)

    $candidate = Get-ChildItem -LiteralPath $RepoRootPath -Filter *.sln -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $candidate) {
        $candidate = Get-ChildItem -LiteralPath $RepoRootPath -Recurse -Filter *.sln -File -ErrorAction SilentlyContinue | Select-Object -First 1
    }

    if ($null -eq $candidate) {
        return ""
    }

    return Resolve-RelativePathFromRoot -RootPath $RepoRootPath -AbsolutePath $candidate.FullName
}

function Resolve-DefaultTestProjectPath {
    param([string]$RepoRootPath)

    $testCandidates = Get-ChildItem -LiteralPath $RepoRootPath -Recurse -Filter *.csproj -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match 'test' -or $_.DirectoryName -match 'test' }
    $candidate = $testCandidates | Select-Object -First 1
    if ($null -eq $candidate) {
        return ""
    }

    return Resolve-RelativePathFromRoot -RootPath $RepoRootPath -AbsolutePath $candidate.FullName
}

function New-MinimalChecklistTasks {
    param(
        [string]$TargetRootPath,
        [string]$TaskFilePath,
        [string]$ResolvedSolutionPath,
        [string]$ResolvedTestProjectPath
    )

    $taskDir = Split-Path -Parent $TaskFilePath
    if (-not (Test-Path -LiteralPath $taskDir)) {
        New-Item -ItemType Directory -Path $taskDir -Force | Out-Null
    }

    $tasks = [System.Collections.ArrayList]::new()
    if (-not [string]::IsNullOrWhiteSpace($ResolvedSolutionPath)) {
        [void]$tasks.Add([ordered]@{
                id = "build-debug"
                title = "Build Debug"
                commit_message = "chore: unattended build debug"
                prompt = "Run minimal safe changes required for this task only, then stop."
                gates = @(
                    [ordered]@{
                        command = "dotnet"
                        args = @("build", $ResolvedSolutionPath, "-c", "Debug")
                        timeout_seconds = 900
                        idle_timeout_seconds = 120
                    }
                )
            })
    }

    if (-not [string]::IsNullOrWhiteSpace($ResolvedTestProjectPath)) {
        [void]$tasks.Add([ordered]@{
                id = "test-debug"
                title = "Test Debug"
                commit_message = "chore: unattended test debug"
                prompt = "Run minimal safe changes required for this task only, then stop."
                gates = @(
                    [ordered]@{
                        command = "dotnet"
                        args = @("test", $ResolvedTestProjectPath, "-c", "Debug")
                        timeout_seconds = 1200
                        idle_timeout_seconds = 180
                    }
                )
            })
    }

    if ($tasks.Count -eq 0) {
        [void]$tasks.Add([ordered]@{
                id = "verify-environment"
                title = "Verify Environment"
                commit_message = "chore: unattended verify environment"
                prompt = "Only verify the environment and stop. Do not make unrelated changes."
                gates = @(
                    [ordered]@{
                        command = "powershell"
                        args = @("-NoLogo", "-NoProfile", "-Command", "Write-Host 'Please edit docs/unattended/tasks.json with project-specific build/test gates.'")
                        timeout_seconds = 120
                        idle_timeout_seconds = 30
                    }
                )
            })
    }

    $doc = [ordered]@{
        name = "unattended-checklist"
        version = "1.0.0"
        tasks = @($tasks)
    }

    $doc | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $TaskFilePath -Encoding UTF8
}

$sourceRoot = Resolve-AbsolutePath -Path $SourceRepoRoot
$targetRoot = Resolve-AbsolutePath -Path $TargetRepoRoot -BasePath $sourceRoot

$transferScript = Join-Path $sourceRoot "scripts/refactor/transfer-refactor-adapter.ps1"
if (-not (Test-Path -LiteralPath $transferScript)) {
    throw "Transfer script not found in source repo: $transferScript"
}

Write-Host "MIGRATE: transferring adapter to target repo"
$transferOutput = & powershell -ExecutionPolicy Bypass -File $transferScript -SourceRepoRoot $sourceRoot -TargetRepoRoot $targetRoot -Force -AsJson
if ($LASTEXITCODE -ne 0) {
    throw "Transfer failed with exit code $LASTEXITCODE"
}
$transferJson = [string]::Join([Environment]::NewLine, @($transferOutput)) | ConvertFrom-Json
if ([string]$transferJson.status -ne "ok") {
    throw "Transfer status is not ok: $([string]$transferJson.status)"
}

$taskFilePath = Resolve-AbsolutePath -Path $TaskFile -BasePath $targetRoot
if (-not $SkipTaskFileBootstrap.IsPresent -and -not (Test-Path -LiteralPath $taskFilePath)) {
    $effectiveSolutionPath = if (-not [string]::IsNullOrWhiteSpace($SolutionPath)) { $SolutionPath } else { Resolve-DefaultSolutionPath -RepoRootPath $targetRoot }
    $effectiveTestProjectPath = if (-not [string]::IsNullOrWhiteSpace($TestProjectPath)) { $TestProjectPath } else { Resolve-DefaultTestProjectPath -RepoRootPath $targetRoot }
    Write-Host "MIGRATE: creating minimal task file at $taskFilePath"
    New-MinimalChecklistTasks `
        -TargetRootPath $targetRoot `
        -TaskFilePath $taskFilePath `
        -ResolvedSolutionPath $effectiveSolutionPath `
        -ResolvedTestProjectPath $effectiveTestProjectPath
}

if ($SkipRun.IsPresent) {
    Write-Host "RUN: skipped by -SkipRun"
    exit 0
}

$bootstrapScript = Join-Path $targetRoot "scripts/unattended/bootstrap-unattended.ps1"
if (-not (Test-Path -LiteralPath $bootstrapScript)) {
    throw "Bootstrap script not found in target repo: $bootstrapScript"
}

$forward = @(
    "-ExecutionPolicy", "Bypass",
    "-File", $bootstrapScript,
    "-Mode", $Mode,
    "-RepoRoot", $targetRoot,
    "-TaskFile", $TaskFile,
    "-GuardProfilePreset", $GuardProfilePreset
)
if (-not [string]::IsNullOrWhiteSpace($StartFromTaskId)) { $forward += @("-StartFromTaskId", $StartFromTaskId) }
if (-not [string]::IsNullOrWhiteSpace($SyncScript)) { $forward += @("-SyncScript", $SyncScript) }
if (-not [string]::IsNullOrWhiteSpace($RuntimeSkillPath)) { $forward += @("-RuntimeSkillPath", $RuntimeSkillPath) }
if (-not [string]::IsNullOrWhiteSpace($OverrideSkillPath)) { $forward += @("-OverrideSkillPath", $OverrideSkillPath) }

if ($SkipManualValidation.IsPresent) { $forward += "-SkipManualValidation" }
if ($ForceReleaseWithoutManual.IsPresent) { $forward += "-ForceReleaseWithoutManual" }
if ($SkipReleaseValidation.IsPresent) { $forward += "-SkipReleaseValidation" }
if ($SkipManualGates.IsPresent) { $forward += "-SkipManualGates" }
if ($PreferOverrideSkill.IsPresent) { $forward += "-PreferOverrideSkill" }
if ($SkipGuardSync.IsPresent) { $forward += "-SkipGuardSync" }
if ($AllowDirtyWorkingTree.IsPresent) { $forward += "-AllowDirtyWorkingTree" }
if ($EnableCompatibilityArtifacts.IsPresent) { $forward += "-EnableCompatibilityArtifacts" }
if ($DryRun.IsPresent) { $forward += "-DryRun" }

# Keep unattended checklist defaults to avoid manual-gate blocking unless user explicitly overrides.
if ($Mode -eq "checklist") {
    if (-not $SkipManualValidation.IsPresent) { $forward += "-SkipManualValidation" }
    if (-not $ForceReleaseWithoutManual.IsPresent) { $forward += "-ForceReleaseWithoutManual" }
    if (-not $SkipReleaseValidation.IsPresent) { $forward += "-SkipReleaseValidation" }
}

Write-Host "RUN: invoking bootstrap in target repo"
& powershell @forward
exit $LASTEXITCODE
