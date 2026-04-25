param(
    [string]$RepoRoot = ".",
    [string]$GovernanceKitRoot = "",
    [string]$CodexCommand = "codex",
    [ValidateSet("quick", "full")]
    [string]$QualityProfile = "quick",
    [string]$Configuration = "Debug",
    [string]$RefactorMode = "architecture-refactor",
    [int]$MaxCycles = 20,
    [int]$LoopIterationsPerCycle = 8,
    [int]$MaxGovernanceFixAttempts = 2,
    [int]$MaxRepoFixAttempts = 2,
    [int]$LockStaleAfterMinutes = 30,
    [switch]$SkipGovernanceCycle,
    [switch]$SkipQualityGates,
    [switch]$SkipTaskLoop,
    [switch]$ContinueWhenNoEligibleTask,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$environmentBootstrap = Join-Path $PSScriptRoot "..\env\Initialize-WindowsProcessEnvironment.ps1"
if (Test-Path -LiteralPath $environmentBootstrap) {
    . $environmentBootstrap
}

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$kitCandidate = $GovernanceKitRoot
if ([string]::IsNullOrWhiteSpace($kitCandidate)) {
    $kitCandidate = Join-Path (Split-Path -Parent $repoPath) "repo-governance-hub"
}
elseif (-not [System.IO.Path]::IsPathRooted(($kitCandidate -replace '/', '\'))) {
    $kitCandidate = Join-Path (Split-Path -Parent $repoPath) $kitCandidate
}
$kitPath = (Resolve-Path -LiteralPath $kitCandidate -ErrorAction SilentlyContinue).Path
if ([string]::IsNullOrWhiteSpace($kitPath)) {
    throw "Governance kit path not found: $kitCandidate"
}
$script:runId = [guid]::NewGuid().ToString("n")
$script:startUtc = [DateTime]::UtcNow
$logRoot = Join-Path $repoPath (".codex/logs/safe-autopilot/" + (Get-Date -Format "yyyyMMdd-HHmmss") + "-" + $script:runId)
$lockPath = Join-Path $repoPath ".codex/safe-autopilot.lock.json"

function Test-ProcessAlive {
    param([int]$ProcessId)

    if ($ProcessId -le 0) {
        return $false
    }

    try {
        $null = Get-Process -Id $ProcessId -ErrorAction Stop
        return $true
    }
    catch {
        return $false
    }
}

function Read-Json {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Acquire-Lock {
    param(
        [string]$Path,
        [int]$StaleAfterMinutes
    )

    $existing = Read-Json -Path $Path
    if ($null -ne $existing) {
        $existingPid = if ($null -ne $existing.PSObject.Properties["pid"]) { [int]$existing.pid } else { 0 }
        $ownerAlive = Test-ProcessAlive -ProcessId $existingPid
        $startedAt = $null
        if ($null -ne $existing.PSObject.Properties["started_at_utc"]) {
            try {
                $startedAt = [DateTime]::Parse([string]$existing.started_at_utc)
            }
            catch {
                $startedAt = $null
            }
        }

        $isStale = $true
        if ($null -ne $startedAt) {
            $ageMinutes = ([DateTime]::UtcNow - $startedAt.ToUniversalTime()).TotalMinutes
            $isStale = $ageMinutes -ge $StaleAfterMinutes
        }

        if ($ownerAlive -and -not $isStale -and $existingPid -ne $PID) {
            throw "Another safe-autopilot run is active (pid=$existingPid). Lock path: $Path"
        }

        if (-not $ownerAlive -or $isStale) {
            Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
        }
    }

    $lockDir = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $lockDir)) {
        New-Item -ItemType Directory -Path $lockDir -Force | Out-Null
    }

    $record = [ordered]@{
        owner = "safe-autopilot"
        run_id = $script:runId
        pid = $PID
        started_at_utc = [DateTime]::UtcNow.ToString("o")
        repo_root = $repoPath
        governance_kit_root = $kitPath
    }

    $record | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $Path -Encoding UTF8 -NoNewline
}

function Release-Lock {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $existing = Read-Json -Path $Path
    if ($null -eq $existing) {
        Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
        return
    }

    $existingPid = if ($null -ne $existing.PSObject.Properties["pid"]) { [int]$existing.pid } else { 0 }
    if ($existingPid -eq $PID) {
        Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
    }
}

function Assert-Command {
    param([string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command not found: $Name"
    }
}

function Invoke-LoggedCommand {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action,
        [string]$WorkDir = $repoPath
    )

    $safeName = ($Name -replace '[^a-zA-Z0-9._-]', '_')
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $logPath = Join-Path $logRoot ("$timestamp-$safeName.log")

    Push-Location $WorkDir
    try {
        & $Action *>&1 | Tee-Object -LiteralPath $logPath | Out-Host
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($null -eq $exitCode) {
        $exitCode = 0
    }

    return [pscustomobject]@{
        name = $Name
        exit_code = [int]$exitCode
        log_path = $logPath
    }
}

function Resolve-CodexStatusFromLog {
    param([string]$LogPath)

    if (-not (Test-Path -LiteralPath $LogPath)) {
        return ""
    }

    $statusLines = @(Get-Content -LiteralPath $LogPath | Where-Object { $_ -match '^STATUS:\s*' })
    if ($statusLines.Count -eq 0) {
        return ""
    }

    $lastLine = [string]$statusLines[$statusLines.Count - 1]
    return ($lastLine -replace '^STATUS:\s*', '').Trim()
}

function Invoke-CodexFix {
    param(
        [Parameter(Mandatory = $true)][string]$TargetRoot,
        [Parameter(Mandatory = $true)][string]$IssueTitle,
        [Parameter(Mandatory = $true)][string]$FailureLogPath,
        [Parameter(Mandatory = $true)][string]$FixTag
    )

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $promptPath = Join-Path $logRoot ("$timestamp-$FixTag.prompt.txt")
    $logPath = Join-Path $logRoot ("$timestamp-$FixTag.codex.log")

    $prompt = @"
You are running in non-interactive autonomous remediation mode.

Goal:
- Resolve the failure described by the log file below with the smallest safe change.
- Do not weaken quality gates, do not skip tests, do not remove safety checks.

Context:
- failure_log: $FailureLogPath
- target_repo: $TargetRoot

Required behavior:
1. Read the log and identify the root cause.
2. Implement a minimal fix.
3. Run the required verification commands for the touched scope.
4. Output concise status and the evidence commands/results.

Safety constraints:
- Keep compatibility and existing contracts.
- Do not use destructive git commands.
- If blocked by non-fatal environmental issues, apply safe fallback verification and record it.
"@

    Set-Content -LiteralPath $promptPath -Value $prompt -Encoding UTF8

    Push-Location $TargetRoot
    try {
        $cmd = @(
            "-a", "never",
            "-s", "workspace-write",
            "exec",
            "--cd", $TargetRoot,
            "-"
        )

        Get-Content -LiteralPath $promptPath -Raw | & $CodexCommand @cmd *>&1 | Tee-Object -LiteralPath $logPath
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($null -eq $exitCode) {
        $exitCode = 0
    }

    return [pscustomobject]@{
        exit_code = [int]$exitCode
        prompt_path = $promptPath
        log_path = $logPath
    }
}

function Invoke-GovernanceCycle {
    return Invoke-LoggedCommand -Name "governance-cycle" -WorkDir $kitPath -Action {
        & pwsh -NoProfile -ExecutionPolicy Bypass -File (Join-Path $kitPath "scripts/run-project-governance-cycle.ps1") -RepoPath $repoPath -RepoName (Split-Path -Leaf $repoPath) -Mode safe -ShowScope
    }
}

function Invoke-QualityGates {
    return Invoke-LoggedCommand -Name "quality-gates" -WorkDir $repoPath -Action {
        & pwsh -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoPath "scripts/quality/run-local-quality-gates.ps1") -Profile $QualityProfile -Configuration $Configuration
    }
}

function Invoke-TaskLoop {
    return Invoke-LoggedCommand -Name "task-loop" -WorkDir $repoPath -Action {
        & pwsh -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoPath "scripts/run-refactor-loop.ps1") -RepoRoot $repoPath -Mode $RefactorMode -CodexCommand $CodexCommand -MaxIterations $LoopIterationsPerCycle -MaxNoProgress 6 -MaxNonCodeProgress 4 -MaxExecFailuresPerTask 3 -MaxNoProgressPerTask 3 -MaxAutoRecoverPerTask 2 -IterationTimeoutSeconds 900 -IdleTimeoutSeconds 120 -LockStaleAfterMinutes 30 -PromptProfile compact -SkipManualGates
    }
}

Assert-Command -Name pwsh
Assert-Command -Name $CodexCommand
Assert-Command -Name dotnet

if (-not (Test-Path -LiteralPath (Join-Path $repoPath "scripts/run-refactor-loop.ps1"))) {
    throw "Missing required script: scripts/run-refactor-loop.ps1"
}

if (-not (Test-Path -LiteralPath (Join-Path $repoPath "scripts/quality/run-local-quality-gates.ps1"))) {
    throw "Missing required script: scripts/quality/run-local-quality-gates.ps1"
}

if (-not (Test-Path -LiteralPath (Join-Path $kitPath "scripts/run-project-governance-cycle.ps1"))) {
    throw ("Missing required script: " + (Join-Path $kitPath "scripts/run-project-governance-cycle.ps1"))
}

New-Item -ItemType Directory -Force -Path $logRoot | Out-Null
Acquire-Lock -Path $lockPath -StaleAfterMinutes $LockStaleAfterMinutes

try {
    Write-Host "SAFE_AUTOPILOT"
    Write-Host "run_id: $script:runId"
    Write-Host "repo_root: $repoPath"
    Write-Host "governance_kit_root: $kitPath"
    Write-Host "logs: $logRoot"

    if ($DryRun) {
        Write-Host "dry_run: true"
        Write-Host "planned_cycle_order: governance-cycle -> quality-gates -> task-loop"
        return
    }

    for ($cycle = 1; $cycle -le $MaxCycles; $cycle++) {
        Write-Host ""
        Write-Host "=== cycle $cycle / $MaxCycles ==="

        if (-not $SkipGovernanceCycle) {
            $gov = Invoke-GovernanceCycle
            if ($gov.exit_code -ne 0) {
                $fixed = $false
                for ($attempt = 1; $attempt -le $MaxGovernanceFixAttempts; $attempt++) {
                    Write-Host "AUTO_FIX governance-kit attempt $attempt/$MaxGovernanceFixAttempts"
                    $fix = Invoke-CodexFix -TargetRoot $kitPath -IssueTitle "governance-cycle failed" -FailureLogPath $gov.log_path -FixTag "gov-fix-$cycle-$attempt"
                    if ($fix.exit_code -ne 0) {
                        Write-Host "governance-kit fix command failed: $($fix.log_path)"
                        continue
                    }

                    $gov = Invoke-GovernanceCycle
                    if ($gov.exit_code -eq 0) {
                        $fixed = $true
                        break
                    }
                }

                if (-not $fixed -and $gov.exit_code -ne 0) {
                    throw "governance-cycle failed after auto-fix attempts. log=$($gov.log_path)"
                }
            }
        }

        if (-not $SkipQualityGates) {
            $gate = Invoke-QualityGates
            if ($gate.exit_code -ne 0) {
                $fixed = $false
                for ($attempt = 1; $attempt -le $MaxRepoFixAttempts; $attempt++) {
                    Write-Host "AUTO_FIX repo-quality attempt $attempt/$MaxRepoFixAttempts"
                    $fix = Invoke-CodexFix -TargetRoot $repoPath -IssueTitle "quality-gates failed" -FailureLogPath $gate.log_path -FixTag "repo-gate-fix-$cycle-$attempt"
                    if ($fix.exit_code -ne 0) {
                        Write-Host "repo fix command failed: $($fix.log_path)"
                        continue
                    }

                    $gate = Invoke-QualityGates
                    if ($gate.exit_code -eq 0) {
                        $fixed = $true
                        break
                    }
                }

                if (-not $fixed -and $gate.exit_code -ne 0) {
                    throw "quality-gates failed after auto-fix attempts. log=$($gate.log_path)"
                }
            }
        }

        if (-not $SkipTaskLoop) {
            $loop = Invoke-TaskLoop
            $loopStatus = Resolve-CodexStatusFromLog -LogPath $loop.log_path

            if ($loop.exit_code -ne 0 -or $loopStatus -eq "BLOCKED_NEEDS_HUMAN") {
                $fixed = $false
                for ($attempt = 1; $attempt -le $MaxRepoFixAttempts; $attempt++) {
                    Write-Host "AUTO_FIX repo-task-loop attempt $attempt/$MaxRepoFixAttempts"
                    $fix = Invoke-CodexFix -TargetRoot $repoPath -IssueTitle "task-loop blocked or failed" -FailureLogPath $loop.log_path -FixTag "repo-loop-fix-$cycle-$attempt"
                    if ($fix.exit_code -ne 0) {
                        Write-Host "repo loop-fix command failed: $($fix.log_path)"
                        continue
                    }

                    $loop = Invoke-TaskLoop
                    $loopStatus = Resolve-CodexStatusFromLog -LogPath $loop.log_path
                    if ($loop.exit_code -eq 0 -and $loopStatus -ne "BLOCKED_NEEDS_HUMAN") {
                        $fixed = $true
                        break
                    }
                }

                if (-not $fixed -and ($loop.exit_code -ne 0 -or $loopStatus -eq "BLOCKED_NEEDS_HUMAN")) {
                    throw "task-loop remained blocked/failed after auto-fix attempts. log=$($loop.log_path)"
                }
            }

            if ($loopStatus -eq "ALL_AUTOMATABLE_TASKS_DONE" -or $loopStatus -eq "ALL_TASKS_DONE") {
                Write-Host "STATUS: ALL_AUTOMATABLE_TASKS_DONE"
                Write-Host "cycle_end_reason: no remaining automatable tasks"
                break
            }

            if ($loopStatus -eq "NO_ELIGIBLE_TASK" -and -not $ContinueWhenNoEligibleTask.IsPresent) {
                Write-Host "STATUS: NO_ELIGIBLE_TASK"
                Write-Host "cycle_end_reason: no eligible task; stop by default"
                break
            }
        }
    }

    Write-Host "STATUS: ITERATION_COMPLETE_CONTINUE"
    Write-Host "safe-autopilot completed without unrecovered failures"
}
finally {
    Release-Lock -Path $lockPath
}
