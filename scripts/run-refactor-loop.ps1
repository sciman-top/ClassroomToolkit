param(
    [string]$RepoRoot = ".",
    [string]$Mode = "architecture-refactor",
    [string]$TaskFile = "docs/refactor/tasks.json",
    [string]$StateFile = ".codex/refactor-state.json",
    [string]$ConfigFile = "",
    [string]$CodexCommand = "codex",
    [string]$SkillPath = "",
    [int]$MaxIterations = 10,
    [int]$MaxNoProgress = 3,
    [int]$MaxNonCodeProgress = 2,
    [int]$MaxExecFailuresPerTask = 1,
    [int]$MaxNoProgressPerTask = 1,
    [int]$MaxAutoRecoverPerTask = 1,
    [int]$IterationTimeoutSeconds = 900,
    [int]$IdleTimeoutSeconds = 120,
    [int]$LockStaleAfterMinutes = 30,
    [ValidateSet("compact", "full")]
    [string]$PromptProfile = "compact",
    [switch]$SkipManualGates,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$taskFileOverride = if ($PSBoundParameters.ContainsKey("TaskFile")) { $TaskFile } else { "" }
$stateFileOverride = if ($PSBoundParameters.ContainsKey("StateFile")) { $StateFile } else { "" }
$configFileOverride = if ($PSBoundParameters.ContainsKey("ConfigFile")) { $ConfigFile } else { "" }
$modeResolverPath = Join-Path $repoPath "scripts/refactor/resolve-refactor-mode.ps1"
$lockFilePath = Join-Path $repoPath ".codex/refactor-loop.lock.json"
$loopRunId = [guid]::NewGuid().ToString("n")

function Resolve-PowerShellExecutable {
    if (-not [string]::IsNullOrWhiteSpace($env:CODEX_ALLOW_WINDOWS_POWERSHELL)) {
        $legacy = Get-Command powershell -ErrorAction SilentlyContinue
        if ($legacy) { return [string]$legacy.Source }
    }

    $programFilesPwsh = if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        Join-Path $env:ProgramFiles "PowerShell\7\pwsh.exe"
    } else {
        $null
    }
    if (-not [string]::IsNullOrWhiteSpace($programFilesPwsh) -and (Test-Path -LiteralPath $programFilesPwsh)) {
        return $programFilesPwsh
    }

    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($pwsh) { return [string]$pwsh.Source }

    $legacyFallback = Get-Command powershell -ErrorAction SilentlyContinue
    if ($legacyFallback) { return [string]$legacyFallback.Source }

    throw "PowerShell 7 (pwsh) is required unless CODEX_ALLOW_WINDOWS_POWERSHELL=1 is set."
}

$powerShellExe = Resolve-PowerShellExecutable

function Get-Selection {
    & $powerShellExe -NoProfile -ExecutionPolicy Bypass -File $selectorPath -TaskFile $TaskFile -StateFile $StateFile -AsJson | ConvertFrom-Json
}

function Read-JsonFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    $maxAttempts = 3
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        try {
            return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
        }
        catch {
            if ($attempt -eq $maxAttempts) {
                throw
            }
            Start-Sleep -Milliseconds 120
        }
    }

    return $null
}

function Resolve-ModeContext {
    param(
        [string]$RepoPath,
        [string]$RequestedMode,
        [string]$RequestedTaskFile,
        [string]$RequestedStateFile,
        [string]$RequestedConfigFile
    )

    $rawModeInfo = & $powerShellExe -NoProfile -ExecutionPolicy Bypass -File $modeResolverPath -RepoRoot $RepoPath -Mode $RequestedMode -AsJson 2>&1
    $resolverExitCode = $LASTEXITCODE
    $jsonText = [string]::Join([Environment]::NewLine, @($rawModeInfo | Where-Object { $_ -is [string] }))
    if ($resolverExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($jsonText)) {
        throw "Unsupported refactor mode or family: $RequestedMode"
    }

    $modeInfo = $jsonText | ConvertFrom-Json

    if (-not [string]::IsNullOrWhiteSpace($RequestedTaskFile) -and [string]$RequestedTaskFile -ne [string]$modeInfo.tasks_file) {
        throw "Mode/path conflict: mode '$RequestedMode' resolves tasks_file '$($modeInfo.tasks_file)' but wrapper received '$RequestedTaskFile'."
    }

    if (-not [string]::IsNullOrWhiteSpace($RequestedStateFile) -and [string]$RequestedStateFile -ne [string]$modeInfo.state_file) {
        throw "Mode/path conflict: mode '$RequestedMode' resolves state_file '$($modeInfo.state_file)' but wrapper received '$RequestedStateFile'."
    }

    if (-not [string]::IsNullOrWhiteSpace($RequestedConfigFile)) {
        $resolvedConfig = if ($null -eq $modeInfo.config_file) { "" } else { [string]$modeInfo.config_file }
        if ($RequestedConfigFile -ne $resolvedConfig) {
            throw "Mode/path conflict: mode '$RequestedMode' resolves config_file '$resolvedConfig' but wrapper received '$RequestedConfigFile'."
        }
    }

    return $modeInfo
}

try {
    $modeInfo = Resolve-ModeContext -RepoPath $repoPath -RequestedMode $Mode -RequestedTaskFile $taskFileOverride -RequestedStateFile $stateFileOverride -RequestedConfigFile $configFileOverride
}
catch {
    Write-Host "STATUS: BLOCKED_NEEDS_HUMAN"
    Write-Host $_.Exception.Message
    return
}

$Mode = [string]$modeInfo.mode_id
$TaskFile = [string]$modeInfo.tasks_file
$StateFile = [string]$modeInfo.state_file
$ConfigFile = if ($null -ne $modeInfo.config_file) { [string]$modeInfo.config_file } else { "" }

$selectorPath = Join-Path $repoPath "scripts/refactor/select-next-task.ps1"
$stateUpdaterPath = Join-Path $repoPath "scripts/refactor/update-refactor-state.ps1"
$reconciliationCheckPath = Join-Path $repoPath "scripts/refactor/test-governing-reconciliation.ps1"
$consistencyCheckPath = Join-Path $repoPath "scripts/refactor/check-doc-consistency.ps1"
$loopLogRoot = Join-Path $repoPath (Join-Path ".codex/logs/refactor-loop" $Mode)
$uiProgressDocPath = Join-Path $repoPath "docs/validation/ui-window-system-progress.md"
$uiAcceptanceDocPath = Join-Path $repoPath "docs/validation/ui-window-system-acceptance.md"

New-Item -ItemType Directory -Force -Path $loopLogRoot | Out-Null

function Invoke-DocConsistencyCheck {
    param(
        [switch]$FixMode,
        [string]$Phase = "runtime",
        [switch]$Quiet
    )

    $arguments = @(
        "-File"
        $consistencyCheckPath
        "-TaskFile"
        $TaskFile
        "-StateFile"
        $StateFile
        "-AsJson"
    )

    if ($FixMode) {
        $arguments += "-Fix"
    }

    $raw = & pwsh @arguments
    $exitCode = $LASTEXITCODE
    $json = [string]::Join([Environment]::NewLine, @($raw))

    if ([string]::IsNullOrWhiteSpace($json)) {
        throw "Consistency check returned empty output (phase=$Phase, exit=$exitCode)."
    }

    $result = $json | ConvertFrom-Json
    $result | Add-Member -NotePropertyName exit_code -NotePropertyValue $exitCode -Force
    $result | Add-Member -NotePropertyName phase -NotePropertyValue $Phase -Force

    if (-not $Quiet) {
        Write-Host "CONSISTENCY_CHECK"
        Write-Host "phase: $Phase"
        Write-Host "status: $([string]$result.status)"
        Write-Host "issues_total: $([int]$result.issues_total)"
        Write-Host "issues_remaining: $([int]$result.issues_remaining)"
        Write-Host "fixes_applied: $([int]$result.fixes_applied)"
        $changedFiles = @($result.changed_files)
        Write-Host "changed_files: $(if ($changedFiles.Count -eq 0) { 'none' } else { ($changedFiles -join ' | ') })"
    }

    return $result
}

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

