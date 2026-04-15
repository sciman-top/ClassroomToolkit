param(
  [string]$RepoRoot = ".",
  [string]$LifecyclePolicyRelativePath = ".governance/skill-lifecycle-policy.json",
  [string]$PromotionPolicyRelativePath = ".governance/skill-promotion-policy.json",
  [string]$RegistryRelativePath = ".governance/skill-candidates/promotion-registry.json",
  [ValidateSet("plan", "safe")]
  [string]$Mode = "plan",
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-NormalizedPath([string]$PathText) {
  $resolved = Resolve-Path -LiteralPath $PathText -ErrorAction Stop
  return ([System.IO.Path]::GetFullPath($resolved.Path) -replace '\\', '/').TrimEnd('/')
}

function Ensure-ParentDirectory([string]$PathText) {
  $parent = Split-Path -Parent $PathText
  if ([string]::IsNullOrWhiteSpace($parent)) { return }
  if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
  }
}

function Load-JsonObject([string]$PathText, [object]$DefaultValue) {
  if (-not (Test-Path -LiteralPath $PathText -PathType Leaf)) { return $DefaultValue }
  $raw = Get-Content -LiteralPath $PathText -Raw -Encoding utf8
  if ([string]::IsNullOrWhiteSpace($raw)) { return $DefaultValue }
  return ($raw | ConvertFrom-Json)
}

function Get-DateOrNull([object]$Value) {
  if ($null -eq $Value) { return $null }
  $text = [string]$Value
  if ([string]::IsNullOrWhiteSpace($text)) { return $null }
  try { return [datetime]$text } catch { return $null }
}

function Ensure-StringArray([object]$Value) {
  if ($null -eq $Value) { return @() }
  return @($Value | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } | ForEach-Object { ([string]$_).Trim() } | Sort-Object -Unique)
}

function Get-TokenSet([string]$Text) {
  $set = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
  if ([string]::IsNullOrWhiteSpace($Text)) { return $set }
  $normalized = ($Text.ToLowerInvariant() -replace "[^a-z0-9]+", " ").Trim()
  if ([string]::IsNullOrWhiteSpace($normalized)) { return $set }
  foreach ($token in @($normalized.Split(" ", [System.StringSplitOptions]::RemoveEmptyEntries))) {
    if (-not [string]::IsNullOrWhiteSpace($token)) { $set.Add($token) | Out-Null }
  }
  return $set
}

function Get-JaccardSimilarity([string]$A, [string]$B) {
  $left = Get-TokenSet $A
  $right = Get-TokenSet $B
  if ($left.Count -eq 0 -and $right.Count -eq 0) { return 0.0 }
  $union = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
  foreach ($t in @($left)) { $union.Add($t) | Out-Null }
  foreach ($t in @($right)) { $union.Add($t) | Out-Null }
  if ($union.Count -le 0) { return 0.0 }
  $intersection = 0
  foreach ($t in @($left)) {
    if ($right.Contains($t)) { $intersection++ }
  }
  return [Math]::Round(([double]$intersection / [double]$union.Count), 6)
}

function Ensure-RegistryEntry([psobject]$Entry) {
  if ($null -eq $Entry.PSObject.Properties['issue_signature']) { $Entry | Add-Member -NotePropertyName issue_signature -NotePropertyValue "" }
  $issue = [string]$Entry.issue_signature

  if ($null -eq $Entry.PSObject.Properties['family_signature'] -or [string]::IsNullOrWhiteSpace([string]$Entry.family_signature)) {
    if ($null -eq $Entry.PSObject.Properties['family_signature']) {
      $Entry | Add-Member -NotePropertyName family_signature -NotePropertyValue $issue
    } else {
      $Entry.family_signature = $issue
    }
  }
  if ($null -eq $Entry.PSObject.Properties['lifecycle_state'] -or [string]::IsNullOrWhiteSpace([string]$Entry.lifecycle_state)) {
    if ($null -eq $Entry.PSObject.Properties['lifecycle_state']) {
      $Entry | Add-Member -NotePropertyName lifecycle_state -NotePropertyValue "active"
    } else {
      $Entry.lifecycle_state = "active"
    }
  }
  if ($null -eq $Entry.PSObject.Properties['invocation_count']) {
    $hit = 0
    try { $hit = [int]$Entry.hit_count } catch { $hit = 0 }
    $Entry | Add-Member -NotePropertyName invocation_count -NotePropertyValue $hit
  }
  if ($null -eq $Entry.PSObject.Properties['merged_from']) {
    $Entry | Add-Member -NotePropertyName merged_from -NotePropertyValue @()
  } else {
    $Entry.merged_from = @(Ensure-StringArray $Entry.merged_from)
  }
  if ($null -eq $Entry.PSObject.Properties['signature_variants']) {
    $Entry | Add-Member -NotePropertyName signature_variants -NotePropertyValue @()
  } else {
    $Entry.signature_variants = @(Ensure-StringArray $Entry.signature_variants)
  }
}

