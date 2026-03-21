param(
    [string]$RepoRoot = "."
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-RepoPath {
    param([string]$Root)
    return (Resolve-Path -LiteralPath $Root).Path
}

function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-LatestSummaryPath {
    param([string]$SummaryDir)

    if (-not (Test-Path -LiteralPath $SummaryDir)) {
        return $null
    }

    return (Get-ChildItem -LiteralPath $SummaryDir -File -Filter "run-*.summary.json" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 -ExpandProperty FullName)
}

function Invoke-ChecklistRun {
    param(
        [string]$RunnerPath,
        [string]$RepoPath,
        [string]$TaskFilePath,
        [string]$CodexMockPath,
        [string]$LockFilePath,
        [int]$MaxCodexRuns = 10,
        [switch]$DisableGatePreflight
    )

    $args = @(
        "-ExecutionPolicy", "Bypass",
        "-File", $RunnerPath,
        "-RepoRoot", $RepoPath,
        "-TaskFile", $TaskFilePath,
        "-CodexCommand", $CodexMockPath,
        "-LockFile", $LockFilePath,
        "-SkipManualValidation",
        "-ForceReleaseWithoutManual",
        "-SkipReleaseValidation",
        "-SkipAutoCommit",
        "-AllowDirtyWorkingTree",
        "-NoRollback",
        "-MaxCodexRuns", [string]$MaxCodexRuns
    )

    if ($DisableGatePreflight.IsPresent) {
        $args += "-DisableGatePreflight"
    }

    $ioRoot = Join-Path $RepoPath ".codex/tmp/unattended-guard-fault-tests/io"
    if (-not (Test-Path -LiteralPath $ioRoot)) {
        New-Item -ItemType Directory -Path $ioRoot -Force | Out-Null
    }

    $stamp = [guid]::NewGuid().ToString("n")
    $stdoutPath = Join-Path $ioRoot "$stamp.stdout.log"
    $stderrPath = Join-Path $ioRoot "$stamp.stderr.log"

    $proc = Start-Process -FilePath "powershell" -ArgumentList $args -PassThru -Wait -NoNewWindow -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
    $exitCode = [int]$proc.ExitCode
    $stdout = if (Test-Path -LiteralPath $stdoutPath) { Get-Content -LiteralPath $stdoutPath -Raw } else { "" }
    $stderr = if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath -Raw } else { "" }
    $output = @()
    if (-not [string]::IsNullOrWhiteSpace($stdout)) { $output += $stdout.TrimEnd() }
    if (-not [string]::IsNullOrWhiteSpace($stderr)) { $output += $stderr.TrimEnd() }

    return [pscustomobject]@{
        output = [string]::Join([Environment]::NewLine, @($output))
        exit_code = $exitCode
    }
}

$repoPath = Resolve-RepoPath -Root $RepoRoot
$runnerPath = Join-Path $repoPath "scripts/run-checklist-loop.ps1"
if (-not (Test-Path -LiteralPath $runnerPath)) {
    throw "Missing checklist runner: $runnerPath"
}

$summaryDir = Join-Path $repoPath ".codex/logs/checklist-loop"
$beforeSummary = Get-LatestSummaryPath -SummaryDir $summaryDir

$tempRoot = Join-Path $repoPath ".codex/tmp/unattended-guard-fault-tests"
if (-not (Test-Path -LiteralPath $tempRoot)) {
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
}

$runStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runDir = Join-Path $tempRoot $runStamp
New-Item -ItemType Directory -Path $runDir -Force | Out-Null

$codexMockPath = Join-Path $runDir "mock-codex.cmd"
@'
@echo off
echo mock-codex-ok
exit /b 0
'@ | Set-Content -LiteralPath $codexMockPath -Encoding ASCII

# Scenario 1: gate preflight parse failure should fail fast.
$scenario1Tasks = Join-Path $runDir "scenario1-invalid-gate.json"
@'
{
  "name": "fault-scenario-1",
  "version": "1.0.0",
  "tasks": [
    {
      "id": "invalid-gate-task",
      "title": "Invalid gate task",
      "commit_message": "chore: invalid gate",
      "prompt": "noop",
      "gates": [
        {
          "command": "powershell",
          "args": [
            "-NoLogo",
            "",
            "-Command",
            "Write-Host 'should never run'"
          ]
        }
      ]
    }
  ]
}
'@ | Set-Content -LiteralPath $scenario1Tasks -Encoding UTF8

