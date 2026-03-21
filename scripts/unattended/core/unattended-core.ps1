Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function UCore-ReadJsonFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [switch]$ReturnNullIfMissing,
        [int]$MaxAttempts = 1,
        [int]$RetryDelayMilliseconds = 0
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        if ($ReturnNullIfMissing) {
            return $null
        }
        throw "JSON file not found: $Path"
    }

    if ($MaxAttempts -lt 1) {
        $MaxAttempts = 1
    }

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
        }
        catch {
            if ($attempt -eq $MaxAttempts) {
                throw
            }

            if ($RetryDelayMilliseconds -gt 0) {
                Start-Sleep -Milliseconds $RetryDelayMilliseconds
            }
        }
    }

    return $null
}

function UCore-TestProcessAlive {
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

function UCore-ResolveLauncher {
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

function UCore-InvokeWatchedProcess {
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
            process_id = $process.Id
        }
    }

    return [pscustomobject]@{
        timed_out = $false
        timed_out_reason = $null
        exit_code = [int]$process.ExitCode
        process_id = $process.Id
    }
}

function UCore-ParseObservableReport {
    param(
        [Parameter(Mandatory = $true)]
        [string]$StdoutPath
    )

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

function UCore-WriteFailureBrief {
    param(
        [string]$TaskId = "",
        [string]$FailedGate = "unknown",
        [string]$FailureClass = "unknown",
        [string]$StdoutLogPath = "",
        [string]$StderrLogPath = "",
        [string]$RollbackPoint = "",
        [string]$NextAction = ""
    )

    Write-Host "FAILURE_BRIEF"
    Write-Host "failed_task: $(if ([string]::IsNullOrWhiteSpace($TaskId)) { 'none' } else { $TaskId })"
    Write-Host "failed_gate: $FailedGate"
    Write-Host "failure_class: $FailureClass"
    Write-Host "stdout_log: $(if ([string]::IsNullOrWhiteSpace($StdoutLogPath)) { 'none' } else { $StdoutLogPath })"
    Write-Host "stderr_log: $(if ([string]::IsNullOrWhiteSpace($StderrLogPath)) { 'none' } else { $StderrLogPath })"
    Write-Host "rollback_point: $(if ([string]::IsNullOrWhiteSpace($RollbackPoint)) { 'none' } else { $RollbackPoint })"
    Write-Host "next_action: $(if ([string]::IsNullOrWhiteSpace($NextAction)) { 'none' } else { $NextAction })"
}

function UCore-AcquireLock {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [hashtable]$Record,
        [int]$StaleAfterMinutes = 30,
        [ValidateSet("block", "takeover")]
        [string]$StalePolicy = "block",
        [string]$ConflictMessagePrefix = "Another unattended loop is running"
    )

    $existing = UCore-ReadJsonFile -Path $Path -ReturnNullIfMissing
    if ($null -ne $existing) {
        $ownerPid = 0
        if ($null -ne $existing.PSObject.Properties["pid"]) {
            $ownerPid = [int]$existing.pid
        }

        $ownerAlive = UCore-TestProcessAlive -ProcessId $ownerPid
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
            throw "$ConflictMessagePrefix (PID $ownerPid). Lock file: $Path"
        }

        if ($StalePolicy -eq "block" -and ($isStale -or -not $ownerAlive) -and $ownerPid -ne $PID) {
            throw "Stale or ambiguous lock ownership detected in $Path. Review and remove the lock manually."
        }
    }

    $lockDir = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($lockDir) -and -not (Test-Path -LiteralPath $lockDir)) {
        New-Item -ItemType Directory -Force -Path $lockDir | Out-Null
    }

    $lockRecord = [ordered]@{}
    foreach ($key in $Record.Keys) {
        $lockRecord[$key] = $Record[$key]
    }
    if (-not $lockRecord.Contains("pid")) {
        $lockRecord["pid"] = $PID
    }
    if (-not $lockRecord.Contains("started_at")) {
        $lockRecord["started_at"] = [DateTime]::UtcNow.ToString("o")
    }

    $lockRecord | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $Path -Encoding UTF8 -NoNewline
}

function UCore-ReleaseLock {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $existing = UCore-ReadJsonFile -Path $Path -ReturnNullIfMissing
    if ($null -eq $existing) {
        Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
        return
    }

    $ownerPid = 0
    if ($null -ne $existing.PSObject.Properties["pid"]) {
        $ownerPid = [int]$existing.pid
    }
    if ($ownerPid -eq $PID) {
        Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
    }
}

function UCore-TestTaskBudgetExceeded {
    param(
        [int]$ExecFailures,
        [int]$MaxExecFailures,
        [int]$NoProgress,
        [int]$MaxNoProgress
    )

    return ($ExecFailures -ge $MaxExecFailures -or $NoProgress -ge $MaxNoProgress)
}

function UCore-InvokeStateBlock {
    param(
        [Parameter(Mandatory = $true)]
        [string]$StateUpdaterScript,
        [Parameter(Mandatory = $true)]
        [string]$StateFile,
        [Parameter(Mandatory = $true)]
        [string]$TaskId,
        [Parameter(Mandatory = $true)]
        [string]$Summary,
        [Parameter(Mandatory = $true)]
        [string]$Reason,
        [hashtable]$ExtraArgs
    )

    $arguments = @(
        "-File", $StateUpdaterScript,
        "-Action", "block",
        "-StateFile", $StateFile,
        "-TaskId", $TaskId,
        "-Summary", $Summary,
        "-Reason", $Reason
    )

    if ($null -ne $ExtraArgs) {
        foreach ($key in $ExtraArgs.Keys) {
            $value = $ExtraArgs[$key]
            if ($null -eq $value -or [string]::IsNullOrWhiteSpace([string]$value)) {
                continue
            }

            $arguments += @("-$key", [string]$value)
        }
    }

    & powershell @arguments | Out-Null
}