function Get-PrimaryEntry([psobject]$Left, [psobject]$Right) {
  $leftInv = 0
  $rightInv = 0
  try { $leftInv = [int]$Left.invocation_count } catch { $leftInv = 0 }
  try { $rightInv = [int]$Right.invocation_count } catch { $rightInv = 0 }
  if ($leftInv -gt $rightInv) { return @($Left, $Right) }
  if ($rightInv -gt $leftInv) { return @($Right, $Left) }

  $leftDate = $null
  if ($null -ne $Left.PSObject.Properties['last_invoked_at']) {
    $leftDate = Get-DateOrNull $Left.last_invoked_at
  }
  if ($null -eq $leftDate -and $null -ne $Left.PSObject.Properties['promoted_at']) {
    $leftDate = Get-DateOrNull $Left.promoted_at
  }

  $rightDate = $null
  if ($null -ne $Right.PSObject.Properties['last_invoked_at']) {
    $rightDate = Get-DateOrNull $Right.last_invoked_at
  }
  if ($null -eq $rightDate -and $null -ne $Right.PSObject.Properties['promoted_at']) {
    $rightDate = Get-DateOrNull $Right.promoted_at
  }

  if ($null -eq $leftDate -and $null -eq $rightDate) { return @($Left, $Right) }
  if ($null -eq $leftDate) { return @($Right, $Left) }
  if ($null -eq $rightDate) { return @($Left, $Right) }
  if ($leftDate -ge $rightDate) { return @($Left, $Right) }
  return @($Right, $Left)
}