function Acquire-LoopLock {
    param(
        [string]$Path,
        [int]$StaleAfterMinutes
    )

    $existingLock = Read-JsonFile -Path $Path
    if ($null -ne $existingLock) {
        $existingPid = 0
        if ($null -ne $existingLock.PSObject.Properties["pid"]) {
            $existingPid = [int]$existingLock.pid
        }

        $ownerAlive = Test-ProcessAlive -ProcessId $existingPid
        if ($ownerAlive -and $existingPid -ne $PID) {
            throw "Another autonomous refactor loop is already running with PID $existingPid. Lock file: $Path"
        }

        $startedAt = $null
        if ($null -ne $existingLock.PSObject.Properties["started_at"]) {
            try {
                $startedAt = [DateTime]::Parse([string]$existingLock.started_at)
            }
            catch {
                $startedAt = $null
            }
        }

        $isStale = $false
        if ($null -eq $startedAt) {
            $isStale = $true
        }
        else {
            $age = [DateTime]::UtcNow - $startedAt.ToUniversalTime()
            $isStale = $age.TotalMinutes -ge $StaleAfterMinutes
        }

        if (-not $ownerAlive -or $isStale) {
            throw "Stale or ambiguous loop ownership detected in $Path. Review and remove the lock manually before retrying."
        }
    }

    $lockRecord = [ordered]@{
        owner_kind = "wrapper"
        loop_run_id = $loopRunId
        pid = $PID
        started_at = [DateTime]::UtcNow.ToString("o")
        mode_id = $Mode
        mode_family = [string]$modeInfo.mode_family
        repo_root = $repoPath
        state_file = $StateFile
        task_file = $TaskFile
        config_file = $ConfigFile
    }

    $lockDirectory = Split-Path -Parent $Path
    if ($lockDirectory -and -not (Test-Path -LiteralPath $lockDirectory)) {
        New-Item -ItemType Directory -Path $lockDirectory -Force | Out-Null
    }

    $lockRecord | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $Path -Encoding UTF8 -NoNewline
}

function Release-LoopLock {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $existingLock = Read-JsonFile -Path $Path
    if ($null -eq $existingLock) {
        Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
        return
    }

    $existingPid = 0
    if ($null -ne $existingLock.PSObject.Properties["pid"]) {
        $existingPid = [int]$existingLock.pid
    }

    if ($existingPid -eq $PID) {
        Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
    }
}

function Resolve-CodexLauncher {
    param([string]$Command)

    $resolved = Get-Command $Command -ErrorAction Stop
    $source = $resolved.Source

    if ([string]::IsNullOrWhiteSpace($source)) {
        $source = $resolved.Definition
    }

    $extension = [System.IO.Path]::GetExtension($source)
    switch ($extension.ToLowerInvariant()) {
        ".ps1" {
            $cmdSibling = [System.IO.Path]::ChangeExtension($source, ".cmd")
            if (Test-Path -LiteralPath $cmdSibling) {
                return [pscustomobject]@{
                    FilePath = "cmd.exe"
                    PrefixArguments = @(
                        "/c"
                        $cmdSibling
                    )
                }
            }

            return [pscustomobject]@{
                FilePath = $powerShellExe
                PrefixArguments = @(
                    "-NoLogo"
                    "-NoProfile"
                    "-ExecutionPolicy"
                    "Bypass"
                    "-File"
                    $source
                )
            }
        }
        ".cmd" {
            return [pscustomobject]@{
                FilePath = "cmd.exe"
                PrefixArguments = @(
                    "/c"
                    $source
                )
            }
        }
        default {
            return [pscustomobject]@{
                FilePath = $source
                PrefixArguments = @()
            }
        }
    }
}

function Resolve-ExecutionSkillPath {
    param(
        [string]$RequestedPath,
        [string]$RepoPath
    )

    $candidatePaths = @()

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        $candidatePaths += $RequestedPath
    }

    if (-not [string]::IsNullOrWhiteSpace($env:CODEX_HOME)) {
        $candidatePaths += Join-Path $env:CODEX_HOME "skills/autonomous-execution-loop/SKILL.md"
    }

    if (-not [string]::IsNullOrWhiteSpace($HOME)) {
        $candidatePaths += Join-Path $HOME ".codex/skills/autonomous-execution-loop/SKILL.md"
    }

    $candidatePaths += Join-Path (Split-Path -Parent $RepoPath) "skills-manager\overrides\autonomous-execution-loop\SKILL.md"
    $candidatePaths += Join-Path $RepoPath ".codex/skills/autonomous-execution-loop/SKILL.md"
    $candidatePaths += Join-Path $RepoPath ".codex/skills/autonomous-refactor-loop/SKILL.md"

    foreach ($candidatePath in $candidatePaths) {
        if (-not [string]::IsNullOrWhiteSpace($candidatePath) -and (Test-Path -LiteralPath $candidatePath)) {
            return (Resolve-Path -LiteralPath $candidatePath).Path
        }
    }

    throw "No usable autonomous execution skill path was found. Checked explicit path, CODEX_HOME, user .codex skills, override source, and repo-local skills."
}

$SkillPath = Resolve-ExecutionSkillPath -RequestedPath $SkillPath -RepoPath $repoPath

function Invoke-CodexIteration {
    param(
        [string]$RepoPath,
        [string]$Command,
        [string]$Prompt,
        [string]$TaskId,
        [int]$TimeoutSeconds,
        [int]$IdleTimeoutSeconds,
        [int]$IterationNumber
    )

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $stdoutPath = Join-Path $loopLogRoot "$timestamp-$IterationNumber-$TaskId.stdout.log"
    $stderrPath = Join-Path $loopLogRoot "$timestamp-$IterationNumber-$TaskId.stderr.log"
    $stdinPath = Join-Path $loopLogRoot "$timestamp-$IterationNumber-$TaskId.prompt.txt"
    $launcher = Resolve-CodexLauncher -Command $Command

    Set-Content -LiteralPath $stdinPath -Value $Prompt -Encoding UTF8

    $argumentList = @(
        @($launcher.PrefixArguments)
        "exec"
        "--cd"
        $RepoPath
        "-"
    )

    $process = Start-Process -FilePath $launcher.FilePath `
        -ArgumentList $argumentList `
        -WorkingDirectory $RepoPath `
        -RedirectStandardInput $stdinPath `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -PassThru

    $startedAt = [DateTime]::UtcNow
    $lastActivityAt = $startedAt
    $lastStdoutLength = -1L
    $lastStderrLength = -1L
    $timedOutReason = $null

    while (-not $process.HasExited) {
        Start-Sleep -Seconds 2

        $stdoutLength = 0L
        if (Test-Path -LiteralPath $stdoutPath) {
            $stdoutLength = (Get-Item -LiteralPath $stdoutPath).Length
        }

        $stderrLength = 0L
        if (Test-Path -LiteralPath $stderrPath) {
            $stderrLength = (Get-Item -LiteralPath $stderrPath).Length
        }

        if ($stdoutLength -ne $lastStdoutLength -or $stderrLength -ne $lastStderrLength) {
            $lastActivityAt = [DateTime]::UtcNow
            $lastStdoutLength = $stdoutLength
            $lastStderrLength = $stderrLength
        }

        $now = [DateTime]::UtcNow
        if (($now - $startedAt).TotalSeconds -ge $TimeoutSeconds) {
            $timedOutReason = "total-timeout"
            break
        }

        if (($now - $lastActivityAt).TotalSeconds -ge $IdleTimeoutSeconds) {
            $timedOutReason = "idle-timeout"
            break
        }
    }

    if ($null -ne $timedOutReason -and -not $process.HasExited) {
        try {
            & taskkill /PID $process.Id /T /F | Out-Null
        }
        catch {
            try {
                $process.Kill($true)
            }
            catch {
            }
        }

        return [pscustomobject]@{
            TimedOut = $true
            TimedOutReason = $timedOutReason
            ExitCode = $null
            StdoutPath = $stdoutPath
            StderrPath = $stderrPath
        }
    }

    return [pscustomobject]@{
        TimedOut = $false
        TimedOutReason = $null
        ExitCode = if ($null -ne $process.ExitCode) { [int]$process.ExitCode } else { -1 }
        StdoutPath = $stdoutPath
        StderrPath = $stderrPath
    }
}