$scenario1Lock = Join-Path $runDir "scenario1.lock.json"
$scenario1 = Invoke-ChecklistRun -RunnerPath $runnerPath -RepoPath $repoPath -TaskFilePath $scenario1Tasks -CodexMockPath $codexMockPath -LockFilePath $scenario1Lock -MaxCodexRuns 5
Assert-Condition -Condition ($scenario1.exit_code -ne 0) -Message "Scenario 1 should fail fast on preflight gate parse."
Assert-Condition -Condition ($scenario1.output -like "*Invalid gate*") -Message "Scenario 1 output missing invalid gate marker."

# Scenario 2: codex run budget exhaustion should stop loop.
$scenario2Tasks = Join-Path $runDir "scenario2-budget.json"
@'
{
  "name": "fault-scenario-2",
  "version": "1.0.0",
  "tasks": [
    {
      "id": "budget-task-1",
      "title": "Budget task one",
      "commit_message": "chore: budget one",
      "prompt": "noop",
      "gates": []
    },
    {
      "id": "budget-task-2",
      "title": "Budget task two",
      "commit_message": "chore: budget two",
      "prompt": "noop",
      "gates": []
    }
  ]
}
'@ | Set-Content -LiteralPath $scenario2Tasks -Encoding UTF8

$scenario2Lock = Join-Path $runDir "scenario2.lock.json"
$scenario2 = Invoke-ChecklistRun -RunnerPath $runnerPath -RepoPath $repoPath -TaskFilePath $scenario2Tasks -CodexMockPath $codexMockPath -LockFilePath $scenario2Lock -MaxCodexRuns 1
Assert-Condition -Condition ($scenario2.exit_code -ne 0) -Message "Scenario 2 should fail on codex budget exhaustion."
Assert-Condition -Condition ($scenario2.output -like "*Codex run budget exceeded*") -Message "Scenario 2 output missing codex budget marker."

$afterScenario2SummaryPath = Get-LatestSummaryPath -SummaryDir $summaryDir
Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace($afterScenario2SummaryPath)) -Message "Scenario 2 did not generate run summary."
Assert-Condition -Condition ($afterScenario2SummaryPath -ne $beforeSummary) -Message "Scenario 2 summary was not updated."
$scenario2Summary = Get-Content -LiteralPath $afterScenario2SummaryPath -Raw | ConvertFrom-Json
Assert-Condition -Condition ([string]$scenario2Summary.error_class -eq "budget_exhausted") -Message "Scenario 2 summary error_class mismatch."
Assert-Condition -Condition ([string]$scenario2Summary.failed_task_id -eq "budget-task-2") -Message "Scenario 2 summary failed_task_id mismatch."

# Scenario 3: lock conflict should fail immediately.
$scenario3Tasks = Join-Path $runDir "scenario3-lock.json"
@'
{
  "name": "fault-scenario-3",
  "version": "1.0.0",
  "tasks": [
    {
      "id": "lock-task",
      "title": "Lock conflict task",
      "commit_message": "chore: lock",
      "prompt": "noop",
      "gates": []
    }
  ]
}
'@ | Set-Content -LiteralPath $scenario3Tasks -Encoding UTF8

$scenario3Lock = Join-Path $runDir "scenario3.lock.json"
$lockPayload = [ordered]@{
    owner_kind = "checklist-loop"
    run_id = "test-lock"
    pid = $PID
    started_at = [DateTime]::UtcNow.ToString("o")
    repo_root = $repoPath
    task_file = $scenario3Tasks
}
$lockPayload | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $scenario3Lock -Encoding UTF8

$scenario3 = Invoke-ChecklistRun -RunnerPath $runnerPath -RepoPath $repoPath -TaskFilePath $scenario3Tasks -CodexMockPath $codexMockPath -LockFilePath $scenario3Lock -MaxCodexRuns 5
Assert-Condition -Condition ($scenario3.exit_code -ne 0) -Message "Scenario 3 should fail on lock conflict."
Assert-Condition -Condition ($scenario3.output -like "*Another checklist loop is running*") -Message "Scenario 3 output missing lock conflict marker."

Write-Host "UNATTENDED_GUARD_FAULTS: PASS" -ForegroundColor Green
Write-Host "scenario2_summary: $afterScenario2SummaryPath"
