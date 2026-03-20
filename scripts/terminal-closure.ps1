param(
    [string]$RepoRoot = ".",
    [string]$TaskFile = "docs/superpowers/plans/2026-03-20-terminal-architecture-closure.tasks.json",
    [string]$CodexCommand = "codex",
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

function Invoke-ShellCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath,
        [Parameter(Mandatory = $true)]
        [string]$CommandText,
        [Parameter(Mandatory = $true)]
        [string]$Label,
        [Parameter(Mandatory = $true)]
        [string]$LogPath
    )

    Write-Host "[$Label] $CommandText" -ForegroundColor DarkCyan
    if ($DryRun) {
        return
    }

    Push-Location $RootPath
    try {
        $output = & powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -Command $CommandText 2>&1
        $output | Tee-Object -FilePath $LogPath | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed (exit=$LASTEXITCODE): $CommandText"
        }
    }
    finally {
        Pop-Location
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

    $process = Start-Process -FilePath $launcher.FilePath `
        -ArgumentList $args `
        -WorkingDirectory $RootPath `
        -RedirectStandardInput $promptPath `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -PassThru `
        -Wait

    if ($process.ExitCode -ne 0) {
        throw "Codex task failed (task=$TaskId, exit=$($process.ExitCode)). See: $stderrPath"
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

    $buildLog = Join-Path $LogDirectory "release-build.log"
    $testLog = Join-Path $LogDirectory "release-test.log"
    Invoke-ShellCommand -RootPath $RootPath -CommandText "dotnet build ClassroomToolkit.sln -c Release" -Label "release-build" -LogPath $buildLog
    Invoke-ShellCommand -RootPath $RootPath -CommandText "dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release" -Label "release-test" -LogPath $testLog
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

    $Summary | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $SummaryPath -Encoding UTF8
}

Write-Phase "Environment check"
Assert-Command -Name git -Hint "Install Git first."
Assert-Command -Name dotnet -Hint "Install .NET SDK first."
Assert-Command -Name powershell -Hint "PowerShell is required."
Assert-Command -Name $CodexCommand -Hint "Ensure Codex CLI is installed."

$repoPath = Get-RepoRootPath -Root $RepoRoot
$taskFilePath = Join-Path $repoPath $TaskFile

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

$logDirectory = Join-Path $repoPath ".codex/logs/terminal-closure"
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null

$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$summaryPath = Join-Path $logDirectory ("run-{0}.summary.json" -f $runId)

$checkpoint = New-Checkpoint -RootPath $repoPath
Write-Host "Initial checkpoint: $checkpoint" -ForegroundColor Gray

$runSummary = [ordered]@{
    run_id = $runId
    started_at = (Get-Date).ToString("o")
    finished_at = $null
    status = "running"
    dry_run = [bool]$DryRun
    repo_root = $repoPath
    task_file = $taskFilePath
    log_directory = $logDirectory
    summary_path = $summaryPath
    options = [ordered]@{
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
        $gates = @($task.gates)
        $prompt = [string]$task.prompt
        $taskSummary = [ordered]@{
            id = $taskId
            title = $title
            status = "running"
            started_at = (Get-Date).ToString("o")
            finished_at = $null
            commit_message = $commitMessage
            gates = @()
            error_message = $null
        }
        $runSummary.tasks += $taskSummary

        Write-Phase ("Task {0}/{1}: {2}" -f ($i + 1), $tasks.Count, $title)
        Invoke-CodexTask -RootPath $repoPath -Prompt $prompt -TaskId $taskId -LogDirectory $logDirectory

        for ($g = 0; $g -lt $gates.Count; $g++) {
            $gateCommand = [string]$gates[$g]
            $gateLog = Join-Path $logDirectory ("{0}-{1:D2}-{2}.log" -f $taskId, ($g + 1), (Get-Date -Format "yyyyMMdd-HHmmss"))
            $taskSummary.gates += [ordered]@{
                index = ($g + 1)
                command = $gateCommand
                log_path = $gateLog
                status = "running"
                error_message = $null
            }
            Invoke-ShellCommand -RootPath $repoPath -CommandText $gateCommand -Label "$taskId/gate-$($g + 1)" -LogPath $gateLog
            $taskSummary.gates[$g].status = "passed"
        }

        Invoke-AutoCommit -RootPath $repoPath -Message $commitMessage
        $checkpoint = New-Checkpoint -RootPath $repoPath
        $taskSummary.status = "completed"
        $taskSummary.finished_at = (Get-Date).ToString("o")
        Write-Host "Task complete; checkpoint: $checkpoint" -ForegroundColor Green
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
    Write-Host "Unattended terminal-architecture closure finished." -ForegroundColor Green
}
catch {
    $runSummary.status = "failed"
    $runSummary.error_message = $_.Exception.Message
    $runSummary.final_checkpoint = $checkpoint

    $failedTask = $runSummary.tasks | Where-Object { $_.status -eq "running" } | Select-Object -First 1
    if ($null -ne $failedTask) {
        $failedTask.status = "failed"
        $failedTask.finished_at = (Get-Date).ToString("o")
        $failedTask.error_message = $_.Exception.Message
        $runSummary.failed_task_id = $failedTask.id

        $failedGate = $failedTask.gates | Where-Object { $_.status -eq "running" } | Select-Object -First 1
        if ($null -ne $failedGate) {
            $failedGate.status = "failed"
            $failedGate.error_message = $_.Exception.Message
            $runSummary.failed_gate_command = $failedGate.command
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
    Write-Host "Run summary: $summaryPath" -ForegroundColor Gray
}