function Get-ObservableReport {
    param([string]$StdoutPath)

    if (-not (Test-Path -LiteralPath $StdoutPath)) {
        return [pscustomobject]@{
            Status = $null
            Sections = @()
        }
    }

    $lines = @(Get-Content -LiteralPath $StdoutPath -Encoding UTF8)
    $status = $null
    $sections = @()

    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = $lines[$index]
        if ($line -match '^STATUS:\s*(.+)$') {
            $status = [string]$Matches[1]
            continue
        }

        if ($line -notmatch '^(EXECUTION_PLAN|RESULT_SUMMARY)$') {
            continue
        }

        $section = @($line)
        for ($inner = $index + 1; $inner -lt $lines.Count; $inner++) {
            $nextLine = $lines[$inner]
            if ($nextLine -match '^(EXECUTION_PLAN|RESULT_SUMMARY)$' -or $nextLine -match '^STATUS:') {
                break
            }

            if ([string]::IsNullOrWhiteSpace($nextLine)) {
                break
            }

            if ($nextLine -match '^```') {
                continue
            }

            $section += $nextLine
        }

        $sections += ,$section
    }

    return [pscustomobject]@{
        Status = $status
        Sections = $sections
    }
}

function Get-StateTaskStatusMap {
    param($StateObject)

    $result = @{}
    if ($null -eq $StateObject -or $null -eq $StateObject.tasks) {
        return $result
    }

    foreach ($prop in $StateObject.tasks.PSObject.Properties) {
        $taskId = [string]$prop.Name
        $status = $null
        if ($null -ne $prop.Value -and $null -ne $prop.Value.PSObject.Properties["status"]) {
            $status = [string]$prop.Value.status
        }

        $summary = ""
        if ($null -ne $prop.Value -and $null -ne $prop.Value.PSObject.Properties["last_summary"]) {
            $summary = [string]$prop.Value.last_summary
        }

        $result[$taskId] = [pscustomobject]@{
            status = $status
            last_summary = $summary
        }
    }

    return $result
}

function Get-TaskTitleMap {
    param([string]$TaskFilePath)

    $result = @{}
    $doc = Read-JsonFile -Path $TaskFilePath
    if ($null -eq $doc -or $null -eq $doc.tasks) {
        return $result
    }

    foreach ($task in @($doc.tasks)) {
        if ($null -eq $task) {
            continue
        }

        $taskId = [string]$task.id
        if ([string]::IsNullOrWhiteSpace($taskId)) {
            continue
        }

        $result[$taskId] = [string]$task.title
    }

    return $result
}

function Format-TaskLabel {
    param(
        [string]$TaskId,
        [hashtable]$TitleMap
    )

    if ($TitleMap.ContainsKey($TaskId) -and -not [string]::IsNullOrWhiteSpace([string]$TitleMap[$TaskId])) {
        return "$TaskId ($($TitleMap[$TaskId]))"
    }

    return $TaskId
}

function Write-IterationProgress {
    param(
        [hashtable]$StatusBeforeMap,
        [hashtable]$StatusAfterMap,
        [hashtable]$TaskTitleMap,
        [bool]$HadStateProgress,
        [bool]$HadTaskGraphProgress,
        [int]$NoProgressCount,
        [int]$MaxNoProgressValue,
        [string]$NextTaskId,
        [string]$ChildStatus
    )

    $completedNow = [System.Collections.Generic.List[string]]::new()
    $blockedNow = [System.Collections.Generic.List[string]]::new()
    $deferredNow = [System.Collections.Generic.List[string]]::new()
    $omittedNow = [System.Collections.Generic.List[string]]::new()

    foreach ($taskId in $StatusAfterMap.Keys) {
        $beforeStatus = $null
        if ($StatusBeforeMap.ContainsKey($taskId)) {
            $beforeStatus = [string]$StatusBeforeMap[$taskId].status
        }

        $afterStatus = [string]$StatusAfterMap[$taskId].status
        if ($afterStatus -eq $beforeStatus) {
            continue
        }

        $label = Format-TaskLabel -TaskId $taskId -TitleMap $TaskTitleMap
        $summary = [string]$StatusAfterMap[$taskId].last_summary
        if ($afterStatus -eq "completed") {
            $completedNow.Add($label)
            continue
        }

        $entry = if ([string]::IsNullOrWhiteSpace($summary)) { $label } else { "${label}: $summary" }
        if ($afterStatus -eq "blocked") {
            $blockedNow.Add($entry)
            continue
        }
        if ($afterStatus -eq "deferred") {
            $deferredNow.Add($entry)
            continue
        }
        if ($afterStatus -eq "omitted") {
            $omittedNow.Add($entry)
            continue
        }
    }

    Write-Host "ITERATION_PROGRESS"
    Write-Host "state_changed: $HadStateProgress"
    Write-Host "task_graph_changed: $HadTaskGraphProgress"
    Write-Host "child_status: $(if ([string]::IsNullOrWhiteSpace($ChildStatus)) { 'none' } else { $ChildStatus })"
    Write-Host "completed_now: $(if ($completedNow.Count -eq 0) { 'none' } else { ($completedNow -join ' | ') })"
    Write-Host "blocked_now: $(if ($blockedNow.Count -eq 0) { 'none' } else { ($blockedNow -join ' | ') })"
    Write-Host "deferred_now: $(if ($deferredNow.Count -eq 0) { 'none' } else { ($deferredNow -join ' | ') })"
    Write-Host "omitted_now: $(if ($omittedNow.Count -eq 0) { 'none' } else { ($omittedNow -join ' | ') })"
    Write-Host "next_task: $(if ([string]::IsNullOrWhiteSpace($NextTaskId)) { 'none' } else { $NextTaskId })"
    Write-Host "no_progress_streak: $NoProgressCount/$MaxNoProgressValue"

    $statePath = Join-Path $repoPath $StateFile
    $taskPath = Join-Path $repoPath $TaskFile
    $progress = Get-PlanProgressSnapshot -TaskFilePath $taskPath -StateFilePath $statePath
    $briefTaskId = if ([string]::IsNullOrWhiteSpace($script:CurrentIterationTaskId)) { "none" } else { [string]$script:CurrentIterationTaskId }
    $briefFinalStatus = if ([string]::IsNullOrWhiteSpace($script:CurrentIterationFinalStatus)) { "unknown" } else { [string]$script:CurrentIterationFinalStatus }
    $briefCodeLike = if ($script:CurrentIterationCodeLikeChanges) { "yes" } else { "no" }
    $briefNextTask = if ([string]::IsNullOrWhiteSpace($NextTaskId)) { "none" } else { $NextTaskId }
    Write-Host "ITERATION_BRIEF"
    Write-Host "task: $briefTaskId"
    Write-Host "final_status: $briefFinalStatus"
    Write-Host "code_change: $briefCodeLike"
    Write-Host "next_task: $briefNextTask"

    Write-Host "PLAN_PROGRESS"
    Write-Host "progress_percent: $($progress.progress_percent)"
    Write-Host "closed: $($progress.closure)/$($progress.total_automatable)"
    Write-Host "completed: $($progress.completed)"
    Write-Host "in_progress: $($progress.in_progress)"
    Write-Host "pending: $($progress.pending)"
    Write-Host "blocked: $($progress.blocked)"
    Write-Host "deferred: $($progress.deferred)"
    Write-Host "omitted: $($progress.omitted)"
}

