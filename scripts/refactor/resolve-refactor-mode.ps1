param(
    [string]$Mode = "architecture-refactor",
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

function Resolve-RelativeRepoPath {
    param(
        [string]$BasePath,
        [string]$RelativePath
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        return $null
    }

    if ([System.IO.Path]::IsPathRooted($RelativePath)) {
        return $RelativePath
    }

    return Join-Path $BasePath $RelativePath
}

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$registryPath = Join-Path $repoPath ".codex/refactor-modes.json"
$registry = Read-JsonFile -Path $registryPath

$matches = @($registry.modes | Where-Object {
    [string]$_.mode_id -eq $Mode -or [string]$_.mode_family -eq $Mode
})

if ($matches.Count -eq 0) {
    throw "Unsupported refactor mode or family: $Mode"
}

if ($matches.Count -gt 1) {
    throw "Mode resolution is ambiguous for '$Mode'."
}

$match = $matches[0]
$configFile = if ($null -ne $match.PSObject.Properties["config_file"]) { [string]$match.config_file } else { $null }
$result = [pscustomobject]@{
    repo_root = $repoPath
    registry_file = ".codex/refactor-modes.json"
    mode_id = [string]$match.mode_id
    mode_family = [string]$match.mode_family
    tasks_file = [string]$match.tasks_file
    state_file = [string]$match.state_file
    config_file = $configFile
    tasks_file_resolved = Resolve-RelativeRepoPath -BasePath $repoPath -RelativePath ([string]$match.tasks_file)
    state_file_resolved = Resolve-RelativeRepoPath -BasePath $repoPath -RelativePath ([string]$match.state_file)
    config_file_resolved = Resolve-RelativeRepoPath -BasePath $repoPath -RelativePath $configFile
    governing_docs = @($match.governing_docs)
    verification = $match.verification
    manual_gates = @($match.manual_gates)
    stop_rules = @($match.stop_rules)
}

if ($AsJson) {
    $result | ConvertTo-Json -Depth 50
}
else {
    $result
}
