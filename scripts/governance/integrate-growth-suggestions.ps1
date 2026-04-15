param(
  [string]$RepoPath,
  [ValidateSet("plan", "safe")]
  [string]$Mode = "safe",
  [switch]$AsJson
)

$ErrorActionPreference = "Stop"
$kitRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$commonPath = Join-Path $kitRoot "scripts\lib\common.ps1"
if (Test-Path -LiteralPath $commonPath -PathType Leaf) {
  . $commonPath
}

if (-not (Get-Command -Name Read-JsonFile -ErrorAction SilentlyContinue)) {
  function Read-JsonFile {
    param([Parameter(Mandatory = $true)][string]$Path, [object]$DefaultValue = $null)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $DefaultValue }
    try { return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json) } catch { return $DefaultValue }
  }
}

if (-not (Get-Command -Name Read-JsonArray -ErrorAction SilentlyContinue)) {
  function Read-JsonArray([string]$Path) {
    $raw = Read-JsonFile -Path $Path -DefaultValue @()
    if ($null -eq $raw) { return @() }
    if ($raw -is [System.Array]) { return @($raw) }
    if ($raw.PSObject -and $raw.PSObject.Properties["value"]) { return @($raw.value) }
    return @($raw)
  }
}

function Write-TextFile([string]$Path, [string]$Content) {
  if (Get-Command -Name Write-Utf8NoBom -ErrorAction SilentlyContinue) {
    Write-Utf8NoBom -Path $Path -Content $Content
    return
  }
  Set-Content -LiteralPath $Path -Encoding UTF8 -Value $Content
}

function Get-Level2Sections([string]$Text) {
  $lines = $Text -split "`r?`n"
  $sections = [System.Collections.Generic.List[object]]::new()
  $currentTitle = $null
  $buffer = [System.Collections.Generic.List[string]]::new()
  foreach ($line in $lines) {
    if ($line -match '^##\s+(.+?)\s*$') {
      if ($null -ne $currentTitle) {
        $sections.Add([pscustomobject]@{
          title = [string]$currentTitle
          content = [string]::Join([Environment]::NewLine, @($buffer))
        }) | Out-Null
        $buffer = [System.Collections.Generic.List[string]]::new()
      }
      $currentTitle = [string]$matches[1]
      continue
    }
    if ($null -ne $currentTitle) {
      $buffer.Add($line) | Out-Null
    }
  }
  if ($null -ne $currentTitle) {
    $sections.Add([pscustomobject]@{
      title = [string]$currentTitle
      content = [string]::Join([Environment]::NewLine, @($buffer))
    }) | Out-Null
  }
  return @($sections)
}

function Merge-MarkdownByMissingSections([string]$ExistingText, [string]$SuggestedText) {
  if ($ExistingText -match '<[A-Za-z][^>\r\n]*>') {
    return [pscustomobject]@{
      changed = $true
      reason = "materialized_from_suggested"
      content = $SuggestedText
    }
  }

  $existingTitles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
  foreach ($m in [regex]::Matches($ExistingText, '(?m)^##\s+(.+?)\s*$')) {
    $existingTitles.Add([string]$m.Groups[1].Value.Trim()) | Out-Null
  }
  $sections = @(Get-Level2Sections -Text $SuggestedText)
  if ($sections.Count -eq 0) {
    return [pscustomobject]@{
      changed = $false
      reason = "no_level2_sections"
      content = $ExistingText
    }
  }

  $missing = [System.Collections.Generic.List[object]]::new()
  foreach ($sec in $sections) {
    if (-not $existingTitles.Contains([string]$sec.title)) {
      $missing.Add($sec) | Out-Null
    }
  }
  if ($missing.Count -eq 0) {
    return [pscustomobject]@{
      changed = $false
      reason = "unchanged"
      content = $ExistingText
    }
  }

  $merged = [string]$ExistingText.TrimEnd()
  foreach ($sec in $missing) {
    $merged += [Environment]::NewLine + [Environment]::NewLine + "## " + [string]$sec.title + [Environment]::NewLine + [string]$sec.content.Trim()
  }
  $merged += [Environment]::NewLine

  return [pscustomobject]@{
    changed = $true
    reason = "appended_missing_sections"
    content = $merged
  }
}

