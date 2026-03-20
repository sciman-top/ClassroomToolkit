param(
    [string]$RepoRoot = ".",
    [ValidateSet("default")]
    [string]$Scenario = "default"
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

function Invoke-DefaultScenario {
    param([string]$RepoPath)

    $summaryDir = Join-Path $RepoPath ".codex/logs/checklist-loop"
    $beforeSummary = Get-LatestSummaryPath -SummaryDir $summaryDir
    $lockPath = Join-Path $RepoPath ".codex/checklist-loop.lock.json"

    $tempRoot = Join-Path $RepoPath ".codex/tmp/checklist-smoke"
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

    $tasksPath = Join-Path $runDir "smoke-tasks.json"
    @'
{
  "name": "checklist-loop-smoke",
  "version": "1.0.0",
  "tasks": [
    {
      "id": "smoke-task",
      "title": "Checklist smoke task",
      "commit_message": "chore: smoke no-op",
      "prompt": "smoke",
      "gates": [
        {
          "command": "powershell",
          "args": [
            "-NoLogo",
            "-NoProfile",
            "-Command",
            "if ('A|B' -ne 'A|B') { exit 9 }"
          ]
        }
      ]
    }
  ]
}
'@ | Set-Content -LiteralPath $tasksPath -Encoding UTF8

    $runnerPath = Join-Path $RepoPath "scripts/run-checklist-loop.ps1"
    & powershell -ExecutionPolicy Bypass -File $runnerPath `
        -RepoRoot $RepoPath `
        -TaskFile $tasksPath `
        -CodexCommand $codexMockPath `
        -SkipManualValidation `
        -ForceReleaseWithoutManual `
        -SkipReleaseValidation `
        -SkipAutoCommit `
        -AllowDirtyWorkingTree `
        -NoRollback

    if ($LASTEXITCODE -ne 0) {
        throw "run-checklist-loop smoke execution failed with exit code: $LASTEXITCODE"
    }

    $afterSummary = Get-LatestSummaryPath -SummaryDir $summaryDir
    Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace($afterSummary)) -Message "No checklist summary generated."
    Assert-Condition -Condition ($afterSummary -ne $beforeSummary) -Message "Checklist summary was not updated."

    $summary = Get-Content -LiteralPath $afterSummary -Raw | ConvertFrom-Json
    Assert-Condition -Condition ([string]$summary.status -eq "completed") -Message "Summary status is not completed."
    Assert-Condition -Condition ([string]$summary.error_class -eq "") -Message "Summary error_class should be empty on success."
    Assert-Condition -Condition (@($summary.tasks).Count -eq 1) -Message "Unexpected task count in summary."
    Assert-Condition -Condition ([string]$summary.tasks[0].status -eq "completed") -Message "Task status is not completed."
    Assert-Condition -Condition (@($summary.tasks[0].gates).Count -eq 1) -Message "Unexpected gate count in summary."
    Assert-Condition -Condition ([string]$summary.tasks[0].gates[0].status -eq "passed") -Message "Gate status is not passed."
    Assert-Condition -Condition (-not (Test-Path -LiteralPath $lockPath)) -Message "Lock file still exists after run."

    Write-Host "CHECKLIST_SMOKE: PASS" -ForegroundColor Green
    Write-Host "summary: $afterSummary"
}

$repoPath = Resolve-RepoPath -Root $RepoRoot

switch ($Scenario) {
    "default" { Invoke-DefaultScenario -RepoPath $repoPath }
    default { throw "Unsupported scenario: $Scenario" }
}