$repoPath = Resolve-NormalizedPath $RepoRoot
$lifecyclePolicyPath = Join-Path ($repoPath -replace '/', '\') ($LifecyclePolicyRelativePath -replace '/', '\')
$promotionPolicyPath = Join-Path ($repoPath -replace '/', '\') ($PromotionPolicyRelativePath -replace '/', '\')
$registryPath = Join-Path ($repoPath -replace '/', '\') ($RegistryRelativePath -replace '/', '\')

$lifecyclePolicy = Load-JsonObject -PathText $lifecyclePolicyPath -DefaultValue $null
if ($null -eq $lifecyclePolicy) {
  $lifecyclePolicy = [pscustomobject]@{
    enabled = $true
    actions = [pscustomobject]@{
      merge = [pscustomobject]@{ enabled = $true; similarity_threshold = 0.8 }
      retire = [pscustomobject]@{ enabled = $true; inactive_days = 60; min_invocations = 3 }
    }
  }
}
$promotionPolicy = Load-JsonObject -PathText $promotionPolicyPath -DefaultValue ([pscustomobject]@{})
$registry = Load-JsonObject -PathText $registryPath -DefaultValue $null
if ($null -eq $registry) {
  $registry = [pscustomobject]@{
    schema_version = "2.0"
    registry_schema_version = 2
    lifecycle_version = "1.0"
    promoted = @()
  }
}
if ($null -eq $registry.PSObject.Properties['promoted']) {
  $registry | Add-Member -NotePropertyName promoted -NotePropertyValue @()
}

foreach ($entry in @($registry.promoted)) {
  if ($null -eq $entry) { continue }
  Ensure-RegistryEntry -Entry $entry
}

$now = Get-Date
$lifecycleEnabled = $true
if ($null -ne $lifecyclePolicy.PSObject.Properties['enabled']) { $lifecycleEnabled = [bool]$lifecyclePolicy.enabled }

$mergeEnabled = $false
if ($lifecycleEnabled -and $null -ne $lifecyclePolicy.PSObject.Properties['actions'] -and $null -ne $lifecyclePolicy.actions.PSObject.Properties['merge']) {
  $mergeEnabled = [bool]$lifecyclePolicy.actions.merge.enabled
}
$retireEnabled = $false
if ($lifecycleEnabled -and $null -ne $lifecyclePolicy.PSObject.Properties['actions'] -and $null -ne $lifecyclePolicy.actions.PSObject.Properties['retire']) {
  $retireEnabled = [bool]$lifecyclePolicy.actions.retire.enabled
}

$mergeThreshold = 0.8
if ($mergeEnabled -and $null -ne $lifecyclePolicy.actions.merge.PSObject.Properties['similarity_threshold']) {
  $mergeThreshold = [double]$lifecyclePolicy.actions.merge.similarity_threshold
}
if ($null -ne $promotionPolicy.PSObject.Properties['merge_similarity_threshold']) {
  $mergeThreshold = [double]$promotionPolicy.merge_similarity_threshold
}

$retireInactiveDays = 60
if ($retireEnabled -and $null -ne $lifecyclePolicy.actions.retire.PSObject.Properties['inactive_days']) {
  $retireInactiveDays = [int]$lifecyclePolicy.actions.retire.inactive_days
}
if ($null -ne $promotionPolicy.PSObject.Properties['retire_inactive_days']) {
  $retireInactiveDays = [int]$promotionPolicy.retire_inactive_days
}

$retireMinInvocations = 3
if ($retireEnabled -and $null -ne $lifecyclePolicy.actions.retire.PSObject.Properties['min_invocations']) {
  $retireMinInvocations = [int]$lifecyclePolicy.actions.retire.min_invocations
}
if ($null -ne $promotionPolicy.PSObject.Properties['retire_min_invocations']) {
  $retireMinInvocations = [int]$promotionPolicy.retire_min_invocations
}

$retireRequireReplacementCoverage = $false
$retireMinimumActiveReplacements = 1
$retireRequireRollbackFallback = $false
if ($retireEnabled -and $null -ne $lifecyclePolicy.actions.retire.PSObject.Properties['require_replacement_coverage']) {
  $retireRequireReplacementCoverage = [bool]$lifecyclePolicy.actions.retire.require_replacement_coverage
}
if ($retireEnabled -and $null -ne $lifecyclePolicy.actions.retire.PSObject.Properties['minimum_active_replacements']) {
  try { $retireMinimumActiveReplacements = [int]$lifecyclePolicy.actions.retire.minimum_active_replacements } catch { $retireMinimumActiveReplacements = 1 }
}
if ($retireEnabled -and $null -ne $lifecyclePolicy.actions.retire.PSObject.Properties['require_rollback_fallback']) {
  $retireRequireRollbackFallback = [bool]$lifecyclePolicy.actions.retire.require_rollback_fallback
}

$eligibleForMerge = @($registry.promoted | Where-Object {
  $state = ([string]$_.lifecycle_state).Trim().ToLowerInvariant()
  ($state -eq "active" -or $state -eq "approved") -and -not [string]::IsNullOrWhiteSpace([string]$_.family_signature)
})

$mergePairs = New-Object System.Collections.Generic.List[object]
if ($mergeEnabled) {
  for ($i = 0; $i -lt $eligibleForMerge.Count; $i++) {
    for ($j = $i + 1; $j -lt $eligibleForMerge.Count; $j++) {
      $left = $eligibleForMerge[$i]
      $right = $eligibleForMerge[$j]
      if ([string]::Equals([string]$left.family_signature, [string]$right.family_signature, [System.StringComparison]::OrdinalIgnoreCase)) { continue }
      $similarity = Get-JaccardSimilarity -A ([string]$left.family_signature) -B ([string]$right.family_signature)
      if ($similarity -lt $mergeThreshold) { continue }
      $ordered = Get-PrimaryEntry -Left $left -Right $right
      $primary = $ordered[0]
      $secondary = $ordered[1]
      $mergePairs.Add([pscustomobject]@{
        primary_family = [string]$primary.family_signature
        primary_skill = [string]$primary.skill_name
        secondary_family = [string]$secondary.family_signature
        secondary_skill = [string]$secondary.skill_name
        similarity = [double]$similarity
      }) | Out-Null
    }
  }
}

$selectedMerges = New-Object System.Collections.Generic.List[object]
$reservedSecondary = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
foreach ($pair in @($mergePairs | Sort-Object -Property @{ Expression = "similarity"; Descending = $true })) {
  $secondary = [string]$pair.secondary_family
  if ([string]::IsNullOrWhiteSpace($secondary)) { continue }
  if ($reservedSecondary.Contains($secondary)) { continue }
  $reservedSecondary.Add($secondary) | Out-Null
  $selectedMerges.Add($pair) | Out-Null
}

$retireCandidates = New-Object System.Collections.Generic.List[object]
$retireBlockedCandidates = New-Object System.Collections.Generic.List[object]
$mergePrimaryFamilies = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
$mergeSecondaryFamilies = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
foreach ($m in @($selectedMerges.ToArray())) {
  $mergePrimaryFamilies.Add([string]$m.primary_family) | Out-Null
  $mergeSecondaryFamilies.Add([string]$m.secondary_family) | Out-Null
}

$activeFamilies = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
foreach ($entry in @($registry.promoted)) {
  if ($null -eq $entry) { continue }
  $family = [string]$entry.family_signature
  if ([string]::IsNullOrWhiteSpace($family)) { continue }
  $state = ([string]$entry.lifecycle_state).Trim().ToLowerInvariant()
  if ($state -eq "active" -or $state -eq "approved") {
    $activeFamilies.Add($family) | Out-Null
  }
}

if ($retireEnabled) {
  foreach ($entry in @($registry.promoted)) {
    if ($null -eq $entry) { continue }
    $family = [string]$entry.family_signature
    if ([string]::IsNullOrWhiteSpace($family)) { continue }
    if ($mergePrimaryFamilies.Contains($family) -or $mergeSecondaryFamilies.Contains($family)) { continue }

    $state = ([string]$entry.lifecycle_state).Trim().ToLowerInvariant()
    if ($state -ne "active" -and $state -ne "approved") { continue }

    $invocations = 0
    try { $invocations = [int]$entry.invocation_count } catch { $invocations = 0 }
    $last = $null
    if ($null -ne $entry.PSObject.Properties['last_invoked_at']) { $last = Get-DateOrNull $entry.last_invoked_at }
    if ($null -eq $last -and $null -ne $entry.PSObject.Properties['last_optimized_at']) { $last = Get-DateOrNull $entry.last_optimized_at }
    if ($null -eq $last -and $null -ne $entry.PSObject.Properties['promoted_at']) { $last = Get-DateOrNull $entry.promoted_at }
    if ($null -eq $last) { continue }

    $daysInactive = [int][Math]::Floor((New-TimeSpan -Start $last -End $now).TotalDays)
    if ($daysInactive -lt $retireInactiveDays) { continue }
    if ($invocations -gt $retireMinInvocations) { continue }

    $replacementFamilySource = @()
    if ($null -ne $entry.PSObject.Properties['replacement_families']) {
      $replacementFamilySource = $entry.replacement_families
    }
    $replacementFamilies = @(Ensure-StringArray $replacementFamilySource | Where-Object { -not [string]::Equals([string]$_, $family, [System.StringComparison]::OrdinalIgnoreCase) })
    $replacementActiveCount = 0
    foreach ($rf in $replacementFamilies) {
      if ($activeFamilies.Contains([string]$rf)) { $replacementActiveCount++ }
    }
    $replacementCoverageProven = ($replacementActiveCount -ge [Math]::Max(1, [int]$retireMinimumActiveReplacements))
    $rollbackFallback = ""
    if ($null -ne $entry.PSObject.Properties['rollback_fallback']) {
      $rollbackFallback = ([string]$entry.rollback_fallback).Trim()
    }
    $rollbackFallbackReady = -not [string]::IsNullOrWhiteSpace($rollbackFallback)

    $blockReasons = New-Object System.Collections.Generic.List[string]
    if ($retireRequireReplacementCoverage -and -not $replacementCoverageProven) {
      [void]$blockReasons.Add(("replacement_coverage_insufficient:{0}/{1}" -f [int]$replacementActiveCount, [int][Math]::Max(1, [int]$retireMinimumActiveReplacements)))
    }
    if ($retireRequireRollbackFallback -and -not $rollbackFallbackReady) {
      [void]$blockReasons.Add("rollback_fallback_missing")
    }
    if ($blockReasons.Count -gt 0) {
      $retireBlockedCandidates.Add([pscustomobject]@{
        family_signature = $family
        skill_name = [string]$entry.skill_name
        block_reasons = @($blockReasons.ToArray())
        replacement_active_count = [int]$replacementActiveCount
        replacement_families = @($replacementFamilies)
        rollback_fallback = $rollbackFallback
      }) | Out-Null
      continue
    }

    $retireCandidates.Add([pscustomobject]@{
      family_signature = $family
      skill_name = [string]$entry.skill_name
      lifecycle_state = [string]$entry.lifecycle_state
      invocation_count = [int]$invocations
      days_inactive = [int]$daysInactive
      last_activity_at = $last.ToString("o")
      replacement_coverage_proven = [bool]$replacementCoverageProven
      replacement_active_count = [int]$replacementActiveCount
      replacement_families = @($replacementFamilies)
      rollback_fallback = $rollbackFallback
    }) | Out-Null
  }
}

$appliedMergeCount = 0
$appliedRetireCount = 0
if ($Mode -eq "safe") {
  $entryByFamily = @{}
  foreach ($entry in @($registry.promoted)) {
    if ($null -eq $entry) { continue }
    $family = [string]$entry.family_signature
    if ([string]::IsNullOrWhiteSpace($family)) { continue }
    $entryByFamily[$family.ToLowerInvariant()] = $entry
  }

  foreach ($pair in @($selectedMerges.ToArray())) {
    $primaryKey = ([string]$pair.primary_family).ToLowerInvariant()
    $secondaryKey = ([string]$pair.secondary_family).ToLowerInvariant()
    if (-not $entryByFamily.ContainsKey($primaryKey)) { continue }
    if (-not $entryByFamily.ContainsKey($secondaryKey)) { continue }
    $primary = $entryByFamily[$primaryKey]
    $secondary = $entryByFamily[$secondaryKey]

    $primaryVariants = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($v in @(Ensure-StringArray $primary.signature_variants)) { $primaryVariants.Add($v) | Out-Null }
    foreach ($v in @(Ensure-StringArray $secondary.signature_variants)) { $primaryVariants.Add($v) | Out-Null }
    $primaryVariants.Add([string]$primary.family_signature) | Out-Null
    $primaryVariants.Add([string]$secondary.family_signature) | Out-Null
    $primary.signature_variants = @($primaryVariants | Sort-Object)

    $primaryMergedFrom = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($v in @(Ensure-StringArray $primary.merged_from)) { $primaryMergedFrom.Add($v) | Out-Null }
    $primaryMergedFrom.Add([string]$secondary.family_signature) | Out-Null
    $primary.merged_from = @($primaryMergedFrom | Sort-Object)

    try { $primary.hit_count = [int]$primary.hit_count + [int]$secondary.hit_count } catch {}
    try { $primary.invocation_count = [int]$primary.invocation_count + [int]$secondary.invocation_count } catch {}
    if ($null -eq $primary.PSObject.Properties['last_optimized_at']) {
      $primary | Add-Member -NotePropertyName last_optimized_at -NotePropertyValue $now.ToString("o")
    } else {
      $primary.last_optimized_at = $now.ToString("o")
    }
    $primary.lifecycle_state = "active"

    $secondary.lifecycle_state = "deprecated"
    if ($null -eq $secondary.PSObject.Properties['deprecated_at']) {
      $secondary | Add-Member -NotePropertyName deprecated_at -NotePropertyValue $now.ToString("o")
    } else {
      $secondary.deprecated_at = $now.ToString("o")
    }
    if ($null -eq $secondary.PSObject.Properties['deprecated_reason']) {
      $secondary | Add-Member -NotePropertyName deprecated_reason -NotePropertyValue ("merged_into:" + [string]$primary.family_signature)
    } else {
      $secondary.deprecated_reason = ("merged_into:" + [string]$primary.family_signature)
    }
    if ($null -eq $secondary.PSObject.Properties['merged_into']) {
      $secondary | Add-Member -NotePropertyName merged_into -NotePropertyValue ([string]$primary.family_signature)
    } else {
      $secondary.merged_into = [string]$primary.family_signature
    }
    if ($null -eq $secondary.PSObject.Properties['last_optimized_at']) {
      $secondary | Add-Member -NotePropertyName last_optimized_at -NotePropertyValue $now.ToString("o")
    } else {
      $secondary.last_optimized_at = $now.ToString("o")
    }
    $appliedMergeCount++
  }

  $retireIndex = @{}
  foreach ($r in @($retireCandidates.ToArray())) {
    $retireIndex[([string]$r.family_signature).ToLowerInvariant()] = $r
  }
  foreach ($entry in @($registry.promoted)) {
    if ($null -eq $entry) { continue }
    $family = [string]$entry.family_signature
    if ([string]::IsNullOrWhiteSpace($family)) { continue }
    $key = $family.ToLowerInvariant()
    if (-not $retireIndex.ContainsKey($key)) { continue }
    $entry.lifecycle_state = "retired"
    if ($null -eq $entry.PSObject.Properties['retired_at']) {
      $entry | Add-Member -NotePropertyName retired_at -NotePropertyValue $now.ToString("o")
    } else {
      $entry.retired_at = $now.ToString("o")
    }
    if ($null -eq $entry.PSObject.Properties['retired_reason']) {
      $entry | Add-Member -NotePropertyName retired_reason -NotePropertyValue ("inactive:" + [string]$retireIndex[$key].days_inactive + "d")
    } else {
      $entry.retired_reason = ("inactive:" + [string]$retireIndex[$key].days_inactive + "d")
    }
    if ($null -eq $entry.PSObject.Properties['retired_replacement_evidence']) {
      $entry | Add-Member -NotePropertyName retired_replacement_evidence -NotePropertyValue ("active_count=" + [string]$retireIndex[$key].replacement_active_count + ";families=" + ((@($retireIndex[$key].replacement_families) -join ",")))
    } else {
      $entry.retired_replacement_evidence = ("active_count=" + [string]$retireIndex[$key].replacement_active_count + ";families=" + ((@($retireIndex[$key].replacement_families) -join ",")))
    }
    if ($null -eq $entry.PSObject.Properties['retired_rollback_fallback']) {
      $entry | Add-Member -NotePropertyName retired_rollback_fallback -NotePropertyValue ([string]$retireIndex[$key].rollback_fallback)
    } else {
      $entry.retired_rollback_fallback = [string]$retireIndex[$key].rollback_fallback
    }
    $appliedRetireCount++
  }

  if ($null -eq $registry.PSObject.Properties['updated_at']) {
    $registry | Add-Member -NotePropertyName updated_at -NotePropertyValue $now.ToString("o")
  } else {
    $registry.updated_at = $now.ToString("o")
  }
  Ensure-ParentDirectory $registryPath
  $registry | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $registryPath -Encoding utf8
}

$result = [ordered]@{
  schema_version = "1.0"
  status = "ok"
  mode = $Mode
  repo_root = $repoPath
  lifecycle_policy_path = ($lifecyclePolicyPath -replace '\\', '/')
  promotion_policy_path = ($promotionPolicyPath -replace '\\', '/')
  registry_path = ($registryPath -replace '\\', '/')
  lifecycle_enabled = [bool]$lifecycleEnabled
  merge_enabled = [bool]$mergeEnabled
  retire_enabled = [bool]$retireEnabled
  merge_similarity_threshold = [double]$mergeThreshold
  retire_inactive_days = [int]$retireInactiveDays
  retire_min_invocations = [int]$retireMinInvocations
  retire_require_replacement_coverage = [bool]$retireRequireReplacementCoverage
  retire_minimum_active_replacements = [int][Math]::Max(1, [int]$retireMinimumActiveReplacements)
  retire_require_rollback_fallback = [bool]$retireRequireRollbackFallback
  merge_candidate_count = [int]$selectedMerges.Count
  retire_candidate_count = [int]$retireCandidates.Count
  retire_blocked_candidate_count = [int]$retireBlockedCandidates.Count
  applied_merge_count = [int]$appliedMergeCount
  applied_retire_count = [int]$appliedRetireCount
  merge_candidates = @($selectedMerges.ToArray())
  retire_candidates = @($retireCandidates.ToArray())
  retire_blocked_candidates = @($retireBlockedCandidates.ToArray())
}

if ($AsJson) {
  $result | ConvertTo-Json -Depth 12 | Write-Output
} else {
  Write-Host ("skill_lifecycle.mode={0}" -f $Mode)
  Write-Host ("skill_lifecycle.merge_candidate_count={0}" -f [int]$selectedMerges.Count)
  Write-Host ("skill_lifecycle.retire_candidate_count={0}" -f [int]$retireCandidates.Count)
  Write-Host ("skill_lifecycle.applied_merge_count={0}" -f [int]$appliedMergeCount)
  Write-Host ("skill_lifecycle.applied_retire_count={0}" -f [int]$appliedRetireCount)
  Write-Host ("skill_lifecycle.registry_path={0}" -f ($registryPath -replace '\\', '/'))
}