function Append-LineToDoc {
    param(
        [string]$Path,
        [string]$Line
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or [string]::IsNullOrWhiteSpace($Line)) {
        return
    }

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    if (-not (Test-Path -LiteralPath $Path)) {
        Set-Content -LiteralPath $Path -Value "$Line`r`n" -Encoding UTF8
        return
    }

    Add-Content -LiteralPath $Path -Value $Line -Encoding UTF8
}

function Handle-ManualGateSkip {
    param(
        [string]$TaskId,
        [string]$TaskTitle,
        [string]$GateId
    )

    $timestamp = [DateTime]::UtcNow.ToString("o")
    $summary = "Manual gate '$GateId' skipped by wrapper flag -SkipManualGates for task '$TaskId'."
    $evidenceDocRelative = ""
    if ($Mode -eq "ui-window-system") {
        $progressLine = "- $timestamp [gate-skip] $TaskId ($TaskTitle) gate=$GateId reason=skip-manual-gates"
        $acceptanceLine = "- $timestamp [gate-skip] gate=$GateId task=$TaskId evidence=skip-manual-gates"
        Append-LineToDoc -Path $uiProgressDocPath -Line $progressLine
        Append-LineToDoc -Path $uiAcceptanceDocPath -Line $acceptanceLine
        $evidenceDocRelative = "docs/validation/ui-window-system-acceptance.md"
    }

    & pwsh -NoProfile -ExecutionPolicy Bypass -File $stateUpdaterPath `
        -Action gate-skip `
        -StateFile $StateFile `
        -TaskId $TaskId `
        -Summary $summary `
        -Reason $GateId `
        -EvidenceDoc $evidenceDocRelative `
        -Mode $Mode `
        -ModeFamily ([string]$modeInfo.mode_family) | Out-Null

    Write-Host "MANUAL_GATE_SKIPPED"
    Write-Host "task_id: $TaskId"
    Write-Host "gate_id: $GateId"
    Write-Host "evidence_doc: $(if ([string]::IsNullOrWhiteSpace($evidenceDocRelative)) { 'none' } else { $evidenceDocRelative })"
}

function Get-SectionFields {
    param(
        $ObservableReport,
        [string]$SectionName
    )

    foreach ($section in @($ObservableReport.Sections)) {
        if ($null -eq $section -or $section.Count -eq 0) {
            continue
        }

        if ([string]$section[0] -ne $SectionName) {
            continue
        }

        $fields = @{}
        for ($index = 1; $index -lt $section.Count; $index++) {
            $line = [string]$section[$index]
            if ($line -notmatch '^\s*([^:]+):\s*(.*)$') {
                continue
            }

            $fields[[string]$Matches[1].Trim()] = [string]$Matches[2].Trim()
        }

        return $fields
    }

    return @{}
}

