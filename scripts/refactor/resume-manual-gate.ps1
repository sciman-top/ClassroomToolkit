param(
    [Parameter(Mandatory = $true)]
    [string]$Mode,
    [Parameter(Mandatory = $true)]
    [string]$GateId,
    [Parameter(Mandatory = $true)]
    [string]$TaskId,
    [Parameter(Mandatory = $true)]
    [string]$EvidenceDoc,
    [string]$RepoRoot = ".",
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

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$resolverPath = Join-Path $repoPath "scripts/refactor/resolve-refactor-mode.ps1"
$modeInfo = & pwsh -NoProfile -ExecutionPolicy Bypass -File $resolverPath -RepoRoot $repoPath -Mode $Mode -AsJson | ConvertFrom-Json
$statePath = $modeInfo.state_file_resolved
$taskPath = $modeInfo.tasks_file_resolved

$state = Read-JsonFile -Path $statePath
$tasksDoc = Read-JsonFile -Path $taskPath

if (-not (Test-Path -LiteralPath (Join-Path $repoPath $EvidenceDoc)) -and -not (Test-Path -LiteralPath $EvidenceDoc)) {
    throw "Evidence doc not found: $EvidenceDoc"
}

$gateFieldMap = @{
    "theme-freeze" = "theme_frozen"
    "main-scene-freeze" = "main_scene_frozen"
    "fullscreen-float-freeze" = "fullscreen_frozen"
    "final-visual-regression" = "final_visual_review_passed"
}

if (-not $gateFieldMap.ContainsKey($GateId)) {
    throw "Unsupported gate id: $GateId"
}

$gateField = [string]$gateFieldMap[$GateId]
if ($null -eq $state.PSObject.Properties[$gateField]) {
    $state | Add-Member -NotePropertyName $gateField -NotePropertyValue $true
}
else {
    $state.$gateField = $true
}

$state.current_manual_gate = $null
$state.current_task = $null
$state.blocked = @($state.blocked | Where-Object {
    -not ([string]$_.task_id -eq $TaskId -and [string]$_.reason -like "*$GateId*")
})

if ($null -eq $state.tasks.PSObject.Properties[$TaskId]) {
    $state.tasks | Add-Member -NotePropertyName $TaskId -NotePropertyValue ([pscustomobject]@{
        status = "pending"
        attempts = 0
        last_summary = ""
    })
}

$taskState = $state.tasks.PSObject.Properties[$TaskId].Value
$taskState.status = "pending"
$taskState.last_summary = "Manual gate '$GateId' resumed with evidence '$EvidenceDoc'."
$state.last_summary = $taskState.last_summary

if ($null -eq $state.PSObject.Properties["governing_reconciliation"]) {
    $state | Add-Member -NotePropertyName "governing_reconciliation" -NotePropertyValue ([pscustomobject]@{})
}

$docPaths = @()
if ($null -ne $tasksDoc.PSObject.Properties["governing_reconciliation"] -and $null -ne $tasksDoc.governing_reconciliation.PSObject.Properties["doc_paths"]) {
    $docPaths = @($tasksDoc.governing_reconciliation.doc_paths)
}

$state.governing_reconciliation = [pscustomobject]@{
    last_reconciled_at = [DateTime]::UtcNow.ToString("o")
    doc_paths = $docPaths
    doc_sync_policy = if ($null -ne $tasksDoc.PSObject.Properties["governing_reconciliation"] -and $null -ne $tasksDoc.governing_reconciliation.PSObject.Properties["doc_sync_policy"]) { [string]$tasksDoc.governing_reconciliation.doc_sync_policy } else { "spec > integration-design > implementation-plan > progress > acceptance" }
}

$state.updated_at = [DateTime]::UtcNow.ToString("o")
$state.history += [pscustomobject]@{
    action = "gate-resume"
    task_id = $TaskId
    summary = "Resumed gate '$GateId'."
    reason = $GateId
    evidence_doc = $EvidenceDoc
    recorded_at = [DateTime]::UtcNow.ToString("o")
}

$state | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $statePath -Encoding UTF8

if ($AsJson) {
    $state | ConvertTo-Json -Depth 100
}
else {
    $state
}