$repos = @()
if (-not [string]::IsNullOrWhiteSpace($RepoPath)) {
  $repos = @([System.IO.Path]::GetFullPath(($RepoPath -replace '/', '\')))
} else {
  $reposPath = Join-Path $kitRoot "config\repositories.json"
  $repos = @((Read-JsonArray $reposPath) | ForEach-Object { [System.IO.Path]::GetFullPath(([string]$_ -replace '/', '\')) })
}

$items = [System.Collections.Generic.List[object]]::new()
$summary = [ordered]@{
  repos = 0
  candidates = 0
  integrated = 0
  kept_for_manual = 0
  dropped_unchanged = 0
  skipped = 0
}

foreach ($repo in $repos) {
  if (-not (Test-Path -LiteralPath $repo -PathType Container)) {
    continue
  }
  $summary.repos++
  $candidates = @(Get-ChildItem -Path $repo -Recurse -File -Filter "*.growth-pack.suggested" -ErrorAction SilentlyContinue)
  foreach ($suggest in $candidates) {
    $summary.candidates++
    $suggestPath = [string]$suggest.FullName
    $targetPath = $suggestPath.Substring(0, $suggestPath.Length - ".growth-pack.suggested".Length)

    if (-not (Test-Path -LiteralPath $targetPath -PathType Leaf)) {
      if ($Mode -eq "plan") {
        $items.Add([pscustomobject]@{ repo = $repo; action = "PLAN_CREATE_FROM_SUGGESTION"; target = $targetPath; suggestion = $suggestPath }) | Out-Null
        continue
      }
      $targetDir = Split-Path -Parent $targetPath
      if (-not (Test-Path -LiteralPath $targetDir -PathType Container)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
      }
      Copy-Item -LiteralPath $suggestPath -Destination $targetPath -Force
      Remove-Item -LiteralPath $suggestPath -Force
      $summary.integrated++
      $items.Add([pscustomobject]@{ repo = $repo; action = "CREATE_FROM_SUGGESTION"; target = $targetPath; suggestion = $suggestPath }) | Out-Null
      continue
    }

    $targetExtRaw = [System.IO.Path]::GetExtension($targetPath)
    if ($null -eq $targetExtRaw) { $targetExtRaw = "" }
    $targetExt = ([string]$targetExtRaw).ToLowerInvariant()
    if ($targetExt -ne ".md") {
      $summary.kept_for_manual++
      $items.Add([pscustomobject]@{ repo = $repo; action = "KEEP_SUGGESTION_MANUAL"; target = $targetPath; suggestion = $suggestPath; reason = "non_markdown" }) | Out-Null
      continue
    }

    $suggestText = Get-Content -LiteralPath $suggestPath -Raw
    $targetText = Get-Content -LiteralPath $targetPath -Raw
    $merge = Merge-MarkdownByMissingSections -ExistingText $targetText -SuggestedText $suggestText

    if (-not [bool]$merge.changed) {
      if ($Mode -eq "plan") {
        $items.Add([pscustomobject]@{ repo = $repo; action = "PLAN_DROP_SUGGESTION_UNCHANGED"; target = $targetPath; suggestion = $suggestPath; reason = [string]$merge.reason }) | Out-Null
      } else {
        Remove-Item -LiteralPath $suggestPath -Force
        $summary.dropped_unchanged++
        $items.Add([pscustomobject]@{ repo = $repo; action = "DROP_SUGGESTION_UNCHANGED"; target = $targetPath; suggestion = $suggestPath; reason = [string]$merge.reason }) | Out-Null
      }
      continue
    }

    if ($Mode -eq "plan") {
      $items.Add([pscustomobject]@{ repo = $repo; action = "PLAN_MERGE_FROM_SUGGESTION"; target = $targetPath; suggestion = $suggestPath; reason = [string]$merge.reason }) | Out-Null
      continue
    }
    Write-TextFile -Path $targetPath -Content ([string]$merge.content)
    Remove-Item -LiteralPath $suggestPath -Force
    $summary.integrated++
    $items.Add([pscustomobject]@{ repo = $repo; action = "MERGE_FROM_SUGGESTION"; target = $targetPath; suggestion = $suggestPath; reason = [string]$merge.reason }) | Out-Null
  }
}

if ($AsJson) {
  [pscustomobject]@{
    mode = $Mode
    summary = [pscustomobject]$summary
    items = @($items)
  } | ConvertTo-Json -Depth 8 | Write-Output
  exit 0
}

Write-Host ("integrate_growth_suggestions.mode={0}" -f $Mode)
Write-Host ("integrate_growth_suggestions.repos={0}" -f $summary.repos)
Write-Host ("integrate_growth_suggestions.candidates={0}" -f $summary.candidates)
Write-Host ("integrate_growth_suggestions.integrated={0}" -f $summary.integrated)
Write-Host ("integrate_growth_suggestions.kept_for_manual={0}" -f $summary.kept_for_manual)
Write-Host ("integrate_growth_suggestions.dropped_unchanged={0}" -f $summary.dropped_unchanged)
Write-Host ("integrate_growth_suggestions.skipped={0}" -f $summary.skipped)