function Test-HasCodeLikeChanges {
    param([string]$FilesChangedValue)

    if ([string]::IsNullOrWhiteSpace($FilesChangedValue) -or $FilesChangedValue -eq "none") {
        return $false
    }

    foreach ($rawPath in $FilesChangedValue.Split(',')) {
        $path = [string]$rawPath.Trim()
        if ([string]::IsNullOrWhiteSpace($path)) {
            continue
        }

        if ($path.StartsWith("docs/", [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        if ($path.StartsWith(".codex/", [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        return $true
    }

    return $false
}

function New-IterationPrompt {
    param(
        [string]$TodayIso,
        [string]$SkillPathValue,
        [string]$RepoPath,
        [string]$ModeId,
        [string]$ModeFamily,
        [string]$TaskFilePath,
        [string]$StateFilePath,
        [string]$ConfigFilePath,
        [string]$TaskIdValue,
        [string]$TaskTitleValue,
        [bool]$Resume,
        [string]$PromptProfileValue,
        [int]$OwnerPid,
        [string]$LoopId
    )

    if ($PromptProfileValue -eq "full") {
        return @"
Today is $TodayIso.

Read and follow this skill first:
- $SkillPathValue

Execute one unattended autonomous execution iteration for ClassroomToolkit using the existing repo-local refactor adaptation layer.

Execution context:
- repo_root: $RepoPath
- mode_id: $ModeId
- mode_family: $ModeFamily
- task_file: $TaskFilePath
- state_file: $StateFilePath
- config_file: $(if ([string]::IsNullOrWhiteSpace($ConfigFilePath)) { 'none' } else { $ConfigFilePath })
- wrapper_file: scripts/run-refactor-loop.ps1
- selected_task_id: $TaskIdValue
- selected_task_title: $TaskTitleValue
- resume: $Resume

Lock context:
- lock_file: .codex/refactor-loop.lock.json
- wrapper_owner_pid: $OwnerPid
- wrapper_loop_run_id: $LoopId
- treat this lock as same-loop ownership unless it points to a different live owner

Required behavior:
- reuse the resolved repo-local mode files above; do not bootstrap a second execution layer
- use staged reading; read the execution layer first and only read the minimum governing docs needed for this task
- if the task is unsuitable or missing prerequisites, emit the correct blocker status and do not force execution
- otherwise complete the smallest safe closure path, verify it, and update state/tasks as needed
- if the task is too large, split it into child tasks and defer the parent
- do not revert unrelated existing user changes
- do not emit ALL_AUTOMATABLE_TASKS_DONE unless the resolved mode is truly at its final manual-regression or acceptance gate
- emit EXECUTION_PLAN, RESULT_SUMMARY, and exactly one STATUS line
"@
    }

    return @"
Today is $TodayIso.
Follow this skill:
- $SkillPathValue

Run exactly one unattended iteration for ClassroomToolkit.
Context:
- repo_root: $RepoPath
- mode_id: $ModeId
- mode_family: $ModeFamily
- task_file: $TaskFilePath
- state_file: $StateFilePath
- config_file: $(if ([string]::IsNullOrWhiteSpace($ConfigFilePath)) { 'none' } else { $ConfigFilePath })
- selected_task_id: $TaskIdValue
- selected_task_title: $TaskTitleValue
- resume: $Resume
- wrapper_owner_pid: $OwnerPid
- wrapper_loop_run_id: $LoopId

Rules:
- Reuse existing repo-local execution files. Do not bootstrap another layer.
- Read only minimum required files for this task.
- If unsuitable/missing prerequisites, stop with STATUS: BLOCKED_NEEDS_HUMAN.
- Otherwise finish the smallest safe closure path with required verification and state/task updates.
- If too large, split into child tasks and defer parent.
- Do not revert unrelated local changes.
- Keep output compact. Do not print command transcripts or long prose.

Output contract (strict):
EXECUTION_PLAN
- short bullets only (max 6 lines)
RESULT_SUMMARY
final_status: <completed|blocked|deferred|in_progress|pending|omitted>
files_changed: <comma-separated repo-relative paths or none>
verification_result: <pass|fail|pre_existing_failure|not_run>
next_action: <one short line>
STATUS: <ITERATION_COMPLETE_CONTINUE|BLOCKED_NEEDS_HUMAN|NO_ELIGIBLE_TASK|ALL_AUTOMATABLE_TASKS_DONE>
"@
}

function Test-IsRecoverableBlock {
    param(
        [string]$ChildStatus,
        [hashtable]$ResultSummaryFields
    )

    if ($ChildStatus -ne "BLOCKED_NEEDS_HUMAN") {
        return $false
    }

    $finalStatus = [string]$ResultSummaryFields["final_status"]
    $verificationResult = [string]$ResultSummaryFields["verification_result"]

    if ($finalStatus -ne "blocked") {
        return $false
    }

    return $verificationResult -eq "pre_existing_failure"
}

function Get-PlanProgressSnapshot {
    param(
        [string]$TaskFilePath,
        [string]$StateFilePath
    )

    $tasksDoc = Read-JsonFile -Path $TaskFilePath
    $stateDoc = Read-JsonFile -Path $StateFilePath

    $totalAutomatable = 0
    $completed = 0
    $blocked = 0
    $pending = 0
    $inProgress = 0
    $deferred = 0
    $omitted = 0

    foreach ($task in @($tasksDoc.tasks)) {
        $manualGate = $false
        if ($null -ne $task.PSObject.Properties["manual_gate"]) {
            $manualGate = [bool]$task.manual_gate
        }
        if ($manualGate) {
            continue
        }

        $totalAutomatable++
        $taskId = [string]$task.id
        $status = "pending"
        $taskStateProp = $stateDoc.tasks.PSObject.Properties[$taskId]
        if ($null -ne $taskStateProp -and $null -ne $taskStateProp.Value.PSObject.Properties["status"]) {
            $status = [string]$taskStateProp.Value.status
        }

        switch ($status) {
            "completed" { $completed++ }
            "blocked" { $blocked++ }
            "in_progress" { $inProgress++ }
            "deferred" { $deferred++ }
            "omitted" { $omitted++ }
            default { $pending++ }
        }
    }

    $closure = $completed + $omitted
    $progressPercent = if ($totalAutomatable -gt 0) {
        [Math]::Round(($closure * 100.0) / $totalAutomatable, 1)
    }
    else {
        100.0
    }

    return [pscustomobject]@{
        total_automatable = $totalAutomatable
        completed = $completed
        blocked = $blocked
        in_progress = $inProgress
        pending = $pending
        deferred = $deferred
        omitted = $omitted
        closure = $closure
        progress_percent = $progressPercent
    }
}

function Get-RecoverableBlockedTaskId {
    param(
        $Selection,
        [string]$StateFilePath
    )

    if ($null -eq $Selection -or [string]$Selection.status -ne "blocked") {
        return $null
    }

    $state = Read-JsonFile -Path $StateFilePath
    foreach ($item in @($Selection.remaining)) {
        $taskId = [string]$item.id
        if ([string]::IsNullOrWhiteSpace($taskId)) {
            continue
        }

        $taskStateProp = $state.tasks.PSObject.Properties[$taskId]
        if ($null -eq $taskStateProp -or [string]$taskStateProp.Value.status -ne "blocked") {
            continue
        }

        $latestBlock = @($state.blocked | Where-Object { [string]$_.task_id -eq $taskId } | Select-Object -Last 1)
        if ($latestBlock.Count -eq 0) {
            continue
        }

        if ([string]$latestBlock[0].reason -eq "pre_existing_failure") {
            return $taskId
        }
    }

    return $null
}

function Get-SelectionStatusValue {
    param($Selection)

    if ($null -ne $Selection -and $null -ne $Selection.PSObject.Properties["status"]) {
        return [string]$Selection.status
    }

    return $null
}

function Write-TerminalStatusFromSelection {
    param(
        [string]$SelectionStatus,
        [string]$TaskFile
    )

    if ($SelectionStatus -eq "done") {
        $consistency = Invoke-DocConsistencyCheck -FixMode -Phase "terminal-gate"
        if ([string]$consistency.status -eq "needs_human") {
            Write-Host "STATUS: NO_ELIGIBLE_TASK"
            Write-Host "Execution graph is exhausted, but consistency drift still requires human action before declaring done."
            $consistency | ConvertTo-Json -Depth 20
            return $true
        }

        $reconciliation = & pwsh -NoProfile -ExecutionPolicy Bypass -File $reconciliationCheckPath -TaskFile $TaskFile -AsJson | ConvertFrom-Json
        if (-not [string]::IsNullOrWhiteSpace($ConfigFile)) {
            $reconciliation = & pwsh -NoProfile -ExecutionPolicy Bypass -File $reconciliationCheckPath -TaskFile $TaskFile -ConfigFile $ConfigFile -AsJson | ConvertFrom-Json
        }
        if ([string]$reconciliation.status -eq "ok") {
            Write-Host "STATUS: ALL_AUTOMATABLE_TASKS_DONE"
            Write-Host "All automatable tasks completed."
        }
        else {
            Write-Host "STATUS: NO_ELIGIBLE_TASK"
            Write-Host "Execution graph is exhausted, but governing docs require reconciliation before declaring done."
            $reconciliation | ConvertTo-Json -Depth 20
        }
        return $true
    }

    if ($SelectionStatus -eq "blocked") {
        Write-Host "STATUS: NO_ELIGIBLE_TASK"
        Write-Host "No ready task found. Loop stopped."
        return $true
    }

    return $false
}

$noProgressCount = 0
$nonCodeProgressCount = 0
$autoRecoverAttempts = @{}
$autoRecoverHints = @{}
$execFailuresByTask = @{}
$noProgressByTask = @{}
Acquire-LoopLock -Path $lockFilePath -StaleAfterMinutes $LockStaleAfterMinutes

try {
    $preflightConsistency = Invoke-DocConsistencyCheck -FixMode -Phase "preflight"
    if ([string]$preflightConsistency.status -eq "needs_human") {
        Write-Host "STATUS: BLOCKED_NEEDS_HUMAN"
        Write-Host "Preflight consistency check found unresolved drift that cannot be auto-fixed."
        $preflightConsistency | ConvertTo-Json -Depth 20
        return
    }

for ($iteration = 1; $iteration -le $MaxIterations; $iteration++) {
    $selection = Get-Selection

    $selectionStatus = Get-SelectionStatusValue -Selection $selection
    if ($selectionStatus -eq "blocked") {
        $recoverableTaskId = Get-RecoverableBlockedTaskId -Selection $selection -StateFilePath (Join-Path $repoPath $StateFile)
        if (-not [string]::IsNullOrWhiteSpace($recoverableTaskId)) {
            $recoverCount = 0
            if ($autoRecoverAttempts.ContainsKey($recoverableTaskId)) {
                $recoverCount = [int]$autoRecoverAttempts[$recoverableTaskId]
            }

            if ($recoverCount -lt $MaxAutoRecoverPerTask) {
                $autoRecoverAttempts[$recoverableTaskId] = $recoverCount + 1
                $recoverSummary = "Auto-recover queued from blocked preflight selection (attempt $($autoRecoverAttempts[$recoverableTaskId])/$MaxAutoRecoverPerTask)."
                & pwsh -NoProfile -ExecutionPolicy Bypass -File $stateUpdaterPath -Action unblock -StateFile $StateFile -TaskId $recoverableTaskId -Summary $recoverSummary -Reason "auto-recover-preflight-pre-existing-failure" | Out-Null
                Write-Host "AUTO_RECOVERY"
                Write-Host "task_id: $recoverableTaskId"
                Write-Host "reason: blocked pre-existing failure"
                Write-Host "attempt: $($autoRecoverAttempts[$recoverableTaskId])/$MaxAutoRecoverPerTask"
                continue
            }
        }
    }

    if (Write-TerminalStatusFromSelection -SelectionStatus $selectionStatus -TaskFile $TaskFile) {
        if ($selectionStatus -eq "blocked") {
            $selection | ConvertTo-Json -Depth 100
        }
        break
    }

    $taskId = [string]$selection.id
    $taskTitle = [string]$selection.title
    $manualGateId = $null
    if ($null -ne $selection.PSObject.Properties["manual_gate"] -and -not [string]::IsNullOrWhiteSpace([string]$selection.manual_gate)) {
        $manualGateId = [string]$selection.manual_gate
    }

    if ($SkipManualGates.IsPresent -and -not [string]::IsNullOrWhiteSpace($manualGateId)) {
        Handle-ManualGateSkip -TaskId $taskId -TaskTitle $taskTitle -GateId $manualGateId
        continue
    }

    $script:CurrentIterationTaskId = $taskId
    $script:CurrentIterationFinalStatus = "unknown"
    $script:CurrentIterationCodeLikeChanges = $false
    $isResume = $false
    if ($null -ne $selection.PSObject.Properties["resume"]) {
        $isResume = [bool]$selection.resume
    }

    $taskExecFailures = 0
    if ($execFailuresByTask.ContainsKey($taskId)) {
        $taskExecFailures = [int]$execFailuresByTask[$taskId]
    }

    $taskNoProgressCount = 0
    if ($noProgressByTask.ContainsKey($taskId)) {
        $taskNoProgressCount = [int]$noProgressByTask[$taskId]
    }

    if ($taskExecFailures -ge $MaxExecFailuresPerTask -or $taskNoProgressCount -ge $MaxNoProgressPerTask) {
        $budgetSummary = "Task execution budget exhausted (exec_failures=$taskExecFailures/$MaxExecFailuresPerTask, no_progress=$taskNoProgressCount/$MaxNoProgressPerTask)."
        & pwsh -NoProfile -ExecutionPolicy Bypass -File $stateUpdaterPath -Action block -StateFile $StateFile -TaskId $taskId -Summary $budgetSummary -Reason "task-budget-threshold" | Out-Null
        Write-Host "STATUS: BLOCKED_NEEDS_HUMAN"
        Write-Host $budgetSummary
        break
    }

    $todayIso = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd")
    $prompt = New-IterationPrompt `
        -TodayIso $todayIso `
        -SkillPathValue $SkillPath `
        -RepoPath $repoPath `
        -ModeId $Mode `
        -ModeFamily ([string]$modeInfo.mode_family) `
        -TaskFilePath $TaskFile `
        -StateFilePath $StateFile `
        -ConfigFilePath $ConfigFile `
        -TaskIdValue $taskId `
        -TaskTitleValue $taskTitle `
        -Resume $isResume `
        -PromptProfileValue $PromptProfile `
        -OwnerPid $PID `
        -LoopId $loopRunId

    if ($autoRecoverHints.ContainsKey($taskId)) {
        $prompt += @"

Auto-recovery note for this task:
- previous attempt ended with recoverable pre_existing_failure
- first resolve that blocker with the smallest safe local change, then rerun required verification
"@
    }

    if ($DryRun) {
        Write-Host ""
        Write-Host "=== Iteration $iteration / $MaxIterations ==="
        Write-Host "Task: $taskId - $taskTitle"
        Write-Host ""
        Write-Host "Prompt preview:"
        Write-Host $prompt
        break
    }

    $statePath = Join-Path $repoPath $StateFile
    $taskPath = Join-Path $repoPath $TaskFile
    $stateBeforeCodex = Get-Content -LiteralPath $statePath -Raw
    $tasksBeforeCodex = Get-Content -LiteralPath $taskPath -Raw
    $stateBeforeObject = Read-JsonFile -Path $statePath
    $statusBeforeMap = Get-StateTaskStatusMap -StateObject $stateBeforeObject

    Write-Host ""
    Write-Host "=== Iteration $iteration / $MaxIterations ==="
    Write-Host "Task: $taskId - $taskTitle"

    $invokeResult = Invoke-CodexIteration -RepoPath $repoPath -Command $CodexCommand -Prompt $prompt -TaskId $taskId -TimeoutSeconds $IterationTimeoutSeconds -IdleTimeoutSeconds $IdleTimeoutSeconds -IterationNumber $iteration
    $observableReport = Get-ObservableReport -StdoutPath $invokeResult.StdoutPath
    foreach ($section in @($observableReport.Sections)) {
        foreach ($sectionLine in @($section)) {
            Write-Host $sectionLine
        }
        Write-Host ""
    }

    if ($invokeResult.TimedOut) {
        $noProgressCount++
        $taskNoProgressCount++
        $noProgressByTask[$taskId] = $taskNoProgressCount
        $timeoutLabel = if ($invokeResult.TimedOutReason -eq "idle-timeout") { "Iteration stopped after $IdleTimeoutSeconds seconds of no output/log activity." } else { "Iteration timed out after $IterationTimeoutSeconds seconds." }
        $timeoutSummary = "$timeoutLabel stdout=$($invokeResult.StdoutPath); stderr=$($invokeResult.StderrPath)"
        & pwsh -NoProfile -ExecutionPolicy Bypass -File $stateUpdaterPath -Action note -StateFile $StateFile -TaskId $taskId -Summary $timeoutSummary -Reason "timeout" | Out-Null
        Write-Host $timeoutSummary

        if ($taskNoProgressCount -ge $MaxNoProgressPerTask) {
            & pwsh -NoProfile -ExecutionPolicy Bypass -File $stateUpdaterPath -Action block -StateFile $StateFile -TaskId $taskId -Summary "Task blocked by per-task timeout/no-progress threshold. Last logs: stdout=$($invokeResult.StdoutPath); stderr=$($invokeResult.StderrPath)" -Reason "timeout-per-task-threshold" | Out-Null
            Write-Host "STATUS: BLOCKED_NEEDS_HUMAN"
            Write-Host "Per-task timeout/no-progress threshold reached. Task marked blocked."
            break
        }

        if ($noProgressCount -ge $MaxNoProgress) {
            & pwsh -NoProfile -ExecutionPolicy Bypass -File $stateUpdaterPath -Action block -StateFile $StateFile -TaskId $taskId -Summary "Task blocked after repeated timeouts. Last logs: stdout=$($invokeResult.StdoutPath); stderr=$($invokeResult.StderrPath)" -Reason "timeout-threshold" | Out-Null
            Write-Host "STATUS: BLOCKED_NEEDS_HUMAN"
            Write-Host "Timeout threshold reached. Task marked blocked."
            break
        }

        continue
    }

    $postIterationConsistency = Invoke-DocConsistencyCheck -FixMode -Phase "post-iteration"
    $stateAfter = Get-Content -LiteralPath $statePath -Raw
    $tasksAfter = Get-Content -LiteralPath $taskPath -Raw
    $hadStateProgress = $stateBeforeCodex -ne $stateAfter
    $hadTaskGraphProgress = $tasksBeforeCodex -ne $tasksAfter
    $hadMeaningfulProgress = $hadStateProgress -or $hadTaskGraphProgress
    $childStatus = $null
    if ($null -ne $observableReport.Status) {
        $childStatus = [string]$observableReport.Status
    }
    $resultSummaryFields = Get-SectionFields -ObservableReport $observableReport -SectionName "RESULT_SUMMARY"
    $hasCodeLikeChanges = Test-HasCodeLikeChanges -FilesChangedValue ([string]$resultSummaryFields["files_changed"])
    $script:CurrentIterationCodeLikeChanges = $hasCodeLikeChanges
    if ($resultSummaryFields.ContainsKey("final_status")) {
        $script:CurrentIterationFinalStatus = [string]$resultSummaryFields["final_status"]
    }

    $selectionAfter = Get-Selection
    $selectionAfterStatus = Get-SelectionStatusValue -Selection $selectionAfter
    $stateAfterObject = Read-JsonFile -Path $statePath
    $statusAfterMap = Get-StateTaskStatusMap -StateObject $stateAfterObject
    $taskTitleMap = Get-TaskTitleMap -TaskFilePath $taskPath
    $nextTaskId = $null
    if ($null -ne $selectionAfter -and $null -ne $selectionAfter.PSObject.Properties["id"]) {
        $nextTaskId = [string]$selectionAfter.id
    }

    if ([string]$postIterationConsistency.status -eq "needs_human") {
        Write-Host "STATUS: BLOCKED_NEEDS_HUMAN"
        Write-Host "Post-iteration consistency drift cannot be auto-fixed."
        $postIterationConsistency | ConvertTo-Json -Depth 20
        Write-IterationProgress -StatusBeforeMap $statusBeforeMap -StatusAfterMap $statusAfterMap -TaskTitleMap $taskTitleMap -HadStateProgress $hadStateProgress -HadTaskGraphProgress $hadTaskGraphProgress -NoProgressCount $noProgressCount -MaxNoProgressValue $MaxNoProgress -NextTaskId $nextTaskId -ChildStatus $childStatus
        break
    }

    if ($childStatus -eq "ALL_AUTOMATABLE_TASKS_DONE") {
        if (Write-TerminalStatusFromSelection -SelectionStatus $selectionAfterStatus -TaskFile $TaskFile) {
            Write-IterationProgress -StatusBeforeMap $statusBeforeMap -StatusAfterMap $statusAfterMap -TaskTitleMap $taskTitleMap -HadStateProgress $hadStateProgress -HadTaskGraphProgress $hadTaskGraphProgress -NoProgressCount $noProgressCount -MaxNoProgressValue $MaxNoProgress -NextTaskId $nextTaskId -ChildStatus $childStatus
            break
        }

        Write-Host "Ignoring child ALL_AUTOMATABLE_TASKS_DONE because another eligible task still exists."
    }

    if ($childStatus -eq "NO_ELIGIBLE_TASK") {
        if (Write-TerminalStatusFromSelection -SelectionStatus $selectionAfterStatus -TaskFile $TaskFile) {
            Write-IterationProgress -StatusBeforeMap $statusBeforeMap -StatusAfterMap $statusAfterMap -TaskTitleMap $taskTitleMap -HadStateProgress $hadStateProgress -HadTaskGraphProgress $hadTaskGraphProgress -NoProgressCount $noProgressCount -MaxNoProgressValue $MaxNoProgress -NextTaskId $nextTaskId -ChildStatus $childStatus
            break
        }

        if ($hadMeaningfulProgress -and $null -ne $selectionAfter -and $null -ne $selectionAfter.PSObject.Properties["id"]) {
            Write-Host "Ignoring child NO_ELIGIBLE_TASK because the next eligible task is $([string]$selectionAfter.id)."
        }
        else {
            Write-Host "STATUS: NO_ELIGIBLE_TASK"
            Write-Host "Child iteration reported that no eligible task is available."
            Write-IterationProgress -StatusBeforeMap $statusBeforeMap -StatusAfterMap $statusAfterMap -TaskTitleMap $taskTitleMap -HadStateProgress $hadStateProgress -HadTaskGraphProgress $hadTaskGraphProgress -NoProgressCount $noProgressCount -MaxNoProgressValue $MaxNoProgress -NextTaskId $nextTaskId -ChildStatus $childStatus
            break
        }
    }

    if ($childStatus -eq "ITERATION_COMPLETE_CONTINUE") {
        if (Write-TerminalStatusFromSelection -SelectionStatus $selectionAfterStatus -TaskFile $TaskFile) {
            Write-IterationProgress -StatusBeforeMap $statusBeforeMap -StatusAfterMap $statusAfterMap -TaskTitleMap $taskTitleMap -HadStateProgress $hadStateProgress -HadTaskGraphProgress $hadTaskGraphProgress -NoProgressCount $noProgressCount -MaxNoProgressValue $MaxNoProgress -NextTaskId $nextTaskId -ChildStatus $childStatus
            break
        }

        if ($null -ne $selectionAfter -and $null -ne $selectionAfter.PSObject.Properties["id"]) {
            Write-Host "Iteration completed. Next eligible task: $([string]$selectionAfter.id)"
        }
    }

    if ($childStatus -eq "BLOCKED_NEEDS_HUMAN") {
        $isRecoverableBlock = Test-IsRecoverableBlock -ChildStatus $childStatus -ResultSummaryFields $resultSummaryFields
        $currentRecoverCount = 0
        if ($autoRecoverAttempts.ContainsKey($taskId)) {
            $currentRecoverCount = [int]$autoRecoverAttempts[$taskId]
        }

        if ($isRecoverableBlock -and $currentRecoverCount -lt $MaxAutoRecoverPerTask -and $iteration -lt $MaxIterations) {
            $autoRecoverAttempts[$taskId] = $currentRecoverCount + 1
            $autoRecoverHints[$taskId] = [string]$resultSummaryFields["next_action"]
            $recoverSummary = "Auto-recover queued for pre_existing_failure (attempt $($autoRecoverAttempts[$taskId])/$MaxAutoRecoverPerTask)."
            & pwsh -NoProfile -ExecutionPolicy Bypass -File $stateUpdaterPath -Action unblock -StateFile $StateFile -TaskId $taskId -Summary $recoverSummary -Reason "auto-recover-pre-existing-failure" | Out-Null
            Write-Host "AUTO_RECOVERY"
            Write-Host "task_id: $taskId"
            Write-Host "reason: pre_existing_failure"
            Write-Host "attempt: $($autoRecoverAttempts[$taskId])/$MaxAutoRecoverPerTask"
            Write-Host "next_action: re-run task with blocker-first remediation"
            Write-IterationProgress -StatusBeforeMap $statusBeforeMap -StatusAfterMap $statusAfterMap -TaskTitleMap $taskTitleMap -HadStateProgress $hadStateProgress -HadTaskGraphProgress $hadTaskGraphProgress -NoProgressCount $noProgressCount -MaxNoProgressValue $MaxNoProgress -NextTaskId $nextTaskId -ChildStatus $childStatus
            continue
        }

        Write-Host "STATUS: BLOCKED_NEEDS_HUMAN"
        if ($hadStateProgress) {
            Write-Host "Child iteration marked the task as blocked."
        }
        else {
            Write-Host "Child iteration reported a blocker before any state mutation."
        }
        Write-IterationProgress -StatusBeforeMap $statusBeforeMap -StatusAfterMap $statusAfterMap -TaskTitleMap $taskTitleMap -HadStateProgress $hadStateProgress -HadTaskGraphProgress $hadTaskGraphProgress -NoProgressCount $noProgressCount -MaxNoProgressValue $MaxNoProgress -NextTaskId $nextTaskId -ChildStatus $childStatus
        break
    }

    $countedNoProgress = $false
    if ($invokeResult.ExitCode -ne 0 -and -not $hadMeaningfulProgress) {
        $noProgressCount++
        $taskNoProgressCount++
        $noProgressByTask[$taskId] = $taskNoProgressCount
        $taskExecFailures++
        $execFailuresByTask[$taskId] = $taskExecFailures
        $nonCodeProgressCount = 0
        $failureSummary = "Iteration failed with exit code $($invokeResult.ExitCode). stdout=$($invokeResult.StdoutPath); stderr=$($invokeResult.StderrPath)"
        & pwsh -NoProfile -ExecutionPolicy Bypass -File $stateUpdaterPath -Action note -StateFile $StateFile -TaskId $taskId -Summary $failureSummary -Reason "exec-failed" | Out-Null
        Write-Host $failureSummary
        Write-Host "Task exec-failure count: $taskExecFailures / $MaxExecFailuresPerTask"
        $countedNoProgress = $true

        if ($taskExecFailures -ge $MaxExecFailuresPerTask) {
            & pwsh -NoProfile -ExecutionPolicy Bypass -File $stateUpdaterPath -Action block -StateFile $StateFile -TaskId $taskId -Summary "Task blocked by per-task execution failure threshold. Last logs: stdout=$($invokeResult.StdoutPath); stderr=$($invokeResult.StderrPath)" -Reason "exec-failure-per-task-threshold" | Out-Null
            Write-Host "STATUS: BLOCKED_NEEDS_HUMAN"
            Write-Host "Per-task execution failure threshold reached. Task marked blocked."
            break
        }

        if ($taskNoProgressCount -ge $MaxNoProgressPerTask) {
            & pwsh -NoProfile -ExecutionPolicy Bypass -File $stateUpdaterPath -Action block -StateFile $StateFile -TaskId $taskId -Summary "Task blocked by per-task no-progress threshold after execution failure. Last logs: stdout=$($invokeResult.StdoutPath); stderr=$($invokeResult.StderrPath)" -Reason "no-progress-per-task-threshold" | Out-Null
            Write-Host "STATUS: BLOCKED_NEEDS_HUMAN"
            Write-Host "Per-task no-progress threshold reached. Task marked blocked."
            break
        }

        if ($noProgressCount -ge $MaxNoProgress) {
            & pwsh -NoProfile -ExecutionPolicy Bypass -File $stateUpdaterPath -Action block -StateFile $StateFile -TaskId $taskId -Summary "Task blocked after repeated execution failures. Last logs: stdout=$($invokeResult.StdoutPath); stderr=$($invokeResult.StderrPath)" -Reason "exec-failure-threshold" | Out-Null
            Write-Host "STATUS: BLOCKED_NEEDS_HUMAN"
            Write-Host "Execution failure threshold reached. Task marked blocked."
            break
        }
    }

    if (-not $hadMeaningfulProgress) {
        if (-not $countedNoProgress) {
            $noProgressCount++
            $taskNoProgressCount++
            $noProgressByTask[$taskId] = $taskNoProgressCount
        }
        $nonCodeProgressCount = 0
        Write-Host "No state/task-graph change after iteration. Retry count: $noProgressCount / $MaxNoProgress"
        Write-Host "Task no-progress count: $taskNoProgressCount / $MaxNoProgressPerTask"

        if ($taskNoProgressCount -ge $MaxNoProgressPerTask) {
            & pwsh -NoProfile -ExecutionPolicy Bypass -File $stateUpdaterPath -Action block -StateFile $StateFile -TaskId $taskId -Summary "Task blocked by per-task no-progress threshold. Last logs: stdout=$($invokeResult.StdoutPath); stderr=$($invokeResult.StderrPath)" -Reason "no-progress-per-task-threshold" | Out-Null
            Write-Host "STATUS: BLOCKED_NEEDS_HUMAN"
            Write-Host "Per-task no-progress threshold reached. Task marked blocked."
            Write-IterationProgress -StatusBeforeMap $statusBeforeMap -StatusAfterMap $statusAfterMap -TaskTitleMap $taskTitleMap -HadStateProgress $hadStateProgress -HadTaskGraphProgress $hadTaskGraphProgress -NoProgressCount $noProgressCount -MaxNoProgressValue $MaxNoProgress -NextTaskId $nextTaskId -ChildStatus $childStatus
            break
        }

        if ($noProgressCount -ge $MaxNoProgress) {
            Write-Host "STATUS: NO_ELIGIBLE_TASK"
            Write-Host "No progress threshold reached. Loop stopped."
            Write-IterationProgress -StatusBeforeMap $statusBeforeMap -StatusAfterMap $statusAfterMap -TaskTitleMap $taskTitleMap -HadStateProgress $hadStateProgress -HadTaskGraphProgress $hadTaskGraphProgress -NoProgressCount $noProgressCount -MaxNoProgressValue $MaxNoProgress -NextTaskId $nextTaskId -ChildStatus $childStatus
            break
        }
    }
    else {
        $noProgressCount = 0
        $noProgressByTask[$taskId] = 0
        $execFailuresByTask[$taskId] = 0
        if ($hasCodeLikeChanges) {
            $nonCodeProgressCount = 0
        }
        else {
            $nonCodeProgressCount++
            Write-Host "Non-code-only progress streak: $nonCodeProgressCount / $MaxNonCodeProgress"

            if ($nonCodeProgressCount -ge $MaxNonCodeProgress) {
                Write-Host "STATUS: NO_ELIGIBLE_TASK"
                Write-Host "Non-code-only progress threshold reached. Loop stopped to conserve tokens."
                Write-IterationProgress -StatusBeforeMap $statusBeforeMap -StatusAfterMap $statusAfterMap -TaskTitleMap $taskTitleMap -HadStateProgress $hadStateProgress -HadTaskGraphProgress $hadTaskGraphProgress -NoProgressCount $noProgressCount -MaxNoProgressValue $MaxNoProgress -NextTaskId $nextTaskId -ChildStatus $childStatus
                break
            }
        }
    }

    Write-IterationProgress -StatusBeforeMap $statusBeforeMap -StatusAfterMap $statusAfterMap -TaskTitleMap $taskTitleMap -HadStateProgress $hadStateProgress -HadTaskGraphProgress $hadTaskGraphProgress -NoProgressCount $noProgressCount -MaxNoProgressValue $MaxNoProgress -NextTaskId $nextTaskId -ChildStatus $childStatus
}
}
finally {
    Release-LoopLock -Path $lockFilePath
}

