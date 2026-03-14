param(
    [string]$TaskFile = "docs/refactor/tasks.json",
    [string]$StateFile = ".codex/refactor-state.json",
    [string]$ProgressDoc = "docs/validation/2026-03-06-target-architecture-progress.md",
    [string]$HandoverDoc = "docs/handover.md",
    [string]$FinalAcceptanceDoc = "docs/validation/target-architecture-final-acceptance.md",
    [switch]$Fix,
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

function Write-JsonFile {
    param(
        [string]$Path,
        [object]$Object
    )

    $Object | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Get-StateTaskStatus {
    param(
        [object]$StateDoc,
        [string]$TaskId
    )

    if ($null -eq $StateDoc -or $null -eq $StateDoc.tasks) {
        return $null
    }

    $taskProp = $StateDoc.tasks.PSObject.Properties[$TaskId]
    if ($null -eq $taskProp -or $null -eq $taskProp.Value) {
        return $null
    }

    if ($null -eq $taskProp.Value.PSObject.Properties["status"]) {
        return $null
    }

    $status = [string]$taskProp.Value.status
    if ([string]::IsNullOrWhiteSpace($status)) {
        return $null
    }

    return $status
}

function Ensure-TaskStatusHint {
    param(
        [object]$Task,
        [string]$StatusHint
    )

    if ($null -eq $Task.PSObject.Properties["status_hint"]) {
        $Task | Add-Member -NotePropertyName "status_hint" -NotePropertyValue $StatusHint
    }
    else {
        $Task.status_hint = $StatusHint
    }
}

function Get-DocLines {
    param(
        [hashtable]$Cache,
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    if ($Cache.ContainsKey($Path)) {
        return @($Cache[$Path])
    }

    $lines = @()
    foreach ($line in (Get-Content -LiteralPath $Path -Encoding UTF8)) {
        $lines += [string]$line
    }

    $Cache[$Path] = @($lines)
    return @($lines)
}

function Save-DocLines {
    param(
        [hashtable]$Cache,
        [string]$Path
    )

    if (-not $Cache.ContainsKey($Path)) {
        return
    }

    $text = ($Cache[$Path] -join [Environment]::NewLine).TrimEnd()
    $text += [Environment]::NewLine
    Set-Content -LiteralPath $Path -Value $text -Encoding UTF8
}

function Find-LineIndex {
    param(
        [string[]]$Lines,
        [string]$Pattern
    )

    for ($index = 0; $index -lt $Lines.Count; $index++) {
        if ($Lines[$index] -match $Pattern) {
            return $index
        }
    }

    return -1
}

$tasksDoc = Read-JsonFile -Path $TaskFile
$stateDoc = Read-JsonFile -Path $StateFile
$modeFamily = ""
if ($null -ne $tasksDoc.PSObject.Properties["mode_family"]) {
    $modeFamily = [string]$tasksDoc.mode_family
}

$issues = [System.Collections.Generic.List[object]]::new()
$fixes = [System.Collections.Generic.List[object]]::new()
$changedFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

$tasksChanged = $false
$docCache = @{}
$docChanged = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

$taskIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

foreach ($task in @($tasksDoc.tasks)) {
    if ($null -eq $task -or [string]::IsNullOrWhiteSpace([string]$task.id)) {
        continue
    }

    $taskId = [string]$task.id
    $null = $taskIds.Add($taskId)
    $stateStatus = Get-StateTaskStatus -StateDoc $stateDoc -TaskId $taskId
    if ([string]::IsNullOrWhiteSpace($stateStatus)) {
        continue
    }

    $statusHint = ""
    if ($null -ne $task.PSObject.Properties["status_hint"]) {
        $statusHint = [string]$task.status_hint
    }

    if ($statusHint -eq $stateStatus) {
        continue
    }

    $issue = [ordered]@{
        type = "task_status_hint_mismatch"
        task_id = $taskId
        task_status_hint = $statusHint
        state_status = $stateStatus
    }

    if ($Fix) {
        Ensure-TaskStatusHint -Task $task -StatusHint $stateStatus
        $tasksChanged = $true
        $null = $changedFiles.Add($TaskFile)

        $fixes.Add([ordered]@{
                type = "sync_task_status_hint"
                task_id = $taskId
                from = $statusHint
                to = $stateStatus
            }) | Out-Null

        $issue["fixed"] = $true
    }
    else {
        $issue["fixed"] = $false
    }

    $issues.Add($issue) | Out-Null
}

if ($null -ne $stateDoc.tasks) {
    foreach ($stateProp in $stateDoc.tasks.PSObject.Properties) {
        $taskId = [string]$stateProp.Name
        if ($taskIds.Contains($taskId)) {
            continue
        }

        $issues.Add([ordered]@{
                type = "state_task_missing_in_tasks_graph"
                task_id = $taskId
                fixed = $false
            }) | Out-Null
    }
}

$isUiMode = $modeFamily -eq "ui-overhaul"

$freezePassed = $false
foreach ($freezeTaskId in @("automated-freeze-recheck-after-gap-closure", "automated-freeze-check")) {
    if ((Get-StateTaskStatus -StateDoc $stateDoc -TaskId $freezeTaskId) -eq "completed") {
        $freezePassed = $true
        break
    }
}

$expectedProgressStageLine = if ($freezePassed) {
    "- 当前阶段：自动化冻结复检已通过，等待人工最终回归。"
}
else {
    "- 当前阶段：自动化冻结复检未通过，回到架构守卫修复后再进人工最终回归。"
}

$expectedFinalStatusLine = if ($freezePassed) {
    "- 当前状态：自动化冻结复检已通过，自动化门已闭合，等待人工最终回归。"
}
else {
    "- 当前状态：自动化冻结复检未通过，需先修复自动化门后再进入人工最终回归。"
}

$expectedHandoverFreezeLine = if ($freezePassed) {
    '- 自动化冻结复检任务 `automated-freeze-recheck-after-gap-closure` 已完成：`ArchitectureDependencyTests`=`5/5`，全量 Debug=`2227/2227`，全量 Release=`2227/2227`（2026-03-13）；当前自动化门已闭合，下一步仅剩人工最终回归。'
}
else {
    '- 自动化冻结复检任务 `automated-freeze-recheck-after-gap-closure` 未通过或未完成；需先修复自动化门后再进入人工最终回归。'
}

$progressLines = $null
if (-not $isUiMode) {
    $progressLines = Get-DocLines -Cache $docCache -Path $ProgressDoc
}
if ($null -ne $progressLines) {
    $stageLineIndex = Find-LineIndex -Lines $progressLines -Pattern "^\-\s*当前阶段："
    if ($stageLineIndex -lt 0) {
        $issues.Add([ordered]@{
                type = "progress_stage_line_missing"
                file = $ProgressDoc
                fixed = $false
            }) | Out-Null
    }
    else {
        $currentLine = [string]$progressLines[$stageLineIndex]
        if ($currentLine -ne $expectedProgressStageLine) {
            $issue = [ordered]@{
                type = "progress_stage_conflict"
                file = $ProgressDoc
                current_line = $currentLine
                expected_line = $expectedProgressStageLine
            }

            if ($Fix) {
                $progressLines[$stageLineIndex] = $expectedProgressStageLine
                $docCache[$ProgressDoc] = @($progressLines)
                $null = $docChanged.Add($ProgressDoc)
                $null = $changedFiles.Add($ProgressDoc)

                $fixes.Add([ordered]@{
                        type = "sync_progress_stage_line"
                        file = $ProgressDoc
                        from = $currentLine
                        to = $expectedProgressStageLine
                    }) | Out-Null

                $issue["fixed"] = $true
            }
            else {
                $issue["fixed"] = $false
            }

            $issues.Add($issue) | Out-Null
        }
    }
}

$handoverLines = $null
if (-not $isUiMode) {
    $handoverLines = Get-DocLines -Cache $docCache -Path $HandoverDoc
}
if ($null -ne $handoverLines) {
    $handoverLineIndex = Find-LineIndex -Lines $handoverLines -Pattern '^\-\s*自动化冻结复检任务\s+`automated-freeze-recheck-after-gap-closure`'
    if ($handoverLineIndex -lt 0) {
        $issues.Add([ordered]@{
                type = "handover_status_line_missing"
                file = $HandoverDoc
                fixed = $false
            }) | Out-Null
    }
    else {
        $currentLine = [string]$handoverLines[$handoverLineIndex]
        if ($currentLine -ne $expectedHandoverFreezeLine) {
            $issue = [ordered]@{
                type = "handover_status_conflict"
                file = $HandoverDoc
                current_line = $currentLine
                expected_line = $expectedHandoverFreezeLine
            }

            if ($Fix) {
                $handoverLines[$handoverLineIndex] = $expectedHandoverFreezeLine
                $docCache[$HandoverDoc] = @($handoverLines)
                $null = $docChanged.Add($HandoverDoc)
                $null = $changedFiles.Add($HandoverDoc)

                $fixes.Add([ordered]@{
                        type = "sync_handover_status_line"
                        file = $HandoverDoc
                        from = $currentLine
                        to = $expectedHandoverFreezeLine
                    }) | Out-Null

                $issue["fixed"] = $true
            }
            else {
                $issue["fixed"] = $false
            }

            $issues.Add($issue) | Out-Null
        }
    }
}

$finalAcceptanceLines = $null
if (-not $isUiMode) {
    $finalAcceptanceLines = Get-DocLines -Cache $docCache -Path $FinalAcceptanceDoc
}
if ($null -ne $finalAcceptanceLines) {
    $finalStatusIndex = Find-LineIndex -Lines $finalAcceptanceLines -Pattern "^\-\s*当前状态："
    if ($finalStatusIndex -lt 0) {
        $issues.Add([ordered]@{
                type = "final_acceptance_status_line_missing"
                file = $FinalAcceptanceDoc
                fixed = $false
            }) | Out-Null
    }
    else {
        $currentLine = [string]$finalAcceptanceLines[$finalStatusIndex]
        if ($currentLine -ne $expectedFinalStatusLine) {
            $issue = [ordered]@{
                type = "final_acceptance_status_conflict"
                file = $FinalAcceptanceDoc
                current_line = $currentLine
                expected_line = $expectedFinalStatusLine
            }

            if ($Fix) {
                $finalAcceptanceLines[$finalStatusIndex] = $expectedFinalStatusLine
                $docCache[$FinalAcceptanceDoc] = @($finalAcceptanceLines)
                $null = $docChanged.Add($FinalAcceptanceDoc)
                $null = $changedFiles.Add($FinalAcceptanceDoc)

                $fixes.Add([ordered]@{
                        type = "sync_final_acceptance_status_line"
                        file = $FinalAcceptanceDoc
                        from = $currentLine
                        to = $expectedFinalStatusLine
                    }) | Out-Null

                $issue["fixed"] = $true
            }
            else {
                $issue["fixed"] = $false
            }

            $issues.Add($issue) | Out-Null
        }
    }
}

if ($Fix -and $tasksChanged) {
    if ($null -ne $tasksDoc.PSObject.Properties["updated_at"]) {
        $tasksDoc.updated_at = (Get-Date).ToString("yyyy-MM-dd")
    }

    Write-JsonFile -Path $TaskFile -Object $tasksDoc
}

if ($Fix) {
    foreach ($docPath in @($docChanged)) {
        Save-DocLines -Cache $docCache -Path $docPath
    }
}

$remainingIssues = @($issues | Where-Object { -not [bool]$_.fixed })
$status = if ($remainingIssues.Count -gt 0) {
    "needs_human"
}
elseif ($fixes.Count -gt 0) {
    "fixed"
}
else {
    "ok"
}

$result = [ordered]@{
    status = $status
    fix_mode = [bool]$Fix
    issues_total = $issues.Count
    issues_remaining = $remainingIssues.Count
    fixes_applied = $fixes.Count
    changed_files = @($changedFiles)
    issues = @($issues)
    fixes = @($fixes)
}

if ($AsJson) {
    $result | ConvertTo-Json -Depth 100
}
else {
    $result
}

if ($status -eq "needs_human") {
    exit 2
}

exit 0

