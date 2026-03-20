param(
    [string]$RepoRoot = ".",
    [string]$TaskFile = "",
    [string]$CodexCommand = "codex",
    [string]$StartFromTaskId = "",
    [int]$MaxAttemptsPerTask = 1,
    [string]$LockFile = ".codex/checklist-loop.lock.json",
    [int]$LockStaleAfterMinutes = 30,
    [int]$CodexTimeoutSeconds = 1200,
    [int]$CodexIdleTimeoutSeconds = 180,
    [int]$GateTimeoutSeconds = 900,
    [int]$GateIdleTimeoutSeconds = 120,
    [switch]$DisableGatePreflight,
    [switch]$SkipManualValidation,
    [switch]$ForceReleaseWithoutManual,
    [switch]$SkipReleaseValidation,
    [switch]$SkipAutoCommit,
    [switch]$NoRollback,
    [switch]$AllowDirtyWorkingTree,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

function Write-Phase {
    param([string]$Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Assert-Command {
    param(
        [string]$Name,
        [string]$Hint
    )

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Missing command: $Name. $Hint"
    }
}

function Get-RepoRootPath {
    param([string]$Root)
    return (Resolve-Path -LiteralPath $Root).Path
}

function Resolve-InputPath {
    param(
        [string]$RootPath,
        [string]$InputPath
    )

    if ([System.IO.Path]::IsPathRooted($InputPath)) {
        if (Test-Path -LiteralPath $InputPath) {
            return (Resolve-Path -LiteralPath $InputPath).Path
        }
        return [System.IO.Path]::GetFullPath($InputPath)
    }

    return Join-Path $RootPath $InputPath
}

function Test-CleanWorkingTree {
    param([string]$RootPath)
    Push-Location $RootPath
    try {
        $status = git status --porcelain
        return [string]::IsNullOrWhiteSpace($status)
    }
    finally {
        Pop-Location
    }
}

function Read-JsonFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
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

function Acquire-RunLock {
    param(
        [string]$Path,
        [string]$RunId,
        [string]$RepoPath,
        [int]$StaleAfterMinutes
    )

    $existing = Read-JsonFile -Path $Path
    if ($null -ne $existing) {
        $ownerPid = if ($null -ne $existing.PSObject.Properties["pid"]) { [int]$existing.pid } else { 0 }
        $ownerAlive = Test-ProcessAlive -ProcessId $ownerPid
        $startedAt = $null
        if ($null -ne $existing.PSObject.Properties["started_at"]) {
            try {
                $startedAt = [DateTime]::Parse([string]$existing.started_at).ToUniversalTime()
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
            $isStale = ([DateTime]::UtcNow - $startedAt).TotalMinutes -ge $StaleAfterMinutes
        }

        if ($ownerAlive -and -not $isStale -and $ownerPid -ne $PID) {
            throw "Another checklist loop is running with PID $ownerPid. Lock file: $Path"
        }
    }

    $lockDir = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($lockDir) -and -not (Test-Path -LiteralPath $lockDir)) {
        New-Item -ItemType Directory -Force -Path $lockDir | Out-Null
    }

    $record = [ordered]@{
        owner_kind = "checklist-loop"
        run_id = $RunId
        pid = $PID
        started_at = [DateTime]::UtcNow.ToString("o")
        repo_root = $RepoPath
        task_file = $TaskFile
    }

    $record | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $Path -Encoding UTF8 -NoNewline
}

function Release-RunLock {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $existing = Read-JsonFile -Path $Path
    if ($null -eq $existing) {
        Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
        return
    }

    $ownerPid = if ($null -ne $existing.PSObject.Properties["pid"]) { [int]$existing.pid } else { 0 }
    if ($ownerPid -eq $PID) {
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

    $extension = [System.IO.Path]::GetExtension($source).ToLowerInvariant()
    switch ($extension) {
        ".ps1" {
            $cmdSibling = [System.IO.Path]::ChangeExtension($source, ".cmd")
            if (Test-Path -LiteralPath $cmdSibling) {
                return [pscustomobject]@{
                    FilePath = "cmd.exe"
                    PrefixArguments = @("/c", $cmdSibling)
                }
            }

            return [pscustomobject]@{
                FilePath = "powershell.exe"
                PrefixArguments = @("-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $source)
            }
        }
        ".cmd" {
            return [pscustomobject]@{
                FilePath = "cmd.exe"
                PrefixArguments = @("/c", $source)
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

function ConvertTo-CommandSpec {
    param([object]$Gate)

    if ($Gate -is [string]) {
        return [pscustomobject]@{
            command = "powershell"
            args = @("-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", $Gate)
            raw = $Gate
            timeout_seconds = 0
            idle_timeout_seconds = 0
        }
    }

    if ($null -eq $Gate) {
        throw "Invalid gate: null"
    }

    $command = [string]$Gate.command
    if ([string]::IsNullOrWhiteSpace($command)) {
        throw "Invalid gate: command is empty"
    }

    $args = @()
    if ($null -ne $Gate.args) {
        foreach ($item in @($Gate.args)) {
            $args += [string]$item
        }
    }

    $timeoutSeconds = 0
    if ($null -ne $Gate.PSObject.Properties["timeout_seconds"] -and -not [string]::IsNullOrWhiteSpace([string]$Gate.timeout_seconds)) {
        $timeoutSeconds = [int]$Gate.timeout_seconds
        if ($timeoutSeconds -lt 1) {
            throw "Invalid gate: timeout_seconds must be >= 1"
        }
    }

    $idleTimeoutSeconds = 0
    if ($null -ne $Gate.PSObject.Properties["idle_timeout_seconds"] -and -not [string]::IsNullOrWhiteSpace([string]$Gate.idle_timeout_seconds)) {
        $idleTimeoutSeconds = [int]$Gate.idle_timeout_seconds
        if ($idleTimeoutSeconds -lt 1) {
            throw "Invalid gate: idle_timeout_seconds must be >= 1"
        }
    }

    return [pscustomobject]@{
        command = $command
        args = $args
        raw = $null
        timeout_seconds = $timeoutSeconds
        idle_timeout_seconds = $idleTimeoutSeconds
    }
}

function Format-CommandSpec {
    param([object]$Spec)

    $parts = @($Spec.command)
    if ($null -ne $Spec.args) {
        $parts += @($Spec.args)
    }
    return ($parts -join " ")
}

function Invoke-WatchedProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [string[]]$ArgumentList = @(),
        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,
        [Parameter(Mandatory = $true)]
        [string]$StdoutPath,
        [Parameter(Mandatory = $true)]
        [string]$StderrPath,
        [string]$StdinPath = "",
        [int]$TimeoutSeconds = 0,
        [int]$IdleTimeoutSeconds = 0
    )

    if ($DryRun) {
        return [pscustomobject]@{
            timed_out = $false
            timed_out_reason = $null
            exit_code = 0
        }
    }

    $startArgs = @{
        FilePath = $FilePath
        ArgumentList = @($ArgumentList)
        WorkingDirectory = $WorkingDirectory
        RedirectStandardOutput = $StdoutPath
        RedirectStandardError = $StderrPath
        PassThru = $true
    }

    if (-not [string]::IsNullOrWhiteSpace($StdinPath)) {
        $startArgs.RedirectStandardInput = $StdinPath
    }

    $process = Start-Process @startArgs
    $startedAt = [DateTime]::UtcNow
    $lastActivityAt = $startedAt
    $lastStdoutLength = -1L
    $lastStderrLength = -1L
    $timedOutReason = $null

    while (-not $process.HasExited) {
        Start-Sleep -Seconds 2

        $stdoutLength = 0L
        if (Test-Path -LiteralPath $StdoutPath) {
            $stdoutLength = (Get-Item -LiteralPath $StdoutPath).Length
        }

        $stderrLength = 0L
        if (Test-Path -LiteralPath $StderrPath) {
            $stderrLength = (Get-Item -LiteralPath $StderrPath).Length
        }

        if ($stdoutLength -ne $lastStdoutLength -or $stderrLength -ne $lastStderrLength) {
            $lastStdoutLength = $stdoutLength
            $lastStderrLength = $stderrLength
            $lastActivityAt = [DateTime]::UtcNow
        }

        $now = [DateTime]::UtcNow
        if ($TimeoutSeconds -gt 0 -and ($now - $startedAt).TotalSeconds -ge $TimeoutSeconds) {
            $timedOutReason = "total-timeout"
            break
        }

        if ($IdleTimeoutSeconds -gt 0 -and ($now - $lastActivityAt).TotalSeconds -ge $IdleTimeoutSeconds) {
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
            timed_out = $true
            timed_out_reason = $timedOutReason
            exit_code = $null
        }
    }

    return [pscustomobject]@{
        timed_out = $false
        timed_out_reason = $null
        exit_code = [int]$process.ExitCode
    }
}

function Show-ProcessLogs {
    param(
        [string]$StdoutPath,
        [string]$StderrPath
    )

    $stdout = @()
    if (Test-Path -LiteralPath $StdoutPath) {
        $stdout = @(Get-Content -LiteralPath $StdoutPath)
        if ($stdout.Count -gt 0) {
            $stdout | Out-Host
        }
    }

    $stderr = @()
    if (Test-Path -LiteralPath $StderrPath) {
        $stderr = @(Get-Content -LiteralPath $StderrPath)
        if ($stderr.Count -gt 0) {
            $stderr | Out-Host
        }
    }
}

function Invoke-ProcessCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath,
        [Parameter(Mandatory = $true)]
        [string]$Label,
        [Parameter(Mandatory = $true)]
        [object]$Spec,
        [string]$StdoutPath,
        [string]$StderrPath
    )

    $display = Format-CommandSpec -Spec $Spec
    Write-Host "[$Label] $display" -ForegroundColor DarkCyan
    if ($DryRun) {
        return
    }

    $timeoutSeconds = $GateTimeoutSeconds
    if ($null -ne $Spec.PSObject.Properties["timeout_seconds"] -and [int]$Spec.timeout_seconds -gt 0) {
        $timeoutSeconds = [int]$Spec.timeout_seconds
    }

    $idleTimeoutSeconds = $GateIdleTimeoutSeconds
    if ($null -ne $Spec.PSObject.Properties["idle_timeout_seconds"] -and [int]$Spec.idle_timeout_seconds -gt 0) {
        $idleTimeoutSeconds = [int]$Spec.idle_timeout_seconds
    }

    $result = Invoke-WatchedProcess `
        -FilePath $Spec.command `
        -ArgumentList @($Spec.args) `
        -WorkingDirectory $RootPath `
        -StdoutPath $StdoutPath `
        -StderrPath $StderrPath `
        -TimeoutSeconds $timeoutSeconds `
        -IdleTimeoutSeconds $idleTimeoutSeconds

    Show-ProcessLogs -StdoutPath $StdoutPath -StderrPath $StderrPath

    if ($result.timed_out) {
        throw "Command timed out (reason=$($result.timed_out_reason), timeout=$timeoutSeconds, idle=$idleTimeoutSeconds): $display"
    }

    if ([int]$result.exit_code -ne 0) {
        throw "Command failed (exit=$($result.exit_code)): $display"
    }
}

function Invoke-CodexTask {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath,
        [Parameter(Mandatory = $true)]
        [string]$Prompt,
        [Parameter(Mandatory = $true)]
        [string]$TaskId,
        [Parameter(Mandatory = $true)]
        [string]$LogDirectory
    )

    Write-Host "[codex/$TaskId] start" -ForegroundColor DarkCyan
    if ($DryRun) {
        return
    }

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $promptPath = Join-Path $LogDirectory "$timestamp-$TaskId.prompt.txt"
    $stdoutPath = Join-Path $LogDirectory "$timestamp-$TaskId.stdout.log"
    $stderrPath = Join-Path $LogDirectory "$timestamp-$TaskId.stderr.log"
    Set-Content -LiteralPath $promptPath -Value $Prompt -Encoding UTF8

    $launcher = Resolve-CodexLauncher -Command $CodexCommand
    $args = @(
        @($launcher.PrefixArguments)
        "exec"
        "--cd"
        $RootPath
        "-"
    )

    $result = Invoke-WatchedProcess `
        -FilePath $launcher.FilePath `
        -ArgumentList $args `
        -WorkingDirectory $RootPath `
        -StdoutPath $stdoutPath `
        -StderrPath $stderrPath `
        -StdinPath $promptPath `
        -TimeoutSeconds $CodexTimeoutSeconds `
        -IdleTimeoutSeconds $CodexIdleTimeoutSeconds

    if ($result.timed_out) {
        throw "Codex task timed out (task=$TaskId, reason=$($result.timed_out_reason), timeout=$CodexTimeoutSeconds, idle=$CodexIdleTimeoutSeconds). See: $stderrPath"
    }

    if ([int]$result.exit_code -ne 0) {
        throw "Codex task failed (task=$TaskId, exit=$($result.exit_code)). See: $stderrPath"
    }
}

function New-Checkpoint {
    param([string]$RootPath)
    Push-Location $RootPath
    try {
        return (git rev-parse HEAD).Trim()
    }
    finally {
        Pop-Location
    }
}

function Invoke-Rollback {
    param(
        [string]$RootPath,
        [string]$CheckpointCommit
    )

    if ($NoRollback) {
        Write-Host "Rollback is disabled; keep failed workspace." -ForegroundColor Yellow
        return
    }

    if ($DryRun) {
        Write-Host "[rollback] git reset --hard $CheckpointCommit" -ForegroundColor Yellow
        Write-Host "[rollback] git clean -fd" -ForegroundColor Yellow
        return
    }

    Push-Location $RootPath
    try {
        Write-Host "[rollback] reset to $CheckpointCommit" -ForegroundColor Yellow
        git reset --hard $CheckpointCommit | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "git reset --hard failed."
        }
        git clean -fd | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "git clean -fd failed."
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-AutoCommit {
    param(
        [string]$RootPath,
        [string]$Message
    )

    if ($SkipAutoCommit) {
        Write-Host "Skip auto commit." -ForegroundColor Yellow
        return
    }

    if ($DryRun) {
        Write-Host "[commit] $Message" -ForegroundColor DarkYellow
        return
    }

    Push-Location $RootPath
    try {
        $status = git status --porcelain
        if ([string]::IsNullOrWhiteSpace($status)) {
            Write-Host "No changes to commit for this task." -ForegroundColor Yellow
            return
        }

        git add -A | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "git add failed."
        }
        git commit -m $Message | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "git commit failed."
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-ReleaseValidation {
    param(
        [string]$RootPath,
        [string]$LogDirectory
    )

    if ($SkipReleaseValidation) {
        Write-Host "Skip release validation." -ForegroundColor Yellow
        return
    }

    $buildOut = Join-Path $LogDirectory "release-build.stdout.log"
    $buildErr = Join-Path $LogDirectory "release-build.stderr.log"
    Invoke-ProcessCommand -RootPath $RootPath -Label "release-build" -Spec ([pscustomobject]@{
            command = "dotnet"
            args = @("build", "ClassroomToolkit.sln", "-c", "Release")
        }) -StdoutPath $buildOut -StderrPath $buildErr

    $testOut = Join-Path $LogDirectory "release-test.stdout.log"
    $testErr = Join-Path $LogDirectory "release-test.stderr.log"
    Invoke-ProcessCommand -RootPath $RootPath -Label "release-test" -Spec ([pscustomobject]@{
            command = "dotnet"
            args = @("test", "tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj", "-c", "Release")
        }) -StdoutPath $testOut -StderrPath $testErr
}

function Write-RunSummary {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SummaryPath,
        [Parameter(Mandatory = $true)]
        [hashtable]$Summary
    )

    $directory = Split-Path -Parent $SummaryPath
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $Summary | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $SummaryPath -Encoding UTF8
}

function Get-ErrorClass {
    param([string]$Message)

    if ([string]::IsNullOrWhiteSpace($Message)) {
        return "unknown"
    }
    if ($Message -like "*Another checklist loop is running*") {
        return "lock_conflict"
    }
    if ($Message -like "*Working tree is dirty*") {
        return "dirty_worktree"
    }
    if ($Message -like "*Task file not found:*" -or $Message -like "*StartFromTaskId not found:*" -or $Message -like "*No tasks in descriptor file.*" -or $Message -like "*TaskFile is required.*") {
        return "input_validation_failure"
    }
    if ($Message -like "*Manual validation is required*" -or $Message -like "*SkipManualValidation requires ForceReleaseWithoutManual.*") {
        return "manual_gate_failure"
    }
    if ($Message -like "*dotnet build ClassroomToolkit.sln -c Release*" -or $Message -like "*dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release*") {
        return "release_validation_failure"
    }
    if ($Message -like "*Codex task timed out*") {
        return "codex_timeout"
    }
    if ($Message -like "*Command timed out*") {
        return "gate_timeout"
    }
    if ($Message -like "*Codex task failed*") {
        return "codex_failure"
    }
    if ($Message -like "*Missing command:*" -or $Message -like "*Invalid gate:*") {
        return "preflight_failure"
    }
    if ($Message -like "*Command failed*") {
        return "gate_failure"
    }
    if ($Message -like "*git add failed.*" -or $Message -like "*git commit failed.*") {
        return "vcs_failure"
    }
    if ($Message -like "*Task failed after max attempts:*") {
        return "task_retry_exhausted"
    }
    if ($Message -like "*git reset --hard failed.*" -or $Message -like "*git clean -fd failed.*") {
        return "rollback_failure"
    }
    return "unknown"
}

function Test-GatePreflight {
    param(
        [string]$TaskId,
        [object[]]$GateSpecs
    )

    for ($i = 0; $i -lt $GateSpecs.Count; $i++) {
        $spec = $GateSpecs[$i]
        if ([string]::IsNullOrWhiteSpace([string]$spec.command)) {
            throw "Invalid gate: task=$TaskId index=$($i + 1) command empty."
        }
        if (-not (Get-Command $spec.command -ErrorAction SilentlyContinue)) {
            throw "Missing command: $($spec.command) for task=$TaskId gate=$($i + 1)."
        }

        $args = @($spec.args)
        for ($j = 0; $j -lt $args.Count; $j++) {
            if ([string]::IsNullOrWhiteSpace([string]$args[$j])) {
                throw "Invalid gate: task=$TaskId gate=$($i + 1) contains empty argument at index $j."
            }
        }

        $commandLower = [string]$spec.command
        $commandLower = $commandLower.ToLowerInvariant()
        if ($commandLower -in @("powershell", "powershell.exe", "pwsh", "pwsh.exe")) {
            for ($j = 0; $j -lt $args.Count; $j++) {
                $arg = [string]$args[$j]
                if ($arg -in @("-Command", "-c")) {
                    if ($j + 1 -ge $args.Count -or [string]::IsNullOrWhiteSpace([string]$args[$j + 1])) {
                        throw "Invalid gate: task=$TaskId gate=$($i + 1) has empty PowerShell command body."
                    }
                }
            }
        }
    }
}

function Test-TaskDescriptor {
    param([object[]]$Tasks)

    $seen = @{}
    for ($i = 0; $i -lt $Tasks.Count; $i++) {
        $task = $Tasks[$i]
        $index = $i + 1
        if ($null -eq $task) {
            throw "Invalid task descriptor: task index $index is null."
        }

        $taskId = [string]$task.id
        if ([string]::IsNullOrWhiteSpace($taskId)) {
            throw "Invalid task descriptor: task index $index missing id."
        }

        if ($seen.ContainsKey($taskId)) {
            throw "Invalid task descriptor: duplicate task id '$taskId'."
        }
        $seen[$taskId] = $true

        if ([string]::IsNullOrWhiteSpace([string]$task.title)) {
            throw "Invalid task descriptor: task '$taskId' missing title."
        }
        if ([string]::IsNullOrWhiteSpace([string]$task.prompt)) {
            throw "Invalid task descriptor: task '$taskId' missing prompt."
        }
        if (-not $SkipAutoCommit -and [string]::IsNullOrWhiteSpace([string]$task.commit_message)) {
            throw "Invalid task descriptor: task '$taskId' missing commit_message."
        }
    }
}

Write-Phase "Environment check"
Assert-Command -Name git -Hint "Install Git first."
Assert-Command -Name dotnet -Hint "Install .NET SDK first."
Assert-Command -Name powershell -Hint "PowerShell is required."
Assert-Command -Name $CodexCommand -Hint "Ensure Codex CLI is installed."

if ($MaxAttemptsPerTask -lt 1) {
    throw "MaxAttemptsPerTask must be >= 1."
}
if ($LockStaleAfterMinutes -lt 1) {
    throw "LockStaleAfterMinutes must be >= 1."
}
if ($CodexTimeoutSeconds -lt 1) {
    throw "CodexTimeoutSeconds must be >= 1."
}
if ($CodexIdleTimeoutSeconds -lt 1) {
    throw "CodexIdleTimeoutSeconds must be >= 1."
}
if ($GateTimeoutSeconds -lt 1) {
    throw "GateTimeoutSeconds must be >= 1."
}
if ($GateIdleTimeoutSeconds -lt 1) {
    throw "GateIdleTimeoutSeconds must be >= 1."
}
if ([string]::IsNullOrWhiteSpace($TaskFile)) {
    throw "TaskFile is required. Pass -TaskFile <path-to-tasks.json>."
}

$repoPath = Get-RepoRootPath -Root $RepoRoot
$taskFilePath = Resolve-InputPath -RootPath $repoPath -InputPath $TaskFile
if (-not (Test-Path -LiteralPath $taskFilePath)) {
    throw "Task file not found: $taskFilePath"
}

if (-not $AllowDirtyWorkingTree -and -not (Test-CleanWorkingTree -RootPath $repoPath)) {
    throw "Working tree is dirty. Clean it first or pass -AllowDirtyWorkingTree."
}

$taskDoc = (Get-Content -LiteralPath $taskFilePath -Raw) | ConvertFrom-Json
$tasks = @($taskDoc.tasks)
if ($tasks.Count -eq 0) {
    throw "No tasks in descriptor file."
}
Test-TaskDescriptor -Tasks $tasks

$startIndex = 0
if (-not [string]::IsNullOrWhiteSpace($StartFromTaskId)) {
    $matched = $false
    for ($i = 0; $i -lt $tasks.Count; $i++) {
        if ([string]$tasks[$i].id -eq $StartFromTaskId) {
            $startIndex = $i
            $matched = $true
            break
        }
    }
    if (-not $matched) {
        throw "StartFromTaskId not found: $StartFromTaskId"
    }
}

$logDirectory = Join-Path $repoPath ".codex/logs/checklist-loop"
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null

$runId = "{0}-{1}" -f (Get-Date -Format "yyyyMMdd-HHmmss-fff"), ([guid]::NewGuid().ToString("N").Substring(0, 6))
$summaryPath = Join-Path $logDirectory ("run-{0}.summary.json" -f $runId)
$lockPath = Resolve-InputPath -RootPath $repoPath -InputPath $LockFile

$checkpoint = New-Checkpoint -RootPath $repoPath
Write-Host "Initial checkpoint: $checkpoint" -ForegroundColor Gray

if (-not $DryRun) {
    Acquire-RunLock -Path $lockPath -RunId $runId -RepoPath $repoPath -StaleAfterMinutes $LockStaleAfterMinutes
}

$runSummary = [ordered]@{
    run_id = $runId
    started_at = (Get-Date).ToString("o")
    finished_at = $null
    status = "running"
    error_class = $null
    dry_run = [bool]$DryRun
    repo_root = $repoPath
    task_file = $taskFilePath
    log_directory = $logDirectory
    summary_path = $summaryPath
    options = [ordered]@{
        start_from_task_id = $StartFromTaskId
        max_attempts_per_task = $MaxAttemptsPerTask
        lock_file = $LockFile
        lock_stale_after_minutes = $LockStaleAfterMinutes
        codex_timeout_seconds = $CodexTimeoutSeconds
        codex_idle_timeout_seconds = $CodexIdleTimeoutSeconds
        gate_timeout_seconds = $GateTimeoutSeconds
        gate_idle_timeout_seconds = $GateIdleTimeoutSeconds
        disable_gate_preflight = [bool]$DisableGatePreflight
        skip_manual_validation = [bool]$SkipManualValidation
        force_release_without_manual = [bool]$ForceReleaseWithoutManual
        skip_release_validation = [bool]$SkipReleaseValidation
        skip_auto_commit = [bool]$SkipAutoCommit
        no_rollback = [bool]$NoRollback
        allow_dirty_working_tree = [bool]$AllowDirtyWorkingTree
    }
    initial_checkpoint = $checkpoint
    final_checkpoint = $null
    failed_task_id = $null
    failed_gate_command = $null
    failed_timeout_reason = $null
    rollback = [ordered]@{
        attempted = $false
        skipped = $false
        target_checkpoint = $null
    }
    release_validation = [ordered]@{
        executed = $false
    }
    tasks = @()
    error_message = $null
}

try {
    for ($i = 0; $i -lt $tasks.Count; $i++) {
        $task = $tasks[$i]
        $taskId = [string]$task.id
        $title = [string]$task.title
        $commitMessage = [string]$task.commit_message
        $prompt = [string]$task.prompt
        $gateSpecs = @()
        foreach ($gate in @($task.gates)) {
            $gateSpecs += (ConvertTo-CommandSpec -Gate $gate)
        }

        $taskSummary = [ordered]@{
            id = $taskId
            title = $title
            status = "pending"
            attempts = 0
            started_at = $null
            finished_at = $null
            commit_message = $commitMessage
            gates = @()
            error_message = $null
        }
        $runSummary.tasks += $taskSummary

        if ($i -lt $startIndex) {
            $taskSummary.status = "skipped_by_resume"
            continue
        }

        if (-not $DisableGatePreflight) {
            Test-GatePreflight -TaskId $taskId -GateSpecs $gateSpecs
        }

        $taskSucceeded = $false
        for ($attempt = 1; $attempt -le $MaxAttemptsPerTask; $attempt++) {
            $taskSummary.attempts = $attempt
            $taskSummary.status = "running"
            $taskSummary.started_at = (Get-Date).ToString("o")
            $taskSummary.error_message = $null
            $taskSummary.gates = @()

            Write-Phase ("Task {0}/{1}: {2} (attempt {3}/{4})" -f ($i + 1), $tasks.Count, $title, $attempt, $MaxAttemptsPerTask)

            try {
                Invoke-CodexTask -RootPath $repoPath -Prompt $prompt -TaskId $taskId -LogDirectory $logDirectory

                for ($g = 0; $g -lt $gateSpecs.Count; $g++) {
                    $spec = $gateSpecs[$g]
                    $gateSummary = [ordered]@{
                        index = ($g + 1)
                        command = (Format-CommandSpec -Spec $spec)
                        stdout_path = Join-Path $logDirectory ("{0}-{1:D2}-{2}.stdout.log" -f $taskId, ($g + 1), (Get-Date -Format "yyyyMMdd-HHmmss"))
                        stderr_path = Join-Path $logDirectory ("{0}-{1:D2}-{2}.stderr.log" -f $taskId, ($g + 1), (Get-Date -Format "yyyyMMdd-HHmmss"))
                        timeout_seconds = if ([int]$spec.timeout_seconds -gt 0) { [int]$spec.timeout_seconds } else { $GateTimeoutSeconds }
                        idle_timeout_seconds = if ([int]$spec.idle_timeout_seconds -gt 0) { [int]$spec.idle_timeout_seconds } else { $GateIdleTimeoutSeconds }
                        status = "running"
                        timed_out = $false
                        timeout_reason = $null
                        error_message = $null
                    }
                    $taskSummary.gates += $gateSummary

                    Invoke-ProcessCommand -RootPath $repoPath -Label "$taskId/gate-$($g + 1)" -Spec $spec -StdoutPath $gateSummary.stdout_path -StderrPath $gateSummary.stderr_path
                    $gateSummary.status = "passed"
                }

                Invoke-AutoCommit -RootPath $repoPath -Message $commitMessage
                $checkpoint = New-Checkpoint -RootPath $repoPath
                $taskSummary.status = "completed"
                $taskSummary.finished_at = (Get-Date).ToString("o")
                Write-Host "Task complete; checkpoint: $checkpoint" -ForegroundColor Green
                $taskSucceeded = $true
                break
            }
            catch {
                $taskSummary.status = "failed"
                $taskSummary.finished_at = (Get-Date).ToString("o")
                $taskSummary.error_message = $_.Exception.Message
                $failedGate = $taskSummary.gates | Where-Object { $_.status -eq "running" } | Select-Object -First 1
                if ($null -ne $failedGate) {
                    $failedGate.status = "failed"
                    $failedGate.error_message = $_.Exception.Message
                    if ($_.Exception.Message -like "*timed out*") {
                        $failedGate.timed_out = $true
                        if ($_.Exception.Message -like "*idle-timeout*") {
                            $failedGate.timeout_reason = "idle-timeout"
                        }
                        else {
                            $failedGate.timeout_reason = "total-timeout"
                        }
                    }
                }
                $errorClass = Get-ErrorClass -Message $_.Exception.Message
                $runSummary.error_class = $errorClass

                if ($attempt -lt $MaxAttemptsPerTask -and $errorClass -in @("codex_failure", "gate_failure", "codex_timeout", "gate_timeout")) {
                    Write-Host "Task failed, retrying: $($_.Exception.Message)" -ForegroundColor Yellow
                    continue
                }

                throw
            }
        }

        if (-not $taskSucceeded) {
            throw "Task failed after max attempts: $taskId"
        }
    }

    Write-Phase "Manual validation gate"
    if ($SkipManualValidation) {
        if (-not $ForceReleaseWithoutManual) {
            throw "SkipManualValidation requires ForceReleaseWithoutManual."
        }
        Write-Host "Manual gate skipped by explicit force flag." -ForegroundColor Yellow
    }
    else {
        throw "Manual validation is required by default. Use -SkipManualValidation -ForceReleaseWithoutManual for unattended mode."
    }

    Write-Phase "Release validation gate"
    $runSummary.release_validation.executed = $true
    Invoke-ReleaseValidation -RootPath $repoPath -LogDirectory $logDirectory

    $runSummary.status = "completed"
    $runSummary.final_checkpoint = $checkpoint
    Write-Host "Unattended checklist loop finished." -ForegroundColor Green
}
catch {
    $runSummary.status = "failed"
    $runSummary.error_message = $_.Exception.Message
    if ($null -eq $runSummary.error_class) {
        $runSummary.error_class = Get-ErrorClass -Message $_.Exception.Message
    }
    if ([string]::IsNullOrWhiteSpace([string]$runSummary.failed_timeout_reason) -and $_.Exception.Message -like "*timed out*") {
        if ($_.Exception.Message -like "*idle-timeout*") {
            $runSummary.failed_timeout_reason = "idle-timeout"
        }
        else {
            $runSummary.failed_timeout_reason = "total-timeout"
        }
    }
    $runSummary.final_checkpoint = $checkpoint

    $failedTask = $runSummary.tasks | Where-Object { $_.status -eq "failed" -or $_.status -eq "running" } | Select-Object -First 1
    if ($null -ne $failedTask) {
        $runSummary.failed_task_id = $failedTask.id
        $failedGate = $failedTask.gates | Where-Object { $_.status -eq "failed" -or $_.status -eq "running" } | Select-Object -First 1
        if ($null -ne $failedGate) {
            $runSummary.failed_gate_command = $failedGate.command
            if ($null -ne $failedGate.PSObject.Properties["timeout_reason"] -and -not [string]::IsNullOrWhiteSpace([string]$failedGate.timeout_reason)) {
                $runSummary.failed_timeout_reason = [string]$failedGate.timeout_reason
            }
        }
    }

    Write-Host "Execution failed: $($_.Exception.Message)" -ForegroundColor Red
    $runSummary.rollback.attempted = $true
    $runSummary.rollback.target_checkpoint = $checkpoint
    $runSummary.rollback.skipped = [bool]$NoRollback
    Invoke-Rollback -RootPath $repoPath -CheckpointCommit $checkpoint
    throw
}
finally {
    if ($null -eq $runSummary.final_checkpoint) {
        $runSummary.final_checkpoint = $checkpoint
    }
    $runSummary.finished_at = (Get-Date).ToString("o")
    Write-RunSummary -SummaryPath $summaryPath -Summary $runSummary
    if (-not $DryRun) {
        Release-RunLock -Path $lockPath
    }
    Write-Host "Run summary: $summaryPath" -ForegroundColor Gray
}
