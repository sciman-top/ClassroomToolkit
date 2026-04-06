param(
  [string]$RepoPath = ".",
  [string]$PolicyPath = "",
  [ValidateSet("staged", "outgoing", "both")]
  [string]$Scope = "both",
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Normalize-RepoPath {
  param([object]$PathValue)
  $resolved = Resolve-Path -LiteralPath $PathValue -ErrorAction SilentlyContinue
  if ($null -eq $resolved -or -not (Test-Path -LiteralPath $resolved.Path -PathType Container)) {
    throw "Repo path not found: $PathValue"
  }
  return [System.IO.Path]::GetFullPath($resolved.Path)
}

function Convert-GlobToRegex {
  param([string]$Pattern)
  $normalized = ($Pattern -replace '\\', '/').Trim()
  if ([string]::IsNullOrWhiteSpace($normalized)) {
    return "^\z$"
  }

  $escaped = [regex]::Escape($normalized)
  $escaped = $escaped -replace '\\\*\\\*', '__DOUBLE_STAR__'
  $escaped = $escaped -replace '\\\*', '[^/]*'
  $escaped = $escaped -replace '\\\?', '[^/]'
  $escaped = $escaped -replace '__DOUBLE_STAR__', '.*'
  return '^' + $escaped + '$'
}

function Read-PolicyRules {
  param(
    [object]$PathValue,
    [string]$RuleName
  )

  $result = New-Object System.Collections.Generic.List[object]
  if ($null -eq $PathValue) { return @($result.ToArray()) }
  $items = @()
  if ($PathValue -is [System.Array]) {
    $items = @($PathValue)
  } elseif ($null -ne $PathValue) {
    $items = @($PathValue)
  }

  foreach ($item in $items) {
    if ($null -eq $item) { continue }
    $pattern = [string]$item.pattern
    if ([string]::IsNullOrWhiteSpace($pattern)) { continue }
    $reason = [string]$item.reason
    $result.Add([pscustomobject]@{
      category = $RuleName
      pattern = ($pattern -replace '\\', '/')
      regex = (Convert-GlobToRegex -Pattern $pattern)
      reason = $reason
    }) | Out-Null
  }
  return @($result.ToArray())
}

function Invoke-GitLines {
  param(
    [string]$Repo,
    [string[]]$Args
  )
  $output = & git -C $Repo @Args 2>$null
  $code = $LASTEXITCODE
  if ($code -ne 0) {
    return [pscustomobject]@{
      ok = $false
      lines = @()
      exit_code = $code
    }
  }
  return [pscustomobject]@{
    ok = $true
    lines = @($output)
    exit_code = 0
  }
}

function Get-StagedPaths {
  param([string]$Repo)
  $r = Invoke-GitLines -Repo $Repo -Args @("diff", "--cached", "--name-only", "--diff-filter=ACMR")
  if (-not $r.ok) { return @() }
  return @($r.lines | ForEach-Object { ([string]$_).Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_ -replace '\\', '/' })
}

function Get-OutgoingPaths {
  param([string]$Repo)

  $upstream = (& git -C $Repo rev-parse --abbrev-ref --symbolic-full-name "@{u}" 2>$null)
  if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace([string]$upstream)) {
    return [pscustomobject]@{
      paths = @()
      note = "upstream_not_configured"
    }
  }

  $range = ([string]$upstream).Trim() + "...HEAD"
  $r = Invoke-GitLines -Repo $Repo -Args @("diff", "--name-only", "--diff-filter=ACMR", $range)
  if (-not $r.ok) {
    return [pscustomobject]@{
      paths = @()
      note = "upstream_diff_failed"
    }
  }

  return [pscustomobject]@{
    paths = @($r.lines | ForEach-Object { ([string]$_).Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_ -replace '\\', '/' })
    note = "ok"
  }
}

function Find-RuleHits {
  param(
    [string[]]$Paths,
    [object[]]$Rules
  )
  $hits = New-Object System.Collections.Generic.List[object]
  foreach ($p in $Paths) {
    foreach ($r in $Rules) {
      if ([regex]::IsMatch($p, $r.regex)) {
        $hits.Add([pscustomobject]@{
          path = $p
          category = $r.category
          pattern = $r.pattern
          reason = $r.reason
        }) | Out-Null
      }
    }
  }
  return @($hits.ToArray())
}

$repo = Normalize-RepoPath -PathValue $RepoPath
$gitRoot = (& git -C $repo rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace([string]$gitRoot)) {
  throw "Not a git repository: $repo"
}
$repo = [System.IO.Path]::GetFullPath(([string]$gitRoot).Trim())

if ([string]::IsNullOrWhiteSpace($PolicyPath)) {
  $PolicyPath = Join-Path $repo ".governance\tracked-files-policy.json"
}
$policyResolved = Resolve-Path -LiteralPath $PolicyPath -ErrorAction SilentlyContinue
$policyFull = if ($null -eq $policyResolved) { [System.IO.Path]::GetFullPath($PolicyPath) } else { [System.IO.Path]::GetFullPath($policyResolved.Path) }

