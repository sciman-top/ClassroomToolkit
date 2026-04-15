param(
  [string]$GovernanceRoot = ".",
  [switch]$Diagnostics,
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$commonPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\lib\common.ps1"))
if (Test-Path -LiteralPath $commonPath -PathType Leaf) {
  . $commonPath
}

function Resolve-NormalizedPath([string]$PathText) {
  if ([string]::IsNullOrWhiteSpace($PathText)) { return "" }
  $raw = [string]$PathText
  $hasTemplateToken = $raw -match '\$\{(WORKSPACE_ROOT|USERPROFILE)\}|\$WORKSPACE_ROOT|\$USERPROFILE|%WORKSPACE_ROOT%|%USERPROFILE%'
  function Get-FallbackWorkspaceRoot {
    $envRoots = @(
      $env:WORKSPACE_ROOT,
      $env:CODE_ROOT,
      $env:REPO_WORKSPACE_ROOT
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) }
    foreach ($candidate in @($envRoots)) {
      try {
        return ([System.IO.Path]::GetFullPath(($candidate -replace '/', '\')) -replace '\\', '/').TrimEnd('/')
      } catch {}
    }
    try {
      $governanceResolved = [System.IO.Path]::GetFullPath(([string]$GovernanceRoot -replace '/', '\'))
      $governanceParent = Split-Path -Parent $governanceResolved
      if (-not [string]::IsNullOrWhiteSpace($governanceParent)) {
        return ([System.IO.Path]::GetFullPath($governanceParent) -replace '\\', '/').TrimEnd('/')
      }
    } catch {}
    try {
      $kitRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
      $kitParent = Split-Path -Parent $kitRoot
      if (-not [string]::IsNullOrWhiteSpace($kitParent)) {
        return ([System.IO.Path]::GetFullPath($kitParent) -replace '\\', '/').TrimEnd('/')
      }
    } catch {}
    return ""
  }
  function Resolve-WithoutWorkspaceHelper([string]$InputPath) {
    $expanded = [string]$InputPath
    $workspaceRoot = Get-FallbackWorkspaceRoot
    if (-not [string]::IsNullOrWhiteSpace($workspaceRoot)) {
      $expanded = $expanded.Replace('${WORKSPACE_ROOT}', $workspaceRoot).Replace('$WORKSPACE_ROOT', $workspaceRoot).Replace('%WORKSPACE_ROOT%', $workspaceRoot)
    }
    $userProfileRoot = @(
      $env:USERPROFILE,
      [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::UserProfile)
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } | Select-Object -First 1
    if (-not [string]::IsNullOrWhiteSpace([string]$userProfileRoot)) {
      $userProfileNorm = ([System.IO.Path]::GetFullPath(([string]$userProfileRoot -replace '/', '\')) -replace '\\', '/').TrimEnd('/')
      $expanded = $expanded.Replace('${USERPROFILE}', $userProfileNorm).Replace('$USERPROFILE', $userProfileNorm).Replace('%USERPROFILE%', $userProfileNorm)
    }
    $candidate = $expanded -replace '/', '\'
    if ([System.IO.Path]::IsPathRooted($candidate)) {
      return ([System.IO.Path]::GetFullPath($candidate) -replace '\\', '/').TrimEnd('/')
    }
    return ([System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $candidate)) -replace '\\', '/').TrimEnd('/')
  }
  try {
    if ($hasTemplateToken -and (Get-Command -Name Resolve-WorkspacePath -ErrorAction SilentlyContinue)) {
      return (Resolve-WorkspacePath -PathText $raw) -replace '\\', '/'
    }
    return (Resolve-WithoutWorkspaceHelper -InputPath $raw)
  } catch {
    if (Get-Command -Name Resolve-WorkspacePath -ErrorAction SilentlyContinue) {
      return (Resolve-WorkspacePath -PathText $raw) -replace '\\', '/'
    }
    return (Resolve-WithoutWorkspaceHelper -InputPath $raw)
  }
}

function Ensure-ParentDirectory([string]$PathText) {
  $parent = Split-Path -Parent $PathText
  if ([string]::IsNullOrWhiteSpace($parent)) { return }
  if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
  }
}

function ConvertTo-Slug([string]$Text, [int]$MaxLength = 48) {
  if ([string]::IsNullOrWhiteSpace($Text)) { return "candidate" }
  $slug = ($Text.ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-')
  if ([string]::IsNullOrWhiteSpace($slug)) { return "candidate" }
  if ($slug.Length -gt $MaxLength) {
    $parts = @($slug -split '-' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $builder = ""
    foreach ($part in $parts) {
      $candidate = if ([string]::IsNullOrWhiteSpace($builder)) { $part } else { "$builder-$part" }
      if ($candidate.Length -le $MaxLength) {
        $builder = $candidate
      } else {
        break
      }
    }
    if (-not [string]::IsNullOrWhiteSpace($builder)) {
      $slug = $builder
    } else {
      $slug = $slug.Substring(0, $MaxLength).Trim('-')
    }
  }
  if ([string]::IsNullOrWhiteSpace($slug)) { return "candidate" }
  return $slug
}

function Get-SignatureSlugSeed([string]$Family) {
  $seed = if ($null -eq $Family) { "" } else { $Family.Trim().ToLowerInvariant() }
  if ([string]::IsNullOrWhiteSpace($seed)) { return "" }
  $withoutDate = ($seed -replace '-\d{8}$', '').Trim('-')
  if ([string]::IsNullOrWhiteSpace($withoutDate)) { return $seed }
  return $withoutDate
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
  return ("custom-auto-{0}" -f $slug)
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

function Get-ManualOverrideBindings([psobject]$Policy) {
  $items = New-Object System.Collections.Generic.List[object]
  if ($null -eq $Policy) { return @($items.ToArray()) }
  if ($null -eq $Policy.PSObject.Properties['manual_override_bindings']) { return @($items.ToArray()) }
  foreach ($raw in @($Policy.manual_override_bindings)) {
    if ($null -eq $raw) { continue }
    $pattern = ""
    $skillName = ""
    if ($null -ne $raw.PSObject.Properties['signature_pattern']) {
      $pattern = [string]$raw.signature_pattern
    }
    if ($null -ne $raw.PSObject.Properties['skill_name']) {
      $skillName = [string]$raw.skill_name
    }
    if ([string]::IsNullOrWhiteSpace($pattern) -or [string]::IsNullOrWhiteSpace($skillName)) { continue }
    $items.Add([pscustomobject]@{
      signature_pattern = $pattern
      skill_name = $skillName.Trim()
    }) | Out-Null
  }
  return @($items.ToArray())
}

function Get-MatchedManualOverrideBinding([string]$Signature, [object]$Bindings) {
  $value = if ($null -eq $Signature) { "" } else { $Signature.Trim().ToLowerInvariant() }
  if ([string]::IsNullOrWhiteSpace($value)) { return $null }
  foreach ($binding in @($Bindings)) {
    if ($null -eq $binding) { continue }
    $pattern = ""
    try { $pattern = [string]$binding.signature_pattern } catch { $pattern = "" }
    if ([string]::IsNullOrWhiteSpace($pattern)) { continue }
    try {
      if ($value -match $pattern) {
        return $binding
      }
    } catch {
      continue
    }
  }
  return $null
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
    skills_root = '${WORKSPACE_ROOT}/skills-manager'
    skills_manager_root = ""
    overrides_relative_path = "overrides"
    overrides_source_root = ""
    overrides_source_relative_path = ""
    auto_run_skills_manager_gates = $true
    collapse_suffix_pattern = "^(.*-\d{8})-[a-z]$"
    exclude_signature_patterns = @(
      "(?i)^autopilot-utf8-smoke"
    )
    manual_override_bindings = @()
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
    require_adversarial_eval_for_create = $false
    trigger_eval_min_adversarial_validation_pass_rate = 0.60
    trigger_eval_max_adversarial_validation_false_trigger_rate = 0.35
    block_create_when_adversarial_missing = $true
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

function Get-ExistingFamilyMapFromOverrides([string]$OverridesRoot, [string]$CollapsePattern, [object]$ExcludePatterns) {
  $lookup = @{}
  if (-not (Test-Path -LiteralPath $OverridesRoot -PathType Container)) { return $lookup }
  foreach ($dir in @(Get-ChildItem -LiteralPath $OverridesRoot -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "custom-auto-*" })) {
    $skillFile = Join-Path $dir.FullName "SKILL.md"
    $family = Get-SkillSignatureFromSkillFile -SkillFile $skillFile -CollapsePattern $CollapsePattern -ExcludePatterns $ExcludePatterns
    if ([string]::IsNullOrWhiteSpace($family)) { continue }
    $key = $family.Trim().ToLowerInvariant()
    if (-not $lookup.ContainsKey($key)) {
      $lookup[$key] = [pscustomobject]@{
        issue_signature = $family
        skill_name = [string]$dir.Name
        promoted_at = ""
        hit_count = 0
        repos = @()
        signature_variants = @($family)
        source = "overrides"
      }
    }
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

function Try-RefreshTriggerEvalSummary {
  param(
    [Parameter(Mandatory = $true)][string]$KitRoot,
    [Parameter(Mandatory = $true)][string]$TargetRepoRoot,
    [Parameter(Mandatory = $true)][string]$OutputRelativePath
  )

  $state = [ordered]@{
    attempted = $false
    succeeded = $false
    status = ""
    exit_code = $null
    generated_at = ""
    error = ""
    script_path = ""
  }

  $scriptPath = Join-Path ($KitRoot -replace '/', '\') "scripts\governance\check-skill-trigger-evals.ps1"
  $state.script_path = ($scriptPath -replace '\\', '/')
  if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
    $state.error = "check_skill_trigger_evals_script_missing"
    return [pscustomobject]$state
  }

  $state.attempted = $true
  $psExe = "powershell"
  $pwshCmd = Get-Command -Name "pwsh" -ErrorAction SilentlyContinue
  if ($null -ne $pwshCmd) { $psExe = $pwshCmd.Source }

  try {
    $rawOutput = & $psExe -NoProfile -ExecutionPolicy Bypass -File $scriptPath -RepoRoot $TargetRepoRoot -OutputRelativePath $OutputRelativePath -AsJson 2>&1
    $state.exit_code = [int]$LASTEXITCODE
    if ([int]$state.exit_code -ne 0) {
      $state.error = "check_skill_trigger_evals_failed"
      return [pscustomobject]$state
    }
    $jsonText = (@($rawOutput | ForEach-Object { [string]$_ }) -join "`n").Trim()
    if ([string]::IsNullOrWhiteSpace($jsonText)) {
      $state.error = "check_skill_trigger_evals_empty_output"
      return [pscustomobject]$state
    }
    $parsed = $jsonText | ConvertFrom-Json
    if ($null -ne $parsed -and $null -ne $parsed.PSObject.Properties['status']) {
      $state.status = [string]$parsed.status
    }
    if ($null -ne $parsed -and $null -ne $parsed.PSObject.Properties['generated_at']) {
      $state.generated_at = [string]$parsed.generated_at
    }
    $state.succeeded = $true
  } catch {
    $state.error = [string]$_.Exception.Message
  }

  return [pscustomobject]$state
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
  $requireAdversarialEval = $false
  if ($null -ne $Policy.PSObject.Properties['require_adversarial_eval_for_create']) {
    $requireAdversarialEval = [bool]$Policy.require_adversarial_eval_for_create
  }
  $minAdversarialPassRate = 0.60
  if ($null -ne $Policy.PSObject.Properties['trigger_eval_min_adversarial_validation_pass_rate']) {
    $minAdversarialPassRate = [double]$Policy.trigger_eval_min_adversarial_validation_pass_rate
  }
  $maxAdversarialFalseRate = 0.35
  if ($null -ne $Policy.PSObject.Properties['trigger_eval_max_adversarial_validation_false_trigger_rate']) {
    $maxAdversarialFalseRate = [double]$Policy.trigger_eval_max_adversarial_validation_false_trigger_rate
  }
  $blockWhenMissing = $true
  if ($null -ne $Policy.PSObject.Properties['block_create_when_eval_missing']) {
    $blockWhenMissing = [bool]$Policy.block_create_when_eval_missing
  }
  $blockWhenAdversarialMissing = $true
  if ($null -ne $Policy.PSObject.Properties['block_create_when_adversarial_missing']) {
    $blockWhenAdversarialMissing = [bool]$Policy.block_create_when_adversarial_missing
  }

  $state = [ordered]@{
    require_trigger_eval_for_create = [bool]$requireEval
    require_adversarial_eval_for_create = [bool]$requireAdversarialEval
    trigger_eval_summary_path = ($summaryPath -replace '\\', '/')
    trigger_eval_summary_found = $false
    trigger_eval_summary_status = ""
    trigger_eval_pass = $false
    trigger_eval_min_validation_pass_rate = $minPassRate
    trigger_eval_max_validation_false_trigger_rate = $maxFalseRate
    trigger_eval_min_adversarial_validation_pass_rate = $minAdversarialPassRate
    trigger_eval_max_adversarial_validation_false_trigger_rate = $maxAdversarialFalseRate
    trigger_eval_validation_pass_rate = $null
    trigger_eval_validation_false_trigger_rate = $null
    trigger_eval_adversarial_validation_query_count = 0
    trigger_eval_adversarial_validation_pass_rate = $null
    trigger_eval_adversarial_validation_false_trigger_rate = $null
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
  $avc = 0
  $avp = $null
  $avf = $null
  $summaryStatus = ""
  if ($null -ne $summary) {
    if ($null -ne $summary.PSObject.Properties['status']) {
      $summaryStatus = ([string]$summary.status).Trim().ToLowerInvariant()
    }
    if ($null -ne $summary.PSObject.Properties['validation_pass_rate']) {
      try { $vp = [double]$summary.validation_pass_rate } catch { $vp = $null }
    }
    if ($null -ne $summary.PSObject.Properties['validation_false_trigger_rate']) {
      try { $vf = [double]$summary.validation_false_trigger_rate } catch { $vf = $null }
    }
    if ($null -ne $summary.PSObject.Properties['adversarial_validation_query_count']) {
      try { $avc = [int]$summary.adversarial_validation_query_count } catch { $avc = 0 }
    }
    if ($null -ne $summary.PSObject.Properties['adversarial_validation_pass_rate']) {
      try { $avp = [double]$summary.adversarial_validation_pass_rate } catch { $avp = $null }
    }
    if ($null -ne $summary.PSObject.Properties['adversarial_validation_false_trigger_rate']) {
      try { $avf = [double]$summary.adversarial_validation_false_trigger_rate } catch { $avf = $null }
    }
  }
  $state.trigger_eval_summary_status = $summaryStatus
  $state.trigger_eval_validation_pass_rate = $vp
  $state.trigger_eval_validation_false_trigger_rate = $vf
  $state.trigger_eval_adversarial_validation_query_count = [int]$avc
  $state.trigger_eval_adversarial_validation_pass_rate = $avp
  $state.trigger_eval_adversarial_validation_false_trigger_rate = $avf

  if ($summaryStatus -eq "no_data") {
    $state.trigger_eval_blocked_reason = "eval_summary_no_data"
    $state.trigger_eval_pass = $false
    return [pscustomobject]$state
  }
  if ($summaryStatus -eq "no_validation_split") {
    $state.trigger_eval_blocked_reason = "eval_summary_no_validation_split"
    $state.trigger_eval_pass = $false
    return [pscustomobject]$state
  }

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

  if ($requireAdversarialEval) {
    if ([int]$avc -le 0) {
      if ($blockWhenAdversarialMissing) {
        $state.trigger_eval_blocked_reason = "eval_summary_no_adversarial_validation_split"
        $state.trigger_eval_pass = $false
        return [pscustomobject]$state
      }
    } else {
      if ($null -eq $avp -or $null -eq $avf) {
        $state.trigger_eval_blocked_reason = "adversarial_eval_missing_metrics"
        $state.trigger_eval_pass = $false
        return [pscustomobject]$state
      }
      if ($avp -lt $minAdversarialPassRate) {
        $state.trigger_eval_blocked_reason = "adversarial_validation_pass_rate_below_threshold"
        $state.trigger_eval_pass = $false
        return [pscustomobject]$state
      }
      if ($avf -gt $maxAdversarialFalseRate) {
        $state.trigger_eval_blocked_reason = "adversarial_validation_false_trigger_rate_above_threshold"
        $state.trigger_eval_pass = $false
        return [pscustomobject]$state
      }
    }
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

$kitRoot = Resolve-NormalizedPath $GovernanceRoot
$policyPath = Join-Path ($kitRoot -replace '/', '\') ".governance\skill-promotion-policy.json"
$policyTemplatePath = Join-Path ($kitRoot -replace '/', '\') "source\project\_common\custom\.governance\skill-promotion-policy.json"

$policy = New-DefaultPolicy
$policy = Merge-Policy -Base $policy -Candidate (Load-JsonObject $policyTemplatePath)
$policy = Merge-Policy -Base $policy -Candidate (Load-JsonObject $policyPath)
$collapsePattern = [string]$policy.collapse_suffix_pattern
$excludePatterns = @($policy.exclude_signature_patterns)
$manualOverrideBindings = @(Get-ManualOverrideBindings -Policy $policy)

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
$repos = @(
  (Normalize-RepoList (Get-Content -LiteralPath $reposPath -Raw | ConvertFrom-Json)) |
  ForEach-Object { Resolve-NormalizedPath ([string]$_) } |
  Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) }
)
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
  $repoWin = $repoText -replace '/', '\'
  if (-not (Test-Path -LiteralPath $repoWin -PathType Container)) { continue }
  $eventPath = Join-Path $repoWin (($policy.event_relative_path -replace '/', '\'))
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
$skillsRoot = Resolve-NormalizedPath $policy.skills_root
$skillsManagerRoot = $skillsRoot
if ($null -ne $policy.PSObject.Properties['skills_manager_root']) {
  $candidateSkillsManagerRoot = [string]$policy.skills_manager_root
  if (-not [string]::IsNullOrWhiteSpace($candidateSkillsManagerRoot)) {
    $skillsManagerRoot = Resolve-NormalizedPath $candidateSkillsManagerRoot
  }
}
$overridesSourceRoot = $skillsRoot
if ($null -ne $policy.PSObject.Properties['overrides_source_root']) {
  $candidateOverridesSourceRoot = [string]$policy.overrides_source_root
  if (-not [string]::IsNullOrWhiteSpace($candidateOverridesSourceRoot)) {
    $overridesSourceRoot = Resolve-NormalizedPath $candidateOverridesSourceRoot
  }
}
$overridesRelativePath = [string]$policy.overrides_relative_path
if ($null -ne $policy.PSObject.Properties['overrides_source_relative_path']) {
  $candidateOverridesRelativePath = [string]$policy.overrides_source_relative_path
  if (-not [string]::IsNullOrWhiteSpace($candidateOverridesRelativePath)) {
    $overridesRelativePath = $candidateOverridesRelativePath
  }
}
$triggerEvalSummaryRelativePath = [string]$policy.trigger_eval_summary_relative_path
if ([string]::IsNullOrWhiteSpace($triggerEvalSummaryRelativePath)) {
  $triggerEvalSummaryRelativePath = ".governance/skill-candidates/trigger-eval-summary.json"
}
$triggerEvalRefreshState = [pscustomobject]@{
  attempted = $false
  succeeded = $false
  status = ""
  exit_code = $null
  generated_at = ""
  error = ""
  script_path = ""
}
$requireEvalForCreate = $false
if ($null -ne $policy.PSObject.Properties['require_trigger_eval_for_create']) {
  $requireEvalForCreate = [bool]$policy.require_trigger_eval_for_create
}
if ($requireEvalForCreate) {
  $triggerEvalRefreshState = Try-RefreshTriggerEvalSummary -KitRoot $kitRoot -TargetRepoRoot $skillsManagerRoot -OutputRelativePath $triggerEvalSummaryRelativePath
}
$triggerEvalState = Get-TriggerEvalGateState -KitRoot $skillsManagerRoot -Policy $policy
$overridesRoot = Join-Path ($overridesSourceRoot -replace '/', '\') (($overridesRelativePath -replace '/', '\'))
$overridesFamilyLookup = Get-ExistingFamilyMapFromOverrides -OverridesRoot $overridesRoot -CollapsePattern $collapsePattern -ExcludePatterns $excludePatterns
$knownExistingFamilies = @{}
foreach ($k in @($promotedLookup.Keys)) {
  $knownExistingFamilies[$k] = $promotedLookup[$k]
}
foreach ($k in @($overridesFamilyLookup.Keys)) {
  if (-not $knownExistingFamilies.ContainsKey($k)) {
    $knownExistingFamilies[$k] = $overridesFamilyLookup[$k]
  }
}

$actionable = New-Object System.Collections.Generic.List[object]
$decisionAudit = New-Object System.Collections.Generic.List[object]
foreach ($key in $groupMap.Keys) {
  $item = $groupMap[$key]
  if ([int]$item.count -lt $threshold) { continue }
  $family = [string]$item.issue_signature
  $canonical = Get-CanonicalSkillName $family
  $repoCount = @($item.repos | Sort-Object).Count
  $manualBinding = Get-MatchedManualOverrideBinding -Signature $family -Bindings $manualOverrideBindings
  if ($null -ne $manualBinding) {
    $boundSkill = [string]$manualBinding.skill_name
    $decisionAudit.Add([pscustomobject]@{
      action = "skip"
      issue_signature = $family
      skill_name = $boundSkill
      hit_count = [int]$item.count
      unique_repo_count = [int]$repoCount
      reason_codes = @(("manual_override_binding:" + $boundSkill))
    }) | Out-Null
    continue
  }

  if (-not $knownExistingFamilies.ContainsKey($key)) {
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

  $existing = $knownExistingFamilies[$key]
  $existingSource = "registry"
  if ($null -ne $existing.PSObject.Properties['source'] -and -not [string]::IsNullOrWhiteSpace([string]$existing.source)) {
    $existingSource = [string]$existing.source
  }
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
    if ($existingSource -eq "overrides") { $reasons.Add("existing_family_detected_in_overrides") | Out-Null }
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
    $skipReasons = New-Object System.Collections.Generic.List[string]
    if ($existingSource -eq "overrides") { $skipReasons.Add("existing_family_detected_in_overrides") | Out-Null }
    $skipReasons.Add("no_material_delta") | Out-Null
    $decisionAudit.Add([pscustomobject]@{
      action = "skip"
      issue_signature = $family
      skill_name = $canonical
      hit_count = [int]$item.count
      unique_repo_count = [int]$repoCount
      reason_codes = @($skipReasons)
    }) | Out-Null
  }
}

$selected = @($actionable | Sort-Object -Property @{Expression="count";Descending=$true}, @{Expression="latest_event";Descending=$true} | Select-Object -First $maxPromotions)
$summaryPath = Join-Path ($skillsManagerRoot -replace '/', '\') (($policy.summary_relative_path -replace '/', '\'))
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
    trigger_eval_summary_refresh_attempted = [bool]$triggerEvalRefreshState.attempted
    trigger_eval_summary_refresh_succeeded = [bool]$triggerEvalRefreshState.succeeded
    trigger_eval_summary_refresh_status = [string]$triggerEvalRefreshState.status
    trigger_eval_summary_refresh_exit_code = $triggerEvalRefreshState.exit_code
    trigger_eval_summary_refresh_generated_at = [string]$triggerEvalRefreshState.generated_at
    trigger_eval_summary_refresh_error = [string]$triggerEvalRefreshState.error
    trigger_eval_summary_refresh_script_path = [string]$triggerEvalRefreshState.script_path
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
  $candidateId = "{0}-{1}" -f (ConvertTo-Slug $signature), (Get-Date -Format "yyyyMMdd")
  $triggerEvalSummaryRef = if ($null -ne $triggerEvalState -and -not [string]::IsNullOrWhiteSpace([string]$triggerEvalState.trigger_eval_summary_path)) { [string]$triggerEvalState.trigger_eval_summary_path } else { ".governance/skill-candidates/trigger-eval-summary.json" }
  $sourceMaterialRefs = @()
  foreach ($variant in @($signatureVariants)) {
    if (-not [string]::IsNullOrWhiteSpace([string]$variant)) {
      $sourceMaterialRefs += ("issue_signature:{0}" -f [string]$variant)
    }
  }
  if ($sourceMaterialRefs.Count -eq 0) {
    $sourceMaterialRefs = @("issue_signature:" + $signature)
  }
  $correctionLayerRef = if ([string]$item.action -eq "optimize") { "pending://manual-correction" } else { "none://initial-version" }
  $versionArchiveRef = ("overrides/{0}/SKILL.md" -f $skillName)
  $rollbackRef = ("git restore {0}" -f ($skillFile -replace '\\','/'))
  $skillContent = Build-SkillContent -SkillName $skillName -Signature $signature -Count ([int]$item.count) -Repos $reposUsed -Variants $signatureVariants
  Set-Content -LiteralPath $skillFile -Encoding utf8 -Value $skillContent

  $registryRecord = [pscustomobject]@{
    action = [string]$item.action
    candidate_id = $candidateId
    issue_signature = $signature
    family_signature = $signature
    skill_name = $skillName
    promoted_at = $now.ToString("o")
    hit_count = [int]$item.count
    repos = $reposUsed
    signature_variants = $signatureVariants
    source_material_refs = @($sourceMaterialRefs)
    trigger_eval_summary = $triggerEvalSummaryRef
    correction_layer_ref = $correctionLayerRef
    version_archive_ref = $versionArchiveRef
    rollback_ref = $rollbackRef
  }

  $existing = @($registry.promoted | Where-Object { ([string]$_.issue_signature).ToLowerInvariant() -eq $signature })
  if ($existing.Count -gt 0) {
    foreach ($entry in $existing) {
      $entry.skill_name = $registryRecord.skill_name
      $entry.promoted_at = $registryRecord.promoted_at
      $entry.hit_count = $registryRecord.hit_count
      $entry.repos = $registryRecord.repos
      $entry.signature_variants = $registryRecord.signature_variants
      $entry.candidate_id = $registryRecord.candidate_id
      $entry.family_signature = $registryRecord.family_signature
      $entry.source_material_refs = $registryRecord.source_material_refs
      $entry.trigger_eval_summary = $registryRecord.trigger_eval_summary
      $entry.correction_layer_ref = $registryRecord.correction_layer_ref
      $entry.version_archive_ref = $registryRecord.version_archive_ref
      $entry.rollback_ref = $registryRecord.rollback_ref
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
$cleanupRenamed = New-Object System.Collections.Generic.List[string]
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
    if ($name -ne $canonical) {
      $canonicalPath = Join-Path $overridesRoot $canonical
      if (-not (Test-Path -LiteralPath $canonicalPath -PathType Container)) {
        Rename-Item -LiteralPath $dir.FullName -NewName $canonical -Force
        $cleanupRenamed.Add((("{0} -> {1}") -f ($dir.FullName -replace '\\', '/'), ($canonicalPath -replace '\\', '/'))) | Out-Null
        if ($registryFamilies.ContainsKey($skillFamily)) {
          $registryFamilies[$skillFamily] = $canonical
        }
        continue
      }
      $remove = $true
    }
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
  Invoke-SkillsManagerGates -SkillsRoot $skillsManagerRoot
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
  cleanup_renamed_count = [int]$cleanupRenamed.Count
  gates_ran = [bool]$gatesRan
  require_user_ack = [bool]$requireAck
  user_ack_satisfied = [bool]$ackSatisfied
  user_ack_env_var = $ackEnvVar
  create_min_unique_repos = [int]$createMinUniqueRepos
  optimize_min_new_variants = [int]$optimizeMinNewVariants
  require_trigger_eval_for_create = [bool]$triggerEvalState.require_trigger_eval_for_create
  require_adversarial_eval_for_create = [bool]$triggerEvalState.require_adversarial_eval_for_create
  trigger_eval_summary_path = [string]$triggerEvalState.trigger_eval_summary_path
  trigger_eval_summary_found = [bool]$triggerEvalState.trigger_eval_summary_found
  trigger_eval_summary_status = [string]$triggerEvalState.trigger_eval_summary_status
  trigger_eval_pass = [bool]$triggerEvalState.trigger_eval_pass
  trigger_eval_min_validation_pass_rate = [double]$triggerEvalState.trigger_eval_min_validation_pass_rate
  trigger_eval_max_validation_false_trigger_rate = [double]$triggerEvalState.trigger_eval_max_validation_false_trigger_rate
  trigger_eval_min_adversarial_validation_pass_rate = [double]$triggerEvalState.trigger_eval_min_adversarial_validation_pass_rate
  trigger_eval_max_adversarial_validation_false_trigger_rate = [double]$triggerEvalState.trigger_eval_max_adversarial_validation_false_trigger_rate
  trigger_eval_validation_pass_rate = $triggerEvalState.trigger_eval_validation_pass_rate
  trigger_eval_validation_false_trigger_rate = $triggerEvalState.trigger_eval_validation_false_trigger_rate
  trigger_eval_adversarial_validation_query_count = [int]$triggerEvalState.trigger_eval_adversarial_validation_query_count
  trigger_eval_adversarial_validation_pass_rate = $triggerEvalState.trigger_eval_adversarial_validation_pass_rate
  trigger_eval_adversarial_validation_false_trigger_rate = $triggerEvalState.trigger_eval_adversarial_validation_false_trigger_rate
  trigger_eval_blocked_reason = [string]$triggerEvalState.trigger_eval_blocked_reason
  trigger_eval_summary_refresh_attempted = [bool]$triggerEvalRefreshState.attempted
  trigger_eval_summary_refresh_succeeded = [bool]$triggerEvalRefreshState.succeeded
  trigger_eval_summary_refresh_status = [string]$triggerEvalRefreshState.status
  trigger_eval_summary_refresh_exit_code = $triggerEvalRefreshState.exit_code
  trigger_eval_summary_refresh_generated_at = [string]$triggerEvalRefreshState.generated_at
  trigger_eval_summary_refresh_error = [string]$triggerEvalRefreshState.error
  trigger_eval_summary_refresh_script_path = [string]$triggerEvalRefreshState.script_path
  skills_root = [string]$skillsRoot
  skills_manager_root = [string]$skillsManagerRoot
  overrides_source_root = [string]$overridesSourceRoot
  overrides_source_relative_path = [string]$overridesRelativePath
  overrides_root = ($overridesRoot -replace '\\', '/')
  decision_audit = @($decisionAudit.ToArray())
  planned_promotions = $plannedPromotions
  promoted = $promotedArray
  cleanup_renamed = @($cleanupRenamed.ToArray())
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
  Write-Host ("skill_promotion.cleanup_renamed_count={0}" -f $cleanupRenamed.Count)
  Write-Host ("skill_promotion.cleanup_removed_count={0}" -f $cleanupRemoved.Count)
  Write-Host ("skill_promotion.gates_ran={0}" -f $gatesRan)
  foreach ($p in @($promotedItems)) {
    Write-Host ("[APPLIED] action={0} signature={1} skill={2} hit_count={3}" -f $p.action, $p.issue_signature, $p.skill_name, $p.hit_count)
  }
  foreach ($c in @($cleanupRemoved)) {
    Write-Host ("[CLEANUP] removed={0}" -f $c)
  }
  foreach ($r in @($cleanupRenamed)) {
    Write-Host ("[CLEANUP] renamed={0}" -f $r)
  }
}
