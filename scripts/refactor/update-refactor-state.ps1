param(
    [ValidateSet("init", "start", "complete", "block", "defer", "note", "unblock")]
    [string]$Action,
    [string]$StateFile = ".codex/refactor-state.json",
    [string]$TaskId,
    [string]$Summary = "",
    [string]$Reason = "",
    [string]$Mode = "",
    [string]$ModeFamily = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-ModeDefaults {
    param(
        [string]$Path,
        [string]$RequestedMode,
        [string]$RequestedModeFamily
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedMode)) {
        return [pscustomobject]@{
            mode = $RequestedMode
            mode_family = if ([string]::IsNullOrWhiteSpace($RequestedModeFamily)) { $RequestedMode } else { $RequestedModeFamily }
        }
    }

    if ($Path -like "*ui-window-system-state.json") {
        return [pscustomobject]@{
            mode = "ui-window-system"
            mode_family = "ui-overhaul"
        }
    }

    return [pscustomobject]@{
        mode = "autonomous-refactor-loop"
        mode_family = "architecture-refactor"
    }
}

function New-State {
    param(
        [string]$Path,
        [string]$RequestedMode,
        [string]$RequestedModeFamily
    )

    $defaults = Get-ModeDefaults -Path $Path -RequestedMode $RequestedMode -RequestedModeFamily $RequestedModeFamily
    return [ordered]@{
        project = "ClassroomToolkit"
        mode = $defaults.mode
        mode_family = $defaults.mode_family
        updated_at = [DateTime]::UtcNow.ToString("o")
        current_task = $null
        tasks = [ordered]@{}
        blocked = @()
        history = @()
        last_summary = ""
    }
}

function Read-State {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return New-State -Path $Path -RequestedMode $Mode -RequestedModeFamily $ModeFamily
    }

    $state = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($null -eq $state.PSObject.Properties["mode"] -or [string]::IsNullOrWhiteSpace([string]$state.mode)) {
        $defaults = Get-ModeDefaults -Path $Path -RequestedMode $Mode -RequestedModeFamily $ModeFamily
        $state | Add-Member -NotePropertyName "mode" -NotePropertyValue $defaults.mode -Force
        $state | Add-Member -NotePropertyName "mode_family" -NotePropertyValue $defaults.mode_family -Force
    }
    elseif ($null -eq $state.PSObject.Properties["mode_family"]) {
        $defaults = Get-ModeDefaults -Path $Path -RequestedMode $Mode -RequestedModeFamily $ModeFamily
        $state | Add-Member -NotePropertyName "mode_family" -NotePropertyValue $defaults.mode_family -Force
    }

    if ($null -eq $state.PSObject.Properties["tasks"]) {
        $state | Add-Member -NotePropertyName "tasks" -NotePropertyValue ([ordered]@{}) -Force
    }
    if ($null -eq $state.PSObject.Properties["blocked"]) {
        $state | Add-Member -NotePropertyName "blocked" -NotePropertyValue @() -Force
    }
    if ($null -eq $state.PSObject.Properties["history"]) {
        $state | Add-Member -NotePropertyName "history" -NotePropertyValue @() -Force
    }
    if ($null -eq $state.PSObject.Properties["last_summary"]) {
        $state | Add-Member -NotePropertyName "last_summary" -NotePropertyValue "" -Force
    }

    return $state
}

function Ensure-TaskState {
    param(
        [object]$State,
        [string]$Id
    )

    if ([string]::IsNullOrWhiteSpace($Id)) {
        throw "TaskId is required for action '$Action'."
    }

    if ($null -eq $State.tasks.PSObject.Properties[$Id]) {
        $State.tasks | Add-Member -NotePropertyName $Id -NotePropertyValue ([pscustomobject]@{
                status = "pending"
                attempts = 0
                last_summary = ""
            })
    }

    return $State.tasks.PSObject.Properties[$Id].Value
}

$state = if ($Action -eq "init") { New-State -Path $StateFile -RequestedMode $Mode -RequestedModeFamily $ModeFamily } else { Read-State -Path $StateFile }

switch ($Action) {
    "init" {
    }
    "start" {
        $taskState = Ensure-TaskState -State $state -Id $TaskId
        $taskState.status = "in_progress"
        $taskState.attempts = [int]$taskState.attempts + 1
        if ($Summary) {
            $taskState.last_summary = $Summary
        }
        $state.current_task = $TaskId
    }
    "complete" {
        $taskState = Ensure-TaskState -State $state -Id $TaskId
        $taskState.status = "completed"
        $taskState.last_summary = $Summary
        $state.current_task = $null
        $state.last_summary = $Summary
    }
    "block" {
        $taskState = Ensure-TaskState -State $state -Id $TaskId
        $taskState.status = "blocked"
        $taskState.last_summary = $Summary
        $state.current_task = $null
        $state.blocked += [pscustomobject]@{
            task_id = $TaskId
            reason = $Reason
            recorded_at = [DateTime]::UtcNow.ToString("o")
        }
        $state.last_summary = $Summary
        if ($Reason -like "visual_gate:*") {
            $gateId = $Reason.Substring("visual_gate:".Length)
            if ($null -eq $state.PSObject.Properties["current_manual_gate"]) {
                $state | Add-Member -NotePropertyName "current_manual_gate" -NotePropertyValue $gateId -Force
            }
            else {
                $state.current_manual_gate = $gateId
            }
        }
    }
    "defer" {
        $taskState = Ensure-TaskState -State $state -Id $TaskId
        $taskState.status = "deferred"
        $taskState.last_summary = $Summary
        $state.current_task = $null
        $state.last_summary = $Summary
    }
    "note" {
        if ($Summary) {
            $state.last_summary = $Summary
        }
    }
    "unblock" {
        $taskState = Ensure-TaskState -State $state -Id $TaskId
        $taskState.status = "pending"
        if ($Summary) {
            $taskState.last_summary = $Summary
            $state.last_summary = $Summary
        }
        $state.current_task = $null
        $state.blocked = @($state.blocked | Where-Object { [string]$_.task_id -ne $TaskId })
        if ($null -ne $state.PSObject.Properties["current_manual_gate"]) {
            $state.current_manual_gate = $null
        }
    }
}

$state.updated_at = [DateTime]::UtcNow.ToString("o")
$state.history += [pscustomobject]@{
    action = $Action
    task_id = $TaskId
    summary = $Summary
    reason = $Reason
    recorded_at = [DateTime]::UtcNow.ToString("o")
}

$directory = Split-Path -Parent $StateFile
if ($directory -and -not (Test-Path -LiteralPath $directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$state | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $StateFile -Encoding UTF8

$state

