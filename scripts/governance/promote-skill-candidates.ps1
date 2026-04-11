param(
  [string]$GovernanceKitRoot = ".",
  [switch]$Diagnostics,
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

function ConvertTo-Slug([string]$Text) {
  if ([string]::IsNullOrWhiteSpace($Text)) { return "candidate" }
  $slug = ($Text.ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-')
  if ([string]::IsNullOrWhiteSpace($slug)) { return "candidate" }
  if ($slug.Length -gt 24) { $slug = $slug.Substring(0, 24).Trim('-') }
  if ([string]::IsNullOrWhiteSpace($slug)) { return "candidate" }
  return $slug
}

function New-UnicodeString([int[]]$CodePoints) {
  if ($null -eq $CodePoints -or $CodePoints.Count -eq 0) { return "" }
  return -join ($CodePoints | ForEach-Object { [char]$_ })
}

function Get-SignatureHash8([string]$Text) {
  $input = if ($null -eq $Text) { "" } else { $Text.ToLowerInvariant().Trim() }
  if ([string]::IsNullOrWhiteSpace($input)) { return "00000000" }
  $sha = [System.Security.Cryptography.SHA256]::Create()
  try {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($input)
    $hashBytes = $sha.ComputeHash($bytes)
    $hex = [System.BitConverter]::ToString($hashBytes).Replace("-", "").ToLowerInvariant()
    return $hex.Substring(0, 8)
  } finally {
    $sha.Dispose()
  }
}

function Get-CanonicalSkillName([string]$Signature) {
  $family = Get-SignatureFamily -Signature $Signature
  $slug = ConvertTo-Slug $family
  $hash8 = Get-SignatureHash8 $family
  return ("custom-auto-{0}-{1}" -f $slug, $hash8)
}

function Get-SignatureFamily([string]$Signature, [string]$CollapsePattern = "^(.*-\d{8})-[a-z]$") {
  $raw = if ($null -eq $Signature) { "" } else { $Signature.Trim().ToLowerInvariant() }
  if ([string]::IsNullOrWhiteSpace($raw)) { return "" }
  if (-not [string]::IsNullOrWhiteSpace($CollapsePattern) -and $raw -match $CollapsePattern) {
    $base = [string]$Matches[1]
    if (-not [string]::IsNullOrWhiteSpace($base)) { return $base.Trim().ToLowerInvariant() }
  }
  return $raw
}

function Test-SignatureExcluded([string]$Signature, [object]$Patterns) {
  $value = if ($null -eq $Signature) { "" } else { $Signature.Trim().ToLowerInvariant() }
  if ([string]::IsNullOrWhiteSpace($value)) { return $false }
  foreach ($patternObj in @($Patterns)) {
    $pattern = [string]$patternObj
    if ([string]::IsNullOrWhiteSpace($pattern)) { continue }
    try {
      if ($value -match $pattern) { return $true }
    } catch {
      continue
    }
  }
  return $false
}

function New-DefaultPolicy {
  return [pscustomobject]@{
    schema_version = "1.0"
    enabled = $true
    threshold_count = 3
    window_days = 14
    cooldown_days = 14
    max_promotions_per_run = 3
    event_relative_path = ".governance/skill-candidates/events.jsonl"
    registry_relative_path = ".governance/skill-candidates/promotion-registry.json"
    skills_root = "E:/CODE/skills-manager"
    overrides_relative_path = "overrides"
    auto_run_skills_manager_gates = $true
    collapse_suffix_pattern = "^(.*-\d{8})-[a-z]$"
    exclude_signature_patterns = @(
      "(?i)^autopilot-utf8-smoke"
    )
    summary_relative_path = ".governance/skill-candidates/last-promotion-summary.json"
    write_summary_file = $true
    require_user_ack = $true
    optimize_existing_without_ack = $true
    user_ack_env_var = "SKILL_PROMOTION_ACK"
    user_ack_expected_value = "YES"
    create_min_unique_repos = 2
    optimize_min_new_variants = 1
    require_trigger_eval_for_create = $false
    trigger_eval_summary_relative_path = ".governance/skill-candidates/trigger-eval-summary.json"
    trigger_eval_min_validation_pass_rate = 0.70
    trigger_eval_max_validation_false_trigger_rate = 0.20
    block_create_when_eval_missing = $true
  }
}

function Merge-Policy([psobject]$Base, [psobject]$Candidate) {
  if ($null -eq $Candidate) { return $Base }
  foreach ($prop in $Candidate.PSObject.Properties) {
    if ($null -eq $prop.Value) { continue }
    if ($Base.PSObject.Properties[$prop.Name]) {
      $Base.$($prop.Name) = $prop.Value
    } else {
      $Base | Add-Member -NotePropertyName $prop.Name -NotePropertyValue $prop.Value
    }
  }
  return $Base
}

function Load-JsonObject([string]$PathText) {
  if (-not (Test-Path -LiteralPath $PathText -PathType Leaf)) { return $null }
  return (Get-Content -LiteralPath $PathText -Raw | ConvertFrom-Json)
}

function Load-Registry([string]$PathText) {
  $obj = Load-JsonObject $PathText
  if ($null -eq $obj) {
    return [pscustomobject]@{
      schema_version = "1.0"
      promoted = @()
    }
  }
  if ($null -eq $obj.PSObject.Properties['promoted']) {
    $obj | Add-Member -NotePropertyName promoted -NotePropertyValue @()
  }
  return $obj
}

function Normalize-RepoList([object]$Parsed) {
  if ($null -eq $Parsed) { return @() }
  $list = @($Parsed)
  if ($list.Count -eq 1) {
    $single = $list[0]
    $valueProp = $single.PSObject.Properties['value']
    if ($null -ne $valueProp) {
      $candidate = @($valueProp.Value)
      if ($candidate.Count -gt 0) {
        return $candidate
      }
    }
  }
  return $list
}

function Save-Registry([string]$PathText, [psobject]$Registry) {
  Ensure-ParentDirectory $PathText
  $Registry | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $PathText -Encoding utf8
}

function Get-SkillSignatureFromSkillFile([string]$SkillFile, [string]$CollapsePattern, [object]$ExcludePatterns) {
  if (-not (Test-Path -LiteralPath $SkillFile -PathType Leaf)) { return $null }
  $raw = Get-Content -LiteralPath $SkillFile -Raw -ErrorAction SilentlyContinue
  if ([string]::IsNullOrWhiteSpace($raw)) { return $null }
  $m = [regex]::Match($raw, "Auto-promoted from repeated issue signature '([^']+)'", [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
  if (-not $m.Success) { return $null }
  $sig = [string]$m.Groups[1].Value
  if ([string]::IsNullOrWhiteSpace($sig)) { return $null }
  if (Test-SignatureExcluded -Signature $sig -Patterns $ExcludePatterns) { return $null }
  $family = Get-SignatureFamily -Signature $sig -CollapsePattern $CollapsePattern
  if ([string]::IsNullOrWhiteSpace($family)) { return $null }
  return $family
}

function Merge-RegistryByFamily([psobject]$Registry, [string]$CollapsePattern, [object]$ExcludePatterns) {
  $merged = @{}
  foreach ($item in @($Registry.promoted)) {
    if ($null -eq $item) { continue }
    $rawSig = [string]$item.issue_signature
    if ([string]::IsNullOrWhiteSpace($rawSig)) { continue }
    if (Test-SignatureExcluded -Signature $rawSig -Patterns $ExcludePatterns) { continue }
    $family = Get-SignatureFamily -Signature $rawSig -CollapsePattern $CollapsePattern
    if ([string]::IsNullOrWhiteSpace($family)) { continue }
    $key = $family.ToLowerInvariant()
    if (-not $merged.ContainsKey($key)) {
      $merged[$key] = [ordered]@{
        issue_signature = $family
        skill_name = (Get-CanonicalSkillName $family)
        promoted_at = ""
        hit_count = 0
        repos = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
        signature_variants = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
      }
    }
    $bucket = $merged[$key]
    $bucket.hit_count = [Math]::Max([int]$bucket.hit_count, [int]$item.hit_count)
    foreach ($r in @($item.repos)) {
      $repoText = [string]$r
      if (-not [string]::IsNullOrWhiteSpace($repoText)) { $bucket.repos.Add($repoText) | Out-Null }
    }
    $bucket.signature_variants.Add($rawSig.Trim().ToLowerInvariant()) | Out-Null
    if ($null -ne $item.PSObject.Properties['signature_variants']) {
      foreach ($v in @($item.signature_variants)) {
        $vv = [string]$v
        if (-not [string]::IsNullOrWhiteSpace($vv)) { $bucket.signature_variants.Add($vv.Trim().ToLowerInvariant()) | Out-Null }
      }
    }
    $candidate = ""
    try { $candidate = ([datetime]$item.promoted_at).ToString("o") } catch { $candidate = [string]$item.promoted_at }
    if ([string]::IsNullOrWhiteSpace($bucket.promoted_at)) {
      $bucket.promoted_at = $candidate
    } else {
      $lhs = $null; $rhs = $null
      try { $lhs = [datetime]$bucket.promoted_at } catch { $lhs = $null }
      try { $rhs = [datetime]$candidate } catch { $rhs = $null }
      if ($null -ne $rhs -and ($null -eq $lhs -or $rhs -gt $lhs)) { $bucket.promoted_at = $candidate }
    }
  }

  $result = @()
  foreach ($key in @($merged.Keys | Sort-Object)) {
    $b = $merged[$key]
    $result += [pscustomobject]@{
      issue_signature = [string]$b.issue_signature
      skill_name = [string]$b.skill_name
      promoted_at = [string]$b.promoted_at
      hit_count = [int]$b.hit_count
      repos = @($b.repos | Sort-Object)
      signature_variants = @($b.signature_variants | Sort-Object)
    }
  }

  $Registry.promoted = @($result)
  return $Registry
}

function Read-EventLines([string]$PathText) {
  if (-not (Test-Path -LiteralPath $PathText -PathType Leaf)) { return @() }
  $items = New-Object System.Collections.Generic.List[object]
  foreach ($line in @(Get-Content -LiteralPath $PathText -Encoding utf8)) {
    $text = [string]$line
    if ([string]::IsNullOrWhiteSpace($text)) { continue }
    try {
      $items.Add(($text | ConvertFrom-Json)) | Out-Null
    } catch {
      if ($Diagnostics) {
        Write-Warning ("skill_promotion.parse_error file={0} line={1}" -f ($PathText -replace '\\', '/'), $text.Substring(0, [Math]::Min(120, $text.Length)))
      }
    }
  }
  return @($items.ToArray())
}

function Get-PromotedLookup([psobject]$Registry) {
  $lookup = @{}
  foreach ($item in @($Registry.promoted)) {
    if ($null -eq $item) { continue }
    $signature = [string]$item.issue_signature
    if ([string]::IsNullOrWhiteSpace($signature)) { continue }
    $lookup[$signature.ToLowerInvariant()] = $item
  }
  return $lookup
}

function Build-SkillContent([string]$SkillName, [string]$Signature, [int]$Count, [string[]]$Repos, [string[]]$Variants = @()) {
  $repoText = if ($Repos.Count -gt 0) { ($Repos -join ", ") } else { "unknown-repo" }
  $variantLine = $null
  $cleanVariants = @($Variants | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } | ForEach-Object { ([string]$_).Trim().ToLowerInvariant() } | Sort-Object -Unique)
  if ($cleanVariants.Count -gt 1) {
    $variantLine = ("signature_variants: {0}" -f ($cleanVariants -join ", "))
  }
  @(
    "---",
    ("name: {0}" -f $SkillName),
    ("description: Auto-promoted from repeated issue signature '{0}' (hits={1})." -f $Signature, $Count),
    "---",
    "",
    "1. Reproduce the issue using the latest command/log context in the current repository.",
    "2. Apply the proven fix pattern associated with this signature before adding new abstractions.",
    "3. Verify with repository gates in fixed order: build -> test -> contract/invariant -> hotspot.",
    "4. If the same signature reappears, update this skill and evidence records instead of creating a duplicate skill.",
    "",
    ("source_repos: {0}" -f $repoText),
    $variantLine
  ) -join "`n"
}

function Get-TriggerEvalGateState {
  param(
    [Parameter(Mandatory = $true)][string]$KitRoot,
    [Parameter(Mandatory = $true)][psobject]$Policy
  )

  $requireEval = $false
  if ($null -ne $Policy.PSObject.Properties['require_trigger_eval_for_create']) {
    $requireEval = [bool]$Policy.require_trigger_eval_for_create
  }
  $summaryRel = [string]$Policy.trigger_eval_summary_relative_path
  if ([string]::IsNullOrWhiteSpace($summaryRel)) {
    $summaryRel = ".governance/skill-candidates/trigger-eval-summary.json"
  }
  $summaryPath = Join-Path ($KitRoot -replace '/', '\') ($summaryRel -replace '/', '\')
  $minPassRate = [double]$Policy.trigger_eval_min_validation_pass_rate
  $maxFalseRate = [double]$Policy.trigger_eval_max_validation_false_trigger_rate
  $blockWhenMissing = $true
  if ($null -ne $Policy.PSObject.Properties['block_create_when_eval_missing']) {
    $blockWhenMissing = [bool]$Policy.block_create_when_eval_missing
  }

  $state = [ordered]@{
    require_trigger_eval_for_create = [bool]$requireEval
    trigger_eval_summary_path = ($summaryPath -replace '\\', '/')
    trigger_eval_summary_found = $false
    trigger_eval_pass = $false
    trigger_eval_min_validation_pass_rate = $minPassRate
    trigger_eval_max_validation_false_trigger_rate = $maxFalseRate
    trigger_eval_validation_pass_rate = $null
    trigger_eval_validation_false_trigger_rate = $null
    trigger_eval_blocked_reason = ""
  }

  if (-not $requireEval) {
    $state.trigger_eval_pass = $true
    return [pscustomobject]$state
  }

  if (-not (Test-Path -LiteralPath $summaryPath -PathType Leaf)) {
    $state.trigger_eval_blocked_reason = if ($blockWhenMissing) { "eval_summary_missing" } else { "" }
    $state.trigger_eval_pass = (-not $blockWhenMissing)
    return [pscustomobject]$state
  }

  $state.trigger_eval_summary_found = $true
  $summary = Load-JsonObject $summaryPath
  $vp = $null
  $vf = $null
  if ($null -ne $summary) {
    if ($null -ne $summary.PSObject.Properties['validation_pass_rate']) {
      try { $vp = [double]$summary.validation_pass_rate } catch { $vp = $null }
    }
    if ($null -ne $summary.PSObject.Properties['validation_false_trigger_rate']) {
      try { $vf = [double]$summary.validation_false_trigger_rate } catch { $vf = $null }
    }
  }
  $state.trigger_eval_validation_pass_rate = $vp
  $state.trigger_eval_validation_false_trigger_rate = $vf

  if ($null -eq $vp -or $null -eq $vf) {
    $state.trigger_eval_blocked_reason = "eval_summary_missing_metrics"
    $state.trigger_eval_pass = $false
    return [pscustomobject]$state
  }

  if ($vp -lt $minPassRate) {
    $state.trigger_eval_blocked_reason = "validation_pass_rate_below_threshold"
    $state.trigger_eval_pass = $false
    return [pscustomobject]$state
  }
  if ($vf -gt $maxFalseRate) {
    $state.trigger_eval_blocked_reason = "validation_false_trigger_rate_above_threshold"
    $state.trigger_eval_pass = $false
    return [pscustomobject]$state
  }

  $state.trigger_eval_pass = $true
  return [pscustomobject]$state
}

function Invoke-SkillsManagerGates([string]$SkillsRoot) {
  $skillsScript = Join-Path ($SkillsRoot -replace '/', '\') "skills.ps1"
  if (-not (Test-Path -LiteralPath $skillsScript -PathType Leaf)) {
    throw "skills.ps1 not found: $skillsScript"
  }
  $psExe = "powershell"
  $pwshCmd = Get-Command -Name "pwsh" -ErrorAction SilentlyContinue
  if ($null -ne $pwshCmd) { $psExe = $pwshCmd.Source }

  $cmdDiscover = New-UnicodeString @(0x53D1, 0x73B0) # 发现
  $cmdBuildApply = New-UnicodeString @(0x6784, 0x5EFA, 0x751F, 0x6548) # 构建生效

  Push-Location -LiteralPath ($SkillsRoot -replace '/', '\')
  try {
    & $psExe -NoProfile -ExecutionPolicy Bypass -File $skillsScript "doctor" "--strict"
    if ($LASTEXITCODE -ne 0) { throw "skills.ps1 doctor --strict failed (exit=$LASTEXITCODE)" }
    & $psExe -NoProfile -ExecutionPolicy Bypass -File $skillsScript $cmdBuildApply
    if ($LASTEXITCODE -ne 0) { throw "skills.ps1 build/apply failed (exit=$LASTEXITCODE)" }
    & $psExe -NoProfile -ExecutionPolicy Bypass -File $skillsScript $cmdDiscover | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "skills.ps1 discover failed (exit=$LASTEXITCODE)" }
  } finally {
    Pop-Location
  }
}

$kitRoot = Resolve-NormalizedPath $GovernanceKitRoot
$policyPath = Join-Path ($kitRoot -replace '/', '\') ".governance\skill-promotion-policy.json"
$policyTemplatePath = Join-Path ($kitRoot -replace '/', '\') "source\project\_common\custom\.governance\skill-promotion-policy.json"

$policy = New-DefaultPolicy
$policy = Merge-Policy -Base $policy -Candidate (Load-JsonObject $policyTemplatePath)
$policy = Merge-Policy -Base $policy -Candidate (Load-JsonObject $policyPath)
$collapsePattern = [string]$policy.collapse_suffix_pattern
$excludePatterns = @($policy.exclude_signature_patterns)

if (-not [bool]$policy.enabled) {
  $disabled = [pscustomobject]@{
    schema_version = "1.0"
    status = "disabled"
    policy_path = ($policyPath -replace '\\', '/')
    promoted_count = 0
    promoted = @()
  }
  if ($AsJson) { $disabled | ConvertTo-Json -Depth 8 | Write-Output } else { Write-Host "skill_promotion.status=disabled" }
  exit 0
}

$reposPath = Join-Path ($kitRoot -replace '/', '\') "config\repositories.json"
if (-not (Test-Path -LiteralPath $reposPath -PathType Leaf)) {
  throw "repositories.json not found: $reposPath"
}
$repos = Normalize-RepoList (Get-Content -LiteralPath $reposPath -Raw | ConvertFrom-Json)
if ($Diagnostics) {
  Write-Host ("skill_promotion.repos_count={0}" -f $repos.Count)
  Write-Host ("skill_promotion.repos_type={0}" -f $repos.GetType().FullName)
  Write-Host ("skill_promotion.repos_preview={0}" -f (($repos | ConvertTo-Json -Depth 4 -Compress)))
}

$registryPath = Join-Path ($kitRoot -replace '/', '\') (($policy.registry_relative_path -replace '/', '\'))
$registry = Load-Registry $registryPath
$registry = Merge-RegistryByFamily -Registry $registry -CollapsePattern $collapsePattern -ExcludePatterns $excludePatterns
$promotedLookup = Get-PromotedLookup $registry

$now = Get-Date
$windowStart = $now.AddDays(-1 * [int]$policy.window_days)
$cooldownDays = [int]$policy.cooldown_days
$maxPromotions = [Math]::Max(1, [int]$policy.max_promotions_per_run)
$threshold = [Math]::Max(1, [int]$policy.threshold_count)
$requireAck = [bool]$policy.require_user_ack
$ackEnvVar = [string]$policy.user_ack_env_var
$ackExpected = [string]$policy.user_ack_expected_value
$ackValue = ""
if (-not [string]::IsNullOrWhiteSpace($ackEnvVar)) {
  try { $ackValue = [string][System.Environment]::GetEnvironmentVariable($ackEnvVar) } catch { $ackValue = "" }
}
$ackSatisfied = (-not $requireAck) -or (([string]$ackValue).Trim().ToUpperInvariant() -eq ([string]$ackExpected).Trim().ToUpperInvariant())

$groupMap = @{}
$eventCount = 0
foreach ($repoEntry in @($repos)) {
  $repoText = [string]$repoEntry
  if ([string]::IsNullOrWhiteSpace($repoText)) { continue }
  if (-not (Test-Path -LiteralPath ($repoText -replace '/', '\') -PathType Container)) { continue }
  $eventPath = Join-Path (($repoText -replace '/', '\')) (($policy.event_relative_path -replace '/', '\'))
  if ($Diagnostics) {
    Write-Host ("skill_promotion.scan repo={0} event_path={1} exists={2}" -f ($repoText -replace '\\', '/'), ($eventPath -replace '\\', '/'), (Test-Path -LiteralPath $eventPath -PathType Leaf))
  }
  foreach ($event in @(Read-EventLines $eventPath)) {
    if ($null -eq $event) { continue }
    $rawSignature = [string]$event.issue_signature
    if ([string]::IsNullOrWhiteSpace($rawSignature)) { continue }
    if (Test-SignatureExcluded -Signature $rawSignature -Patterns $excludePatterns) { continue }
    $signature = Get-SignatureFamily -Signature $rawSignature -CollapsePattern $collapsePattern
    if ([string]::IsNullOrWhiteSpace($signature)) { continue }
    $eventTime = $null
    try { $eventTime = [datetime]$event.timestamp } catch { $eventTime = $null }
    if ($null -eq $eventTime -or $eventTime -lt $windowStart) { continue }
    $eventCount++

    $key = $signature.Trim().ToLowerInvariant()
    if (-not $groupMap.ContainsKey($key)) {
      $groupMap[$key] = [pscustomobject]@{
        issue_signature = $key
        count = 0
        repos = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
        raw_signatures = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
        latest_event = $eventTime
      }
    }
    $bucket = $groupMap[$key]
    $bucket.count = [int]$bucket.count + 1
    $bucket.repos.Add(($repoText -replace '\\', '/')) | Out-Null
    $bucket.raw_signatures.Add($rawSignature.Trim().ToLowerInvariant()) | Out-Null
    if ($eventTime -gt [datetime]$bucket.latest_event) {
      $bucket.latest_event = $eventTime
    }
  }
}

if ($Diagnostics) {
  Write-Host ("skill_promotion.scanned_event_count={0}" -f $eventCount)
}

$optimizeWithoutAck = $true
if ($null -ne $policy.PSObject.Properties['optimize_existing_without_ack']) {
  $optimizeWithoutAck = [bool]$policy.optimize_existing_without_ack
}
$createMinUniqueRepos = [Math]::Max(1, [int]$policy.create_min_unique_repos)
$optimizeMinNewVariants = [Math]::Max(1, [int]$policy.optimize_min_new_variants)
$triggerEvalState = Get-TriggerEvalGateState -KitRoot $kitRoot -Policy $policy

$actionable = New-Object System.Collections.Generic.List[object]
$decisionAudit = New-Object System.Collections.Generic.List[object]
foreach ($key in $groupMap.Keys) {
  $item = $groupMap[$key]
  if ([int]$item.count -lt $threshold) { continue }
  $family = [string]$item.issue_signature
  $canonical = Get-CanonicalSkillName $family
  $repoCount = @($item.repos | Sort-Object).Count

  if (-not $promotedLookup.ContainsKey($key)) {
    $blockedReasons = New-Object System.Collections.Generic.List[string]
    if ($repoCount -lt $createMinUniqueRepos) { $blockedReasons.Add("insufficient_repo_diversity") | Out-Null }
    if (-not [bool]$triggerEvalState.trigger_eval_pass) {
      $blockedReasons.Add([string]$triggerEvalState.trigger_eval_blocked_reason) | Out-Null
    }
    if ($blockedReasons.Count -gt 0) {
      $decisionAudit.Add([pscustomobject]@{
        action = "skip"
        issue_signature = $family
        skill_name = $canonical
        hit_count = [int]$item.count
        unique_repo_count = [int]$repoCount
        reason_codes = @($blockedReasons)
      }) | Out-Null
      continue
    }
    $actionable.Add([pscustomobject]@{
      action = "create"
      reason_codes = @("new_family_threshold_met")
      issue_signature = $family
      skill_name = $canonical
      count = [int]$item.count
      latest_event = $item.latest_event
      repos = @($item.repos | Sort-Object)
      raw_signatures = @($item.raw_signatures | Sort-Object)
      unique_repo_count = [int]$repoCount
    }) | Out-Null
    $decisionAudit.Add([pscustomobject]@{
      action = "create"
      issue_signature = $family
      skill_name = $canonical
      hit_count = [int]$item.count
      unique_repo_count = [int]$repoCount
      reason_codes = @("new_family_threshold_met")
    }) | Out-Null
    continue
  }

  $existing = $promotedLookup[$key]
  $lastPromoted = $null
  try { $lastPromoted = [datetime]$existing.promoted_at } catch { $lastPromoted = $null }
  if ($null -ne $lastPromoted -and $cooldownDays -gt 0 -and $lastPromoted -gt $now.AddDays(-1 * $cooldownDays)) {
    # cooldown only blocks create; existing family goes through optimize diff checks
  }

  $prevHit = 0
  try { $prevHit = [int]$existing.hit_count } catch { $prevHit = 0 }
  $prevVariants = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
  $existingSig = [string]$existing.issue_signature
  if (-not [string]::IsNullOrWhiteSpace($existingSig)) { $prevVariants.Add($existingSig.Trim().ToLowerInvariant()) | Out-Null }
  if ($null -ne $existing.PSObject.Properties['signature_variants']) {
    foreach ($v in @($existing.signature_variants)) {
      $vv = [string]$v
      if (-not [string]::IsNullOrWhiteSpace($vv)) { $prevVariants.Add($vv.Trim().ToLowerInvariant()) | Out-Null }
    }
  }
  $newVariants = @()
  foreach ($cur in @($item.raw_signatures | Sort-Object)) {
    $cv = [string]$cur
    if ([string]::IsNullOrWhiteSpace($cv)) { continue }
    $norm = $cv.Trim().ToLowerInvariant()
    if (-not $prevVariants.Contains($norm)) { $newVariants += $norm }
  }
  $countIncreased = ([int]$item.count -gt $prevHit)
  if ($newVariants.Count -ge $optimizeMinNewVariants -or $countIncreased) {
    $reasons = New-Object System.Collections.Generic.List[string]
    if ($newVariants.Count -gt 0) { $reasons.Add("new_signature_variant") | Out-Null }
    if ($countIncreased) { $reasons.Add("hit_count_increased") | Out-Null }
    $actionable.Add([pscustomobject]@{
      action = "optimize"
      reason_codes = @($reasons)
      issue_signature = $family
      skill_name = $canonical
      count = [int]$item.count
      latest_event = $item.latest_event
      repos = @($item.repos | Sort-Object)
      raw_signatures = @($item.raw_signatures | Sort-Object)
      unique_repo_count = [int]$repoCount
    }) | Out-Null
    $decisionAudit.Add([pscustomobject]@{
      action = "optimize"
      issue_signature = $family
      skill_name = $canonical
      hit_count = [int]$item.count
      unique_repo_count = [int]$repoCount
      reason_codes = @($reasons)
    }) | Out-Null
  } else {
    $decisionAudit.Add([pscustomobject]@{
      action = "skip"
      issue_signature = $family
      skill_name = $canonical
      hit_count = [int]$item.count
      unique_repo_count = [int]$repoCount
      reason_codes = @("no_material_delta")
    }) | Out-Null
  }
}

$selected = @($actionable | Sort-Object -Property @{Expression="count";Descending=$true}, @{Expression="latest_event";Descending=$true} | Select-Object -First $maxPromotions)

$skillsRoot = Resolve-NormalizedPath $policy.skills_root
$overridesRoot = Join-Path ($skillsRoot -replace '/', '\') (($policy.overrides_relative_path -replace '/', '\'))
$summaryPath = Join-Path ($skillsRoot -replace '/', '\') (($policy.summary_relative_path -replace '/', '\'))
if (-not (Test-Path -LiteralPath $overridesRoot -PathType Container)) {
  New-Item -ItemType Directory -Force -Path $overridesRoot | Out-Null
}

$plannedPromotions = @($selected | ForEach-Object {
  [pscustomobject]@{
    action = [string]$_.action
    reason_codes = @($_.reason_codes)
    issue_signature = [string]$_.issue_signature
    skill_name = [string]$_.skill_name
    hit_count = [int]$_.count
    unique_repo_count = [int]$_.unique_repo_count
    signature_variants = @($_.raw_signatures | Sort-Object)
  }
})

if (-not $AsJson) {
  Write-Host ("[PLAN] planned_promotions={0}" -f $plannedPromotions.Count)
  foreach ($pp in @($plannedPromotions)) {
    Write-Host ("[PLAN] action={0} signature={1} -> {2} (hits={3})" -f $pp.action, $pp.issue_signature, $pp.skill_name, $pp.hit_count)
  }
}

$selectedCreates = @($selected | Where-Object { ([string]$_.action) -eq "create" })
$selectedOptimizations = @($selected | Where-Object { ([string]$_.action) -eq "optimize" })
$pendingAckCreates = @()
$selectedToApply = @($selected)
if (-not $ackSatisfied -and $selectedCreates.Count -gt 0) {
  $pendingAckCreates = @($selectedCreates)
  if ($optimizeWithoutAck) {
    $selectedToApply = @($selectedOptimizations)
  } else {
    $selectedToApply = @()
  }
}

if (-not $ackSatisfied -and $selectedCreates.Count -gt 0 -and $selectedToApply.Count -eq 0) {
  $ackResult = [ordered]@{
    schema_version = "1.0"
    status = "awaiting_user_ack"
    policy_path = ($policyPath -replace '\\', '/')
    require_user_ack = $true
    user_ack_env_var = $ackEnvVar
    user_ack_expected_value = $ackExpected
    user_ack_received_value = $ackValue
    selected_signature_count = [int]$selected.Count
    promoted_count = 0
    created_count = 0
    optimized_count = 0
    planned_promotions = $plannedPromotions
    blocked_create_count = [int]$selectedCreates.Count
    apply_without_ack_count = 0
  }
  if ([bool]$policy.write_summary_file) {
    Ensure-ParentDirectory $summaryPath
    $ackResult | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $summaryPath -Encoding utf8
  }
  if ($AsJson) { $ackResult | ConvertTo-Json -Depth 10 | Write-Output } else { Write-Host "[BLOCK] awaiting user ack for skill creation" }
  exit 0
}

$promotedItems = New-Object System.Collections.Generic.List[object]
foreach ($item in @($selectedToApply)) {
  $signature = [string]$item.issue_signature
  $skillName = [string]$item.skill_name
  $skillDir = Join-Path $overridesRoot $skillName
  $skillFile = Join-Path $skillDir "SKILL.md"
  if (-not (Test-Path -LiteralPath $skillDir -PathType Container)) {
    New-Item -ItemType Directory -Force -Path $skillDir | Out-Null
  }
  $reposUsed = @($item.repos | Sort-Object)
  $signatureVariants = @($item.raw_signatures | Sort-Object)
  $skillContent = Build-SkillContent -SkillName $skillName -Signature $signature -Count ([int]$item.count) -Repos $reposUsed -Variants $signatureVariants
  Set-Content -LiteralPath $skillFile -Encoding utf8 -Value $skillContent

  $registryRecord = [pscustomobject]@{
    action = [string]$item.action
    issue_signature = $signature
    skill_name = $skillName
    promoted_at = $now.ToString("o")
    hit_count = [int]$item.count
    repos = $reposUsed
    signature_variants = $signatureVariants
  }

  $existing = @($registry.promoted | Where-Object { ([string]$_.issue_signature).ToLowerInvariant() -eq $signature })
  if ($existing.Count -gt 0) {
    foreach ($entry in $existing) {
      $entry.skill_name = $registryRecord.skill_name
      $entry.promoted_at = $registryRecord.promoted_at
      $entry.hit_count = $registryRecord.hit_count
      $entry.repos = $registryRecord.repos
      $entry.signature_variants = $registryRecord.signature_variants
    }
  } else {
    $registry.promoted += $registryRecord
  }

  $promotedItems.Add($registryRecord) | Out-Null
}

foreach ($entry in @($registry.promoted)) {
  if ($null -eq $entry) { continue }
  $family = Get-SignatureFamily -Signature ([string]$entry.issue_signature) -CollapsePattern $collapsePattern
  if ([string]::IsNullOrWhiteSpace($family)) { continue }
  $entry.issue_signature = $family
  $entry.skill_name = Get-CanonicalSkillName $family
}
$registry = Merge-RegistryByFamily -Registry $registry -CollapsePattern $collapsePattern -ExcludePatterns $excludePatterns

$cleanupRemoved = New-Object System.Collections.Generic.List[string]
$registryFamilies = @{}
foreach ($entry in @($registry.promoted)) {
  $registryFamilies[[string]$entry.issue_signature] = [string]$entry.skill_name
}
foreach ($dir in @(Get-ChildItem -LiteralPath $overridesRoot -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "custom-auto-*" })) {
  $name = [string]$dir.Name
  $skillFile = Join-Path $dir.FullName "SKILL.md"
  $skillFamily = Get-SkillSignatureFromSkillFile -SkillFile $skillFile -CollapsePattern $collapsePattern -ExcludePatterns $excludePatterns
  $remove = $false
  if ([string]::IsNullOrWhiteSpace($skillFamily)) {
    if ($name -match "(?i)^custom-auto-autopilot-utf8-smoke") { $remove = $true }
  } else {
    $canonical = Get-CanonicalSkillName $skillFamily
    if ($name -ne $canonical) { $remove = $true }
    elseif ($registryFamilies.ContainsKey($skillFamily) -and $registryFamilies[$skillFamily] -ne $name) { $remove = $true }
  }
  if ($remove) {
    Remove-Item -LiteralPath $dir.FullName -Recurse -Force
    $cleanupRemoved.Add(($dir.FullName -replace '\\', '/')) | Out-Null
  }
}

Save-Registry -PathText $registryPath -Registry $registry

$gatesRan = $false
if ($promotedItems.Count -gt 0 -and [bool]$policy.auto_run_skills_manager_gates) {
  Invoke-SkillsManagerGates -SkillsRoot $skillsRoot
  $gatesRan = $true
}

$eventWindowStartText = ""
try {
  $eventWindowStartText = ([datetime]$windowStart).ToString("o")
} catch {
  $eventWindowStartText = [string]$windowStart
}
$promotedArray = @($promotedItems | ForEach-Object { $_ })
$createAppliedCount = @($promotedItems | Where-Object { ([string]$_.action) -eq "create" }).Count
$optimizeAppliedCount = @($promotedItems | Where-Object { ([string]$_.action) -eq "optimize" }).Count
$blockedCreateCount = @($pendingAckCreates).Count
$applyWithoutAckCount = 0
if ($blockedCreateCount -gt 0) {
  $applyWithoutAckCount = @($selectedToApply | Where-Object { ([string]$_.action) -eq "optimize" }).Count
}
$runStatus = if ($blockedCreateCount -gt 0) { "awaiting_user_ack" } else { "ok" }

$result = [ordered]@{
  schema_version = "1.0"
  status = $runStatus
  policy_path = ($policyPath -replace '\\', '/')
  event_window_start = $eventWindowStartText
  threshold_count = [int]$threshold
  scanned_event_count = [int]$eventCount
  grouped_signature_count = [int]$groupMap.Count
  eligible_signature_count = [int]$actionable.Count
  selected_signature_count = [int]$selected.Count
  promoted_count = [int]$promotedItems.Count
  created_count = [int]$createAppliedCount
  optimized_count = [int]$optimizeAppliedCount
  blocked_create_count = [int]$blockedCreateCount
  apply_without_ack_count = [int]$applyWithoutAckCount
  cleanup_removed_count = [int]$cleanupRemoved.Count
  gates_ran = [bool]$gatesRan
  require_user_ack = [bool]$requireAck
  user_ack_satisfied = [bool]$ackSatisfied
  user_ack_env_var = $ackEnvVar
  create_min_unique_repos = [int]$createMinUniqueRepos
  optimize_min_new_variants = [int]$optimizeMinNewVariants
  require_trigger_eval_for_create = [bool]$triggerEvalState.require_trigger_eval_for_create
  trigger_eval_summary_path = [string]$triggerEvalState.trigger_eval_summary_path
  trigger_eval_summary_found = [bool]$triggerEvalState.trigger_eval_summary_found
  trigger_eval_pass = [bool]$triggerEvalState.trigger_eval_pass
  trigger_eval_min_validation_pass_rate = [double]$triggerEvalState.trigger_eval_min_validation_pass_rate
  trigger_eval_max_validation_false_trigger_rate = [double]$triggerEvalState.trigger_eval_max_validation_false_trigger_rate
  trigger_eval_validation_pass_rate = $triggerEvalState.trigger_eval_validation_pass_rate
  trigger_eval_validation_false_trigger_rate = $triggerEvalState.trigger_eval_validation_false_trigger_rate
  trigger_eval_blocked_reason = [string]$triggerEvalState.trigger_eval_blocked_reason
  skills_root = [string]$skillsRoot
  overrides_root = ($overridesRoot -replace '\\', '/')
  decision_audit = @($decisionAudit.ToArray())
  planned_promotions = $plannedPromotions
  promoted = $promotedArray
  cleanup_removed = @($cleanupRemoved.ToArray())
}

if ([bool]$policy.write_summary_file) {
  Ensure-ParentDirectory $summaryPath
  $result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $summaryPath -Encoding utf8
}

if ($AsJson) {
  $result | ConvertTo-Json -Depth 10 | Write-Output
} else {
  Write-Host ("skill_promotion.promoted_count={0}" -f $promotedItems.Count)
  Write-Host ("skill_promotion.created_count={0}" -f $createAppliedCount)
  Write-Host ("skill_promotion.optimized_count={0}" -f $optimizeAppliedCount)
  Write-Host ("skill_promotion.blocked_create_count={0}" -f $blockedCreateCount)
  Write-Host ("skill_promotion.cleanup_removed_count={0}" -f $cleanupRemoved.Count)
  Write-Host ("skill_promotion.gates_ran={0}" -f $gatesRan)
  foreach ($p in @($promotedItems)) {
    Write-Host ("[APPLIED] action={0} signature={1} skill={2} hit_count={3}" -f $p.action, $p.issue_signature, $p.skill_name, $p.hit_count)
  }
  foreach ($c in @($cleanupRemoved)) {
    Write-Host ("[CLEANUP] removed={0}" -f $c)
  }
}
