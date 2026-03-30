param(
    [Parameter(Mandatory = $true)]
    [string]$TaskId,
    [string]$TaskFile = "docs/refactor/tasks.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$tasksDoc = Get-Content -LiteralPath $TaskFile -Raw | ConvertFrom-Json
$task = $tasksDoc.tasks | Where-Object { $_.id -eq $TaskId } | Select-Object -First 1

if ($null -eq $task) {
    throw "Task not found: $TaskId"
}

Write-Output "Task: $($task.id) - $($task.title)"
Write-Output "Summary: $($task.summary)"
Write-Output ""
Write-Output "Done When:"
foreach ($item in @($task.done_when)) {
    Write-Output "- $item"
}

Write-Output ""
Write-Output "Verify Commands:"
foreach ($command in @($task.verify.commands)) {
    Write-Output "- $command"
}

if ($task.doc_sync.Count -gt 0) {
    Write-Output ""
    Write-Output "Doc Sync:"
    foreach ($doc in @($task.doc_sync)) {
        Write-Output "- $doc"
    }
}