$policyFound = Test-Path -LiteralPath $policyFull -PathType Leaf
$policy = $null
if ($policyFound) {
  try {
    $policy = Get-Content -LiteralPath $policyFull -Raw | ConvertFrom-Json
  } catch {
    throw "Invalid tracked files policy JSON: $policyFull"
  }
}

$mustIgnoreRules = @()
$reviewRules = @()
$mustTrackRules = @()
$blockMustIgnore = $true
$blockReview = $false

if ($policyFound -and $null -ne $policy) {
  $mustIgnoreRules = @(Read-PolicyRules -PathValue $policy.rules.must_ignore -RuleName "must_ignore")
  $reviewRules = @(Read-PolicyRules -PathValue $policy.rules.review_required -RuleName "review_required")
  $mustTrackRules = @(Read-PolicyRules -PathValue $policy.rules.must_track -RuleName "must_track")

  if ($null -ne $policy.enforcement -and $policy.enforcement.PSObject.Properties.Name -contains "block_on_must_ignore") {
    $blockMustIgnore = [bool]$policy.enforcement.block_on_must_ignore
  }
  if ($null -ne $policy.enforcement -and $policy.enforcement.PSObject.Properties.Name -contains "block_on_review_required") {
    $blockReview = [bool]$policy.enforcement.block_on_review_required
  }
}

$stagedPaths = @()
$outgoingPaths = @()
$outgoingNote = "skipped_by_scope"
switch ($Scope) {
  "staged" {
    $stagedPaths = @(Get-StagedPaths -Repo $repo)
  }
  "outgoing" {
    $outgoingState = Get-OutgoingPaths -Repo $repo
    $outgoingPaths = @($outgoingState.paths)
    $outgoingNote = [string]$outgoingState.note
  }
  "both" {
    $stagedPaths = @(Get-StagedPaths -Repo $repo)
    $outgoingState = Get-OutgoingPaths -Repo $repo
    $outgoingPaths = @($outgoingState.paths)
    $outgoingNote = [string]$outgoingState.note
  }
}

$pathSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($p in @($stagedPaths + $outgoingPaths)) {
  if (-not [string]::IsNullOrWhiteSpace($p)) {
    [void]$pathSet.Add(($p -replace '\\', '/'))
  }
}
$effectivePaths = @($pathSet)

$mustIgnoreHits = @(Find-RuleHits -Paths $effectivePaths -Rules $mustIgnoreRules)
$reviewHits = @(Find-RuleHits -Paths $effectivePaths -Rules $reviewRules)
$mustTrackHits = @(Find-RuleHits -Paths $effectivePaths -Rules $mustTrackRules)

$blockedByMustIgnore = $blockMustIgnore -and $mustIgnoreHits.Count -gt 0
$blockedByReview = $blockReview -and $reviewHits.Count -gt 0
$blocked = $blockedByMustIgnore -or $blockedByReview

$result = [pscustomobject]@{
  repo = ($repo -replace '\\', '/')
  policy_path = ($policyFull -replace '\\', '/')
  policy_found = $policyFound
  scope = $Scope
  staged_count = $stagedPaths.Count
  outgoing_count = $outgoingPaths.Count
  outgoing_note = $outgoingNote
  checked_paths_count = $effectivePaths.Count
  must_ignore_hits = @($mustIgnoreHits)
  review_required_hits = @($reviewHits)
  must_track_hits = @($mustTrackHits)
  blocked = $blocked
  blocked_by = @(
    if ($blockedByMustIgnore) { "must_ignore" }
    if ($blockedByReview) { "review_required" }
  )
}

if ($AsJson) {
  $result | ConvertTo-Json -Depth 8
  if ($blocked) { exit 2 }
  exit 0
}

Write-Host ("tracked_files.repo=" + $result.repo)
Write-Host ("tracked_files.scope=" + $Scope)
Write-Host ("tracked_files.policy_path=" + $result.policy_path)

if (-not $policyFound) {
  Write-Host "[TRACKED] policy file not found, check skipped."
  exit 0
}

Write-Host ("tracked_files.checked_paths=" + $result.checked_paths_count)
Write-Host ("tracked_files.must_ignore_hits=" + $result.must_ignore_hits.Count)
Write-Host ("tracked_files.review_required_hits=" + $result.review_required_hits.Count)

if ($mustIgnoreHits.Count -gt 0) {
  foreach ($hit in $mustIgnoreHits) {
    Write-Host ("[BLOCK_FILE] " + $hit.path + " | pattern=" + $hit.pattern + " | reason=" + $hit.reason)
  }
}

if ($reviewHits.Count -gt 0) {
  foreach ($hit in $reviewHits) {
    Write-Host ("[REVIEW_FILE] " + $hit.path + " | pattern=" + $hit.pattern + " | reason=" + $hit.reason)
  }
}

if ($blocked) {
  Write-Host ("[BLOCK] tracked files policy violated. blocked_by=" + (@($result.blocked_by) -join ","))
  exit 2
}

Write-Host "[PASS] tracked files policy check passed"
exit 0



