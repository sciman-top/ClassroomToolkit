param(
    [string]$TaskFile = "docs/refactor/tasks.json",
    [string]$StateFile = ".codex/refactor-state.json",
    [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Read-JsonFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "JSON file not found: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Get-TaskState {
    param(
        [object]$State,
        [string]$TaskId
    )

    $taskState = $State.tasks.PSObject.Properties[$TaskId]
    if ($null -eq $taskState) {
        return [pscustomobject]@{
            status = "pending"
            attempts = 0
            last_summary = ""
        }
    }

    return $taskState.Value
}

function Get-TaskStateStatus {
    param(
        [object]$State,
        [string]$TaskId
    )

    return [string](Get-TaskState -State $State -TaskId $TaskId).status
}

function Test-IsTerminalStatus {
    param([string]$Status)

    return $Status -in @("completed", "omitted", "deferred")
}

$tasksDoc = Read-JsonFile -Path $TaskFile
$state = Read-JsonFile -Path $StateFile

$taskIndex = @{}
foreach ($task in $tasksDoc.tasks) {
    $taskIndex[[string]$task.id] = $task
}

function New-SelectionObject {
    param(
        [object]$Task,
        [bool]$Resume = $false
    )

    $manualGate = $null
    if ($null -ne $Task.PSObject.Properties["manual_gate"]) {
        $manualGate = $Task.manual_gate
    }

    $docSync = @()
    if ($null -ne $Task.PSObject.Properties["doc_sync"]) {
        $docSync = @($Task.doc_sync)
    }

    $behaviorInvariants = @()
    if ($null -ne $Task.PSObject.Properties["behavior_invariants"]) {
        $behaviorInvariants = @($Task.behavior_invariants)
    }

    $blockedByVisualReview = $false
    if ($null -ne $Task.PSObject.Properties["blocked_by_visual_review"]) {
        $blockedByVisualReview = [bool]$Task.blocked_by_visual_review
    }

    return [pscustomobject]@{
        id = $Task.id
        title = $Task.title
        priority = [int]$Task.priority
        order = [int]$Task.order
        summary = $Task.summary
        depends_on = @($Task.depends_on)
        file_hints = @($Task.file_hints)
        done_when = @($Task.done_when)
        verify = $Task.verify
        doc_sync = $docSync
        manual_gate = $manualGate
        behavior_invariants = $behaviorInvariants
        blocked_by_visual_review = $blockedByVisualReview
        resume = $Resume
    }
}

$currentTaskId = $null
if ($null -ne $state.PSObject.Properties["current_task"] -and -not [string]::IsNullOrWhiteSpace([string]$state.current_task)) {
    $currentTaskId = [string]$state.current_task
}

if ($null -ne $currentTaskId -and $taskIndex.ContainsKey($currentTaskId)) {
    $currentTask = $taskIndex[$currentTaskId]
    $currentTaskState = Get-TaskState -State $state -TaskId $currentTaskId
    if ($currentTaskState.status -eq "in_progress") {
        $result = New-SelectionObject -Task $currentTask -Resume $true

        if ($AsJson) {
            $result | ConvertTo-Json -Depth 100
        }
        else {
            $result
        }

        return
    }
}

$currentManualGate = $null
if ($null -ne $state.PSObject.Properties["current_manual_gate"] -and -not [string]::IsNullOrWhiteSpace([string]$state.current_manual_gate)) {
    $currentManualGate = [string]$state.current_manual_gate
}

if (-not [string]::IsNullOrWhiteSpace($currentManualGate)) {
    $result = [pscustomobject]@{
        status = "blocked"
        message = "Current manual gate blocks automatic selection."
        remaining = @(
            [pscustomobject]@{
                id = $currentManualGate
                status = "manual_gate"
            }
        )
    }

    if ($AsJson) {
        $result | ConvertTo-Json -Depth 100
    }
    else {
        $result
    }

    return
}

foreach ($task in $tasksDoc.tasks) {
    $taskState = Get-TaskState -State $state -TaskId $task.id
    if ($taskState.status -eq "in_progress") {
        $result = New-SelectionObject -Task $task -Resume $true

        if ($AsJson) {
            $result | ConvertTo-Json -Depth 100
        }
        else {
            $result
        }

        return
    }
}

$completed = @{}
foreach ($task in $tasksDoc.tasks) {
    $taskState = Get-TaskState -State $state -TaskId $task.id
    if ($taskState.status -eq "completed") {
        $completed[$task.id] = $true
    }
}

$readyTasks = @()
foreach ($task in $tasksDoc.tasks) {
    $taskState = Get-TaskState -State $state -TaskId $task.id
    if ($taskState.status -in @("completed", "blocked", "deferred", "in_progress", "omitted")) {
        continue
    }

    $dependenciesMet = $true
    foreach ($dependency in @($task.depends_on)) {
        if (-not $completed.ContainsKey([string]$dependency)) {
            $dependenciesMet = $false
            break
        }
    }

    if ($dependenciesMet) {
        $readyTasks += New-SelectionObject -Task $task
    }
}

if ($readyTasks.Count -eq 0) {
    $remaining = @()
    foreach ($task in $tasksDoc.tasks) {
        $taskState = Get-TaskState -State $state -TaskId $task.id
        if ($taskState.status -notin @("completed", "omitted", "deferred")) {
            $remaining += [pscustomobject]@{
                id = $task.id
                status = $taskState.status
            }
        }
    }

    $result = if ($remaining.Count -eq 0) {
        $deferredIntegrityIssues = @()
        foreach ($task in $tasksDoc.tasks) {
            $taskId = [string]$task.id
            $taskState = Get-TaskState -State $state -TaskId $taskId
            if ([string]$taskState.status -ne "deferred") {
                continue
            }

            $children = @($tasksDoc.tasks | Where-Object { $null -ne $_.PSObject.Properties["parent_id"] -and [string]$_.parent_id -eq $taskId })
            if ($children.Count -eq 0) {
                $deferredIntegrityIssues += [pscustomobject]@{
                    id = $taskId
                    reason = "deferred_task_has_no_children"
                }
                continue
            }

            $activeChildren = @($children | Where-Object {
                $childStatus = Get-TaskStateStatus -State $state -TaskId ([string]$_.id)
                -not (Test-IsTerminalStatus -Status $childStatus)
            })
            if ($activeChildren.Count -eq 0) {
                continue
            }
        }

        $currentTaskId = $null
        if ($null -ne $state.PSObject.Properties["current_task"] -and -not [string]::IsNullOrWhiteSpace([string]$state.current_task)) {
            $currentTaskId = [string]$state.current_task
        }

        if ($deferredIntegrityIssues.Count -gt 0) {
            [pscustomobject]@{
                status = "blocked"
                message = "No ready task found because one or more deferred tasks are structurally incomplete."
                remaining = $deferredIntegrityIssues
            }
        }
        elseif (-not [string]::IsNullOrWhiteSpace($currentTaskId)) {
            [pscustomobject]@{
                status = "blocked"
                message = "No ready task found because state.current_task is still set."
                remaining = @(
                    [pscustomobject]@{
                        id = $currentTaskId
                        status = (Get-TaskStateStatus -State $state -TaskId $currentTaskId)
                    }
                )
            }
        }
        else {
            [pscustomobject]@{
                status = "done"
                message = "All tasks are completed."
            }
        }
    }
    else {
        [pscustomobject]@{
            status = "blocked"
            message = "No ready task found."
            remaining = $remaining
        }
    }
}
else {
    $result = $readyTasks |
        Sort-Object -Property @{ Expression = "priority"; Ascending = $true }, @{ Expression = "order"; Ascending = $true } |
        Select-Object -First 1
}

if ($AsJson) {
    $result | ConvertTo-Json -Depth 100
}
else {
    $result
}


