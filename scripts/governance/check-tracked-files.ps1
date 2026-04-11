param(
  [string]$RepoPath = ".",
  [string]$PolicyPath = "",
  [ValidateSet("staged", "pending", "outgoing", "both")]
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

function Read-BooleanPolicyValue {
  param(
    [object]$PolicyObject,
    [string]$PropertyName,
    [bool]$DefaultValue
  )

  if ($null -eq $PolicyObject) { return $DefaultValue }
  if ($PolicyObject.PSObject.Properties.Name -contains $PropertyName) {
    return [bool]$PolicyObject.$PropertyName
  }
  return $DefaultValue
}

function Read-PolicyStringRules {
  param(
    [object]$PathValue,
    [string]$RuleName,
    [string]$DefaultReason
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

    if ($item -is [string]) {
      $patternText = ([string]$item).Trim()
      if ([string]::IsNullOrWhiteSpace($patternText)) { continue }
      $result.Add([pscustomobject]@{
        category = $RuleName
        pattern = ($patternText -replace '\\', '/')
        regex = (Convert-GlobToRegex -Pattern $patternText)
        reason = $DefaultReason
      }) | Out-Null
      continue
    }

    $pattern = [string]$item.pattern
    if ([string]::IsNullOrWhiteSpace($pattern)) { continue }
    $reason = [string]$item.reason
    if ([string]::IsNullOrWhiteSpace($reason)) {
      $reason = $DefaultReason
    }
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

function Get-PendingPaths {
  param([string]$Repo)

  $pathSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

  foreach ($p in @(Get-StagedPaths -Repo $Repo)) {
    if (-not [string]::IsNullOrWhiteSpace([string]$p)) {
      [void]$pathSet.Add(([string]$p -replace '\\', '/'))
    }
  }

  $unstaged = Invoke-GitLines -Repo $Repo -Args @("diff", "--name-only", "--diff-filter=ACMR")
  if ($unstaged.ok) {
    foreach ($p in @($unstaged.lines)) {
      $path = ([string]$p).Trim()
      if (-not [string]::IsNullOrWhiteSpace($path)) {
        [void]$pathSet.Add(($path -replace '\\', '/'))
      }
    }
  }

  $untracked = Invoke-GitLines -Repo $Repo -Args @("ls-files", "--others", "--exclude-standard")
  if ($untracked.ok) {
    foreach ($p in @($untracked.lines)) {
      $path = ([string]$p).Trim()
      if (-not [string]::IsNullOrWhiteSpace($path)) {
        [void]$pathSet.Add(($path -replace '\\', '/'))
      }
    }
  }

  return @($pathSet)
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

function Resolve-TestFileSuggestions {
  param(
    [string[]]$Paths,
    [object[]]$DetectionRules,
    [object[]]$IgnoreRules,
    [object[]]$TrackRules,
    [object[]]$ReviewRules
  )

  $result = New-Object System.Collections.Generic.List[object]
  foreach ($p in @($Paths)) {
    $isTestFile = $false
    foreach ($rule in @($DetectionRules)) {
      if ([regex]::IsMatch($p, $rule.regex)) {
        $isTestFile = $true
        break
      }
    }
    if (-not $isTestFile) { continue }

    $action = "review_required"
    $matchedPattern = ""
    $reason = "test file requires explicit owner judgement before commit/push"

    foreach ($rule in @($IgnoreRules)) {
      if ([regex]::IsMatch($p, $rule.regex)) {
        $action = "ignore"
        $matchedPattern = $rule.pattern
        $reason = $rule.reason
        break
      }
    }

    if ($action -eq "review_required") {
      foreach ($rule in @($TrackRules)) {
        if ([regex]::IsMatch($p, $rule.regex)) {
          $action = "track"
          $matchedPattern = $rule.pattern
          $reason = $rule.reason
          break
        }
      }
    }

    if ($action -eq "review_required") {
      foreach ($rule in @($ReviewRules)) {
        if ([regex]::IsMatch($p, $rule.regex)) {
          $matchedPattern = $rule.pattern
          $reason = $rule.reason
          break
        }
      }
    }

    $result.Add([pscustomobject]@{
      path = $p
      suggested_action = $action
      matched_pattern = $matchedPattern
      reason = $reason
    }) | Out-Null
  }

  return @($result.ToArray())
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
$blockTestFileReview = $false
$testSuggestionEnabled = $false
$testDetectionRules = @()
$testIgnoreRules = @()
$testTrackRules = @()
$testReviewRules = @()

if ($policyFound -and $null -ne $policy) {
  $mustIgnoreRules = @(Read-PolicyRules -PathValue $policy.rules.must_ignore -RuleName "must_ignore")
  $reviewRules = @(Read-PolicyRules -PathValue $policy.rules.review_required -RuleName "review_required")
  $mustTrackRules = @(Read-PolicyRules -PathValue $policy.rules.must_track -RuleName "must_track")

  $blockMustIgnore = Read-BooleanPolicyValue -PolicyObject $policy.enforcement -PropertyName "block_on_must_ignore" -DefaultValue $true
  $blockReview = Read-BooleanPolicyValue -PolicyObject $policy.enforcement -PropertyName "block_on_review_required" -DefaultValue $false
  $blockTestFileReview = Read-BooleanPolicyValue -PolicyObject $policy.enforcement -PropertyName "block_on_test_file_review_required" -DefaultValue $false

  if ($null -ne $policy.PSObject.Properties['test_file_suggestions'] -and $null -ne $policy.test_file_suggestions) {
    $testSuggestionEnabled = Read-BooleanPolicyValue -PolicyObject $policy.test_file_suggestions -PropertyName "enabled" -DefaultValue $false
    if ($testSuggestionEnabled) {
      $testDetectionRules = @(Read-PolicyStringRules -PathValue $policy.test_file_suggestions.detection_patterns -RuleName "detection" -DefaultReason "matches test file detection pattern")
      $testIgnoreRules = @(Read-PolicyStringRules -PathValue $policy.test_file_suggestions.suggest_ignore -RuleName "suggest_ignore" -DefaultReason "temporary test artifact should not be committed/pushed")
      $testTrackRules = @(Read-PolicyStringRules -PathValue $policy.test_file_suggestions.suggest_track -RuleName "suggest_track" -DefaultReason "persistent test asset should be versioned")
      $testReviewRules = @(Read-PolicyStringRules -PathValue $policy.test_file_suggestions.suggest_review_required -RuleName "suggest_review_required" -DefaultReason "test file requires explicit owner judgement before commit/push")
    }
  }
}

$stagedPaths = @()
$pendingPaths = @()
$outgoingPaths = @()
$outgoingNote = "skipped_by_scope"
switch ($Scope) {
  "staged" {
    $stagedPaths = @(Get-StagedPaths -Repo $repo)
  }
  "pending" {
    $pendingPaths = @(Get-PendingPaths -Repo $repo)
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
foreach ($p in @($stagedPaths + $pendingPaths + $outgoingPaths)) {
  if (-not [string]::IsNullOrWhiteSpace($p)) {
    [void]$pathSet.Add(($p -replace '\\', '/'))
  }
}
$effectivePaths = @($pathSet)

$mustIgnoreHits = @(Find-RuleHits -Paths $effectivePaths -Rules $mustIgnoreRules)
$reviewHits = @(Find-RuleHits -Paths $effectivePaths -Rules $reviewRules)
$mustTrackHits = @(Find-RuleHits -Paths $effectivePaths -Rules $mustTrackRules)
$testFileSuggestions = @()
if ($testSuggestionEnabled -and $testDetectionRules.Count -gt 0) {
  $testFileSuggestions = @(Resolve-TestFileSuggestions -Paths $effectivePaths -DetectionRules $testDetectionRules -IgnoreRules $testIgnoreRules -TrackRules $testTrackRules -ReviewRules $testReviewRules)
}

$blockedByMustIgnore = $blockMustIgnore -and $mustIgnoreHits.Count -gt 0
$blockedByReview = $blockReview -and $reviewHits.Count -gt 0
$testReviewCount = @($testFileSuggestions | Where-Object { $_.suggested_action -eq "review_required" }).Count
$blockedByTestReview = $blockTestFileReview -and $testReviewCount -gt 0
$blocked = $blockedByMustIgnore -or $blockedByReview -or $blockedByTestReview

$result = [pscustomobject]@{
  repo = ($repo -replace '\\', '/')
  policy_path = ($policyFull -replace '\\', '/')
  policy_found = $policyFound
  scope = $Scope
  staged_count = $stagedPaths.Count
  pending_count = $pendingPaths.Count
  outgoing_count = $outgoingPaths.Count
  outgoing_note = $outgoingNote
  checked_paths_count = $effectivePaths.Count
  must_ignore_hits = @($mustIgnoreHits)
  review_required_hits = @($reviewHits)
  must_track_hits = @($mustTrackHits)
  test_file_suggestions = @($testFileSuggestions)
  test_file_suggestion_summary = [pscustomobject]@{
    total = @($testFileSuggestions).Count
    ignore = @($testFileSuggestions | Where-Object { $_.suggested_action -eq "ignore" }).Count
    track = @($testFileSuggestions | Where-Object { $_.suggested_action -eq "track" }).Count
    review_required = $testReviewCount
  }
  blocked = $blocked
  blocked_by = @(
    if ($blockedByMustIgnore) { "must_ignore" }
    if ($blockedByReview) { "review_required" }
    if ($blockedByTestReview) { "test_file_review_required" }
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
Write-Host ("tracked_files.test_file_suggestions=" + $result.test_file_suggestion_summary.total)
Write-Host ("tracked_files.test_file_suggestions.ignore=" + $result.test_file_suggestion_summary.ignore)
Write-Host ("tracked_files.test_file_suggestions.track=" + $result.test_file_suggestion_summary.track)
Write-Host ("tracked_files.test_file_suggestions.review_required=" + $result.test_file_suggestion_summary.review_required)

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

if (@($testFileSuggestions).Count -gt 0) {
  foreach ($item in @($testFileSuggestions)) {
    Write-Host ("[TEST_FILE_SUGGEST] " + $item.path + " | action=" + $item.suggested_action + " | pattern=" + $item.matched_pattern + " | reason=" + $item.reason)
  }
}

if ($blocked) {
  Write-Host ("[BLOCK] tracked files policy violated. blocked_by=" + (@($result.blocked_by) -join ","))
  exit 2
}

Write-Host "[PASS] tracked files policy check passed"
exit 0



