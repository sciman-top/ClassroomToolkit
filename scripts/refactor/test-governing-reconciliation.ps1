param(
    [string]$TaskFile = "docs/refactor/tasks.json",
    [string]$ConfigFile = "",
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

$tasksDoc = Read-JsonFile -Path $TaskFile

function New-MissingMetadataResult {
    param([string]$Message)

    $result = [pscustomobject]@{
        status = "needs_reconciliation"
        message = $Message
        source = "none"
        newer_docs = @()
    }
}

$reconciliation = $null
$reconciliationSource = "none"

if ($null -ne $tasksDoc.PSObject.Properties["governing_reconciliation"]) {
    $reconciliation = $tasksDoc.governing_reconciliation
    $reconciliationSource = "tasks"
}
elseif (-not [string]::IsNullOrWhiteSpace($ConfigFile) -and (Test-Path -LiteralPath $ConfigFile)) {
    $configDoc = Read-JsonFile -Path $ConfigFile
    if ($null -ne $configDoc.PSObject.Properties["governing_reconciliation"]) {
        $reconciliation = $configDoc.governing_reconciliation
        $reconciliationSource = "config"
    }
}

if ($null -eq $reconciliation) {
    $result = New-MissingMetadataResult -Message "governing_reconciliation metadata is missing in both task file and config."
}
else {
    if ($null -eq $reconciliation.PSObject.Properties["doc_paths"] -or @($reconciliation.doc_paths).Count -eq 0) {
        $result = New-MissingMetadataResult -Message "governing_reconciliation.doc_paths is missing or empty."
    }
    elseif ($null -eq $reconciliation.PSObject.Properties["last_reconciled_at"] -or [string]::IsNullOrWhiteSpace([string]$reconciliation.last_reconciled_at)) {
        $result = New-MissingMetadataResult -Message "governing_reconciliation.last_reconciled_at is missing."
    }
    else {
        $reconciledAt = [DateTime]::Parse([string]$reconciliation.last_reconciled_at).ToUniversalTime()
        $newerDocs = @()

        foreach ($docPath in @($reconciliation.doc_paths)) {
            if (-not (Test-Path -LiteralPath $docPath)) {
                $newerDocs += [pscustomobject]@{
                    path = [string]$docPath
                    reason = "missing"
                }
                continue
            }

            $docInfo = Get-Item -LiteralPath $docPath
            if ($docInfo.LastWriteTimeUtc -gt $reconciledAt) {
                $newerDocs += [pscustomobject]@{
                    path = [string]$docPath
                    reason = "newer_than_reconciliation"
                    last_write_utc = $docInfo.LastWriteTimeUtc.ToString("o")
                }
            }
        }

        if ($newerDocs.Count -gt 0) {
            $result = [pscustomobject]@{
                status = "needs_reconciliation"
                message = "One or more governing docs are newer than the last execution-graph reconciliation."
                source = $reconciliationSource
                newer_docs = $newerDocs
            }
        }
        else {
            $result = [pscustomobject]@{
                status = "ok"
                message = "Governing docs are not newer than the execution-graph reconciliation stamp."
                source = $reconciliationSource
                newer_docs = @()
            }
        }
    }
}

if ($null -eq $result.PSObject.Properties["source"]) {
    $result | Add-Member -NotePropertyName source -NotePropertyValue $reconciliationSource -Force
}

if ($AsJson) {
    $result | ConvertTo-Json -Depth 20
}
else {
    $result
}
