param(
  [string]$RepoRoot = (Get-Location).Path,
  [string]$PolicyPath,
  [ValidateSet("lite", "standard", "deep")]
  [string]$TokenBudgetMode,
  [switch]$PolicyOnly,
  [switch]$AsJson
)

$ErrorActionPreference = 'Stop'
if (Get-Variable -Name PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
  $PSNativeCommandUseErrorActionPreference = $false
}

function New-ResultObject {
  param(
    [string]$Status,
    [string]$RepoRootResolved,
    [string]$PolicyPathResolved,
    [string]$TokenBudgetModeResolved,
    [string]$Scope,
    [System.Collections.IList]$ScannedFiles,
    [System.Collections.IList]$Violations,
    [System.Collections.IList]$Warnings,
    [int]$TotalEstimatedTokens,
    [hashtable]$Limits
  )

  return [pscustomobject]@{
    schema_version = '1.0'
    generated_at = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    status = $Status
    repo_root = $RepoRootResolved
    policy_path = $PolicyPathResolved
    token_budget_mode = $TokenBudgetModeResolved
    scope = $Scope
    scanned_file_count = @($ScannedFiles).Count
    total_estimated_tokens = $TotalEstimatedTokens
    limits = $Limits
    warnings = @($Warnings)
    violations = @($Violations)
    files = @($ScannedFiles)
  }
}

function Write-Result {
  param(
    [pscustomobject]$Result,
    [bool]$BlockOnViolation
  )

  $hasViolation = @($Result.violations).Count -gt 0
  if ($AsJson) {
    $Result | ConvertTo-Json -Depth 8 | Write-Output
  } else {
    Write-Host ("anti_bloat.status={0}" -f $Result.status)
    Write-Host ("anti_bloat.token_budget_mode={0}" -f $Result.token_budget_mode)
    Write-Host ("anti_bloat.scope={0}" -f $Result.scope)
    Write-Host ("anti_bloat.scanned_file_count={0}" -f $Result.scanned_file_count)
    Write-Host ("anti_bloat.total_estimated_tokens={0}" -f $Result.total_estimated_tokens)

    foreach ($w in @($Result.warnings)) {
      Write-Host ("[WARN] " + [string]$w)
    }

    foreach ($v in @($Result.violations)) {
      Write-Host ("[VIOLATION] {0} file={1} actual={2} limit={3}" -f $v.type, $v.file, $v.actual, $v.limit)
      if (-not [string]::IsNullOrWhiteSpace([string]$v.suggestion)) {
        Write-Host ("[SUGGEST] " + [string]$v.suggestion)
      }
    }

    if (-not $hasViolation) {
      Write-Host 'anti_bloat.health=PASS'
    } else {
      Write-Host 'anti_bloat.health=FAIL'
    }
  }

  if ($hasViolation -and $BlockOnViolation) {
    exit 1
  }
}

function Resolve-PolicyPath {
  param(
    [string]$Root,
    [string]$PathText
  )

  if ([string]::IsNullOrWhiteSpace($PathText)) {
    return (Join-Path $Root '.governance\anti-bloat-policy.json')
  }

  if ([System.IO.Path]::IsPathRooted($PathText)) {
    return [System.IO.Path]::GetFullPath($PathText)
  }

  return [System.IO.Path]::GetFullPath((Join-Path $Root $PathText))
}

function ConvertTo-Set {
  param([object[]]$Items)
  $set = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
  foreach ($item in @($Items)) {
    $text = ([string]$item).Trim().ToLowerInvariant()
    if (-not [string]::IsNullOrWhiteSpace($text)) {
      if (-not $text.StartsWith('.')) {
        $text = '.' + $text
      }
      [void]$set.Add($text)
    }
  }
  return $set
}

function Normalize-RelPath([string]$PathText) {
  return (($PathText -replace '\\', '/').TrimStart('./')).Trim()
}

function Get-RelativePathCompat {
  param(
    [string]$BasePath,
    [string]$TargetPath
  )

  $baseFull = [System.IO.Path]::GetFullPath($BasePath)
  $targetFull = [System.IO.Path]::GetFullPath($TargetPath)

  if ([System.IO.Path].GetMethods() | Where-Object { $_.Name -eq 'GetRelativePath' }) {
    return [System.IO.Path]::GetRelativePath($baseFull, $targetFull)
  }

  try {
    $baseUri = New-Object System.Uri(($baseFull.TrimEnd('\') + '\'))
    $targetUri = New-Object System.Uri($targetFull)
    $relativeUri = $baseUri.MakeRelativeUri($targetUri)
    return [System.Uri]::UnescapeDataString($relativeUri.ToString()) -replace '/', '\'
  } catch {
    if ($targetFull.StartsWith($baseFull, [System.StringComparison]::OrdinalIgnoreCase)) {
      return $targetFull.Substring($baseFull.Length).TrimStart('\')
    }
    return $targetFull
  }
}

function Test-TokenBudgetModeValue {
  param([string]$Value)
  return @("lite", "standard", "deep") -contains ([string]$Value).Trim().ToLowerInvariant()
}

function Resolve-TokenBudgetMode {
  param(
    [string]$RepoRootResolved,
    [string]$ExplicitMode
  )

  if (Test-TokenBudgetModeValue -Value $ExplicitMode) {
    return ([string]$ExplicitMode).Trim().ToLowerInvariant()
  }

  $envMode = [Environment]::GetEnvironmentVariable("GOVERNANCE_TOKEN_BUDGET_MODE")
  if (-not (Test-TokenBudgetModeValue -Value $envMode)) {
    $envMode = [Environment]::GetEnvironmentVariable("TOKEN_BUDGET_MODE")
  }
  if (Test-TokenBudgetModeValue -Value $envMode) {
    return ([string]$envMode).Trim().ToLowerInvariant()
  }

  $projectRulePolicyPath = Join-Path $RepoRootResolved "config\project-rule-policy.json"
  if (Test-Path -LiteralPath $projectRulePolicyPath -PathType Leaf) {
    try {
      $projectRulePolicy = Get-Content -LiteralPath $projectRulePolicyPath -Raw | ConvertFrom-Json
      $modeFromProjectRule = $null
      if ($null -ne $projectRulePolicy.defaults -and $null -ne $projectRulePolicy.defaults.PSObject.Properties['token_budget_mode']) {
        $modeFromProjectRule = ([string]$projectRulePolicy.defaults.token_budget_mode).Trim().ToLowerInvariant()
      }

      $repoNorm = ($RepoRootResolved -replace '\\', '/').TrimEnd('/')
      $repoName = Split-Path -Leaf $RepoRootResolved
      foreach ($entry in @($projectRulePolicy.repos)) {
        if ($null -eq $entry) { continue }
        $match = $false
        if ($null -ne $entry.PSObject.Properties['repo'] -and -not [string]::IsNullOrWhiteSpace([string]$entry.repo)) {
          $entryRepo = ([string]$entry.repo -replace '\\', '/').TrimEnd('/')
          if ($entryRepo.Equals($repoNorm, [System.StringComparison]::OrdinalIgnoreCase)) {
            $match = $true
          }
        }
        if (-not $match -and $null -ne $entry.PSObject.Properties['repoName'] -and -not [string]::IsNullOrWhiteSpace([string]$entry.repoName)) {
          if (([string]$entry.repoName).Equals($repoName, [System.StringComparison]::OrdinalIgnoreCase)) {
            $match = $true
          }
        }
        if (-not $match) { continue }
        if ($null -ne $entry.PSObject.Properties['token_budget_mode']) {
          $candidate = ([string]$entry.token_budget_mode).Trim().ToLowerInvariant()
          if (Test-TokenBudgetModeValue -Value $candidate) {
            return $candidate
          }
        }
      }

      if (Test-TokenBudgetModeValue -Value $modeFromProjectRule) {
        return $modeFromProjectRule
      }
    } catch {
      # fallback chain continues
    }
  }

  # Gate strictness should not inherit response style defaults (e.g. lite output mode).
  return "standard"
}

function Normalize-RepoText {
  param([string]$PathText)
  if ([string]::IsNullOrWhiteSpace($PathText)) { return "" }
  return (($PathText -replace '\\', '/').Trim().TrimEnd('/'))
}

function Resolve-PolicyRepoOverride {
  param(
    [object]$PolicyObject,
    [string]$RepoRootResolved
  )

  if ($null -eq $PolicyObject -or $null -eq $PolicyObject.PSObject.Properties['repo_overrides']) {
    return $null
  }

  $repoNorm = Normalize-RepoText -PathText $RepoRootResolved
  $repoName = Split-Path -Leaf $RepoRootResolved
  foreach ($entry in @($PolicyObject.repo_overrides)) {
    if ($null -eq $entry) { continue }
    $matched = $false
    if ($null -ne $entry.PSObject.Properties['repo'] -and -not [string]::IsNullOrWhiteSpace([string]$entry.repo)) {
      $entryNorm = Normalize-RepoText -PathText ([string]$entry.repo)
      if ($entryNorm.Equals($repoNorm, [System.StringComparison]::OrdinalIgnoreCase)) {
        $matched = $true
      }
    }
    if (-not $matched -and $null -ne $entry.PSObject.Properties['repoName'] -and -not [string]::IsNullOrWhiteSpace([string]$entry.repoName)) {
      if (([string]$entry.repoName).Equals($repoName, [System.StringComparison]::OrdinalIgnoreCase)) {
        $matched = $true
      }
    }
    if ($matched) {
      return $entry
    }
  }
  return $null
}

function Invoke-GitSilent {
  param(
    [string]$RepoRoot,
    [string]$ArgsText
  )

  $commandText = ('git -C "{0}" {1} 2>nul' -f $RepoRoot, $ArgsText)
  $output = & cmd /c $commandText
  $exitCode = $LASTEXITCODE
  if ($null -eq $exitCode) { $exitCode = 1 }
  return [pscustomobject]@{
    output = @($output)
    exit_code = [int]$exitCode
  }
}

function Test-ExcludedPath {
  param(
    [string]$RelativePath,
    [object[]]$Excludes
  )

  $normalized = Normalize-RelPath $RelativePath
  foreach ($item in @($Excludes)) {
    $prefix = Normalize-RelPath ([string]$item)
    if ([string]::IsNullOrWhiteSpace($prefix)) { continue }
    if ($normalized.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
      return $true
    }
  }
  return $false
}

function Get-MaxConsecutiveNonEmptyLines {
  param([string[]]$Lines)

  $maxBlock = 0
  $current = 0
  foreach ($line in @($Lines)) {
    if ([string]::IsNullOrWhiteSpace([string]$line)) {
      if ($current -gt $maxBlock) { $maxBlock = $current }
      $current = 0
      continue
    }

    $trimmed = ([string]$line).Trim()
    if ($trimmed -eq '{' -or $trimmed -eq '}' -or $trimmed -eq ');') {
      if ($current -gt $maxBlock) { $maxBlock = $current }
      $current = 0
      continue
    }

    $current++
  }
  if ($current -gt $maxBlock) { $maxBlock = $current }
  return $maxBlock
}

function Get-MaxDuplicateNormalizedLineCount {
  param([string[]]$Lines)

  $map = @{}
  $maxCount = 0
  foreach ($line in @($Lines)) {
    $normalized = ([string]$line).Trim()
    if ([string]::IsNullOrWhiteSpace($normalized)) { continue }
    if ($normalized.Length -lt 24) { continue }
    if ($normalized -match '^[\{\}\)\(;,\[\]]+$') { continue }

    if (-not $map.ContainsKey($normalized)) {
      $map[$normalized] = 0
    }
    $map[$normalized] = [int]$map[$normalized] + 1
    if ([int]$map[$normalized] -gt $maxCount) {
      $maxCount = [int]$map[$normalized]
    }
  }

  return $maxCount
}

$repoRootResolved = [System.IO.Path]::GetFullPath($RepoRoot)
$policyPathResolved = Resolve-PolicyPath -Root $repoRootResolved -PathText $PolicyPath
$tokenBudgetModeResolved = Resolve-TokenBudgetMode -RepoRootResolved $repoRootResolved -ExplicitMode $TokenBudgetMode

if (-not (Test-Path -LiteralPath $policyPathResolved -PathType Leaf)) {
  $resultMissingPolicy = New-ResultObject -Status 'FAIL' -RepoRootResolved $repoRootResolved -PolicyPathResolved $policyPathResolved -TokenBudgetModeResolved $tokenBudgetModeResolved -Scope 'none' -ScannedFiles @() -Violations @() -Warnings @("policy file not found: $policyPathResolved") -TotalEstimatedTokens 0 -Limits @{}
  Write-Result -Result $resultMissingPolicy -BlockOnViolation $true
  exit 1
}

try {
  $policy = Get-Content -LiteralPath $policyPathResolved -Raw | ConvertFrom-Json
} catch {
  $resultInvalidPolicy = New-ResultObject -Status 'FAIL' -RepoRootResolved $repoRootResolved -PolicyPathResolved $policyPathResolved -TokenBudgetModeResolved $tokenBudgetModeResolved -Scope 'none' -ScannedFiles @() -Violations @() -Warnings @("invalid policy json: $policyPathResolved") -TotalEstimatedTokens 0 -Limits @{}
  Write-Result -Result $resultInvalidPolicy -BlockOnViolation $true
  exit 1
}

$repoOverride = Resolve-PolicyRepoOverride -PolicyObject $policy -RepoRootResolved $repoRootResolved

$enabled = $true
if ($null -ne $policy.PSObject.Properties['enabled']) {
  $enabled = [bool]$policy.enabled
}

$enforceBlock = $true
if ($null -ne $policy.PSObject.Properties['enforce'] -and $null -ne $policy.enforce.PSObject.Properties['block_on_violation']) {
  $enforceBlock = [bool]$policy.enforce.block_on_violation
}
$requirePlanForViolation = $false
$allowWithPlan = $false
$complexityPlanPathResolved = [System.IO.Path]::GetFullPath((Join-Path $repoRootResolved ".governance\complexity-budget-plan.json"))
if ($null -ne $policy.PSObject.Properties['enforce']) {
  if ($null -ne $policy.enforce.PSObject.Properties['require_merge_or_deprecation_plan_on_violation']) {
    $requirePlanForViolation = [bool]$policy.enforce.require_merge_or_deprecation_plan_on_violation
  }
  if ($null -ne $policy.enforce.PSObject.Properties['allow_with_active_plan']) {
    $allowWithPlan = [bool]$policy.enforce.allow_with_active_plan
  }
  if ($null -ne $policy.enforce.PSObject.Properties['plan_path'] -and -not [string]::IsNullOrWhiteSpace([string]$policy.enforce.plan_path)) {
    $planPathText = [string]$policy.enforce.plan_path
    if ([System.IO.Path]::IsPathRooted($planPathText)) {
      $complexityPlanPathResolved = [System.IO.Path]::GetFullPath($planPathText)
    } else {
      $complexityPlanPathResolved = [System.IO.Path]::GetFullPath((Join-Path $repoRootResolved $planPathText))
    }
  }
}

$scopePreferPending = $true
$scopeIncludeUntracked = $true
$scopeScanRepoWhenNoPending = $false
$scopeMaxRepoFiles = 200
if ($null -ne $policy.PSObject.Properties['scope']) {
  if ($null -ne $policy.scope.PSObject.Properties['prefer_git_pending']) {
    $scopePreferPending = [bool]$policy.scope.prefer_git_pending
  }
  if ($null -ne $policy.scope.PSObject.Properties['include_untracked']) {
    $scopeIncludeUntracked = [bool]$policy.scope.include_untracked
  }
  if ($null -ne $policy.scope.PSObject.Properties['scan_repo_when_no_pending']) {
    $scopeScanRepoWhenNoPending = [bool]$policy.scope.scan_repo_when_no_pending
  }
  if ($null -ne $policy.scope.PSObject.Properties['max_repo_files']) {
    $scopeMaxRepoFiles = [int]$policy.scope.max_repo_files
  }
}
if ($null -ne $repoOverride -and $null -ne $repoOverride.PSObject.Properties['scope'] -and $null -ne $repoOverride.scope) {
  if ($null -ne $repoOverride.scope.PSObject.Properties['prefer_git_pending']) {
    $scopePreferPending = [bool]$repoOverride.scope.prefer_git_pending
  }
  if ($null -ne $repoOverride.scope.PSObject.Properties['include_untracked']) {
    $scopeIncludeUntracked = [bool]$repoOverride.scope.include_untracked
  }
  if ($null -ne $repoOverride.scope.PSObject.Properties['scan_repo_when_no_pending']) {
    $scopeScanRepoWhenNoPending = [bool]$repoOverride.scope.scan_repo_when_no_pending
  }
  if ($null -ne $repoOverride.scope.PSObject.Properties['max_repo_files']) {
    $scopeMaxRepoFiles = [int]$repoOverride.scope.max_repo_files
  }
}

$includeExts = @('.ps1', '.psm1', '.psd1', '.cs', '.py', '.js', '.jsx', '.ts', '.tsx', '.go', '.java', '.kt', '.rb', '.php')
$excludePaths = @('.git/', 'node_modules/', 'dist/', 'build/', 'bin/', 'obj/', 'backups/', '.venv/', 'coverage/', 'docs/')
if ($null -ne $policy.PSObject.Properties['scan']) {
  if ($null -ne $policy.scan.PSObject.Properties['include_extensions']) {
    $includeExts = @($policy.scan.include_extensions)
  }
  if ($null -ne $policy.scan.PSObject.Properties['exclude_paths']) {
    $excludePaths = @($policy.scan.exclude_paths)
  }
}

$maxFileLines = 500
$maxBlockLines = 120
$maxTokensPerFile = 2400
$maxTokensTotalPending = 10000
$maxDuplicateLines = 6
$charsPerToken = 4
function Apply-LimitOverrides {
  param([object]$LimitObject)
  if ($null -eq $LimitObject) { return }
  if ($null -ne $LimitObject.PSObject.Properties['max_file_lines']) {
    $script:maxFileLines = [int]$LimitObject.max_file_lines
  }
  if ($null -ne $LimitObject.PSObject.Properties['max_consecutive_non_empty_lines']) {
    $script:maxBlockLines = [int]$LimitObject.max_consecutive_non_empty_lines
  }
  if ($null -ne $LimitObject.PSObject.Properties['max_estimated_tokens_per_file']) {
    $script:maxTokensPerFile = [int]$LimitObject.max_estimated_tokens_per_file
  }
  if ($null -ne $LimitObject.PSObject.Properties['max_estimated_tokens_total_pending']) {
    $script:maxTokensTotalPending = [int]$LimitObject.max_estimated_tokens_total_pending
  }
  if ($null -ne $LimitObject.PSObject.Properties['max_duplicate_line_occurrences']) {
    $script:maxDuplicateLines = [int]$LimitObject.max_duplicate_line_occurrences
  }
  if ($null -ne $LimitObject.PSObject.Properties['chars_per_token']) {
    $script:charsPerToken = [int]$LimitObject.chars_per_token
  }
}

if ($null -ne $policy.PSObject.Properties['limits']) {
  Apply-LimitOverrides -LimitObject $policy.limits
}
if ($null -ne $policy.PSObject.Properties['mode_limits'] -and $null -ne $policy.mode_limits.PSObject.Properties[$tokenBudgetModeResolved]) {
  $modeLimits = $policy.mode_limits.PSObject.Properties[$tokenBudgetModeResolved].Value
  Apply-LimitOverrides -LimitObject $modeLimits
}
if ($null -ne $repoOverride -and $null -ne $repoOverride.PSObject.Properties['limits']) {
  Apply-LimitOverrides -LimitObject $repoOverride.limits
}
if ($null -ne $repoOverride -and $null -ne $repoOverride.PSObject.Properties['mode_limits'] -and $null -ne $repoOverride.mode_limits.PSObject.Properties[$tokenBudgetModeResolved]) {
  $modeLimits = $repoOverride.mode_limits.PSObject.Properties[$tokenBudgetModeResolved].Value
  Apply-LimitOverrides -LimitObject $modeLimits
}
if ($null -ne $repoOverride -and $null -ne $repoOverride.PSObject.Properties['enforce']) {
  if ($null -ne $repoOverride.enforce.PSObject.Properties['require_merge_or_deprecation_plan_on_violation']) {
    $requirePlanForViolation = [bool]$repoOverride.enforce.require_merge_or_deprecation_plan_on_violation
  }
  if ($null -ne $repoOverride.enforce.PSObject.Properties['allow_with_active_plan']) {
    $allowWithPlan = [bool]$repoOverride.enforce.allow_with_active_plan
  }
  if ($null -ne $repoOverride.enforce.PSObject.Properties['plan_path'] -and -not [string]::IsNullOrWhiteSpace([string]$repoOverride.enforce.plan_path)) {
    $repoPlanPathText = [string]$repoOverride.enforce.plan_path
    if ([System.IO.Path]::IsPathRooted($repoPlanPathText)) {
      $complexityPlanPathResolved = [System.IO.Path]::GetFullPath($repoPlanPathText)
    } else {
      $complexityPlanPathResolved = [System.IO.Path]::GetFullPath((Join-Path $repoRootResolved $repoPlanPathText))
    }
  }
}

function Get-ComplexityPlanValidation {
  param([string]$PlanPathResolved)

  if ([string]::IsNullOrWhiteSpace($PlanPathResolved)) {
    return [pscustomobject]@{ ok = $false; reason = 'plan_path_empty'; plan_type = $null; evidence_ref = $null }
  }
  if (-not (Test-Path -LiteralPath $PlanPathResolved -PathType Leaf)) {
    return [pscustomobject]@{ ok = $false; reason = 'plan_file_missing'; plan_type = $null; evidence_ref = $null }
  }

  $planObj = $null
  try {
    $planObj = Get-Content -LiteralPath $PlanPathResolved -Raw | ConvertFrom-Json
  } catch {
    return [pscustomobject]@{ ok = $false; reason = 'plan_json_invalid'; plan_type = $null; evidence_ref = $null }
  }

  $entries = @()
  if ($null -ne $planObj -and $null -ne $planObj.PSObject.Properties['entries']) {
    $entries = @($planObj.entries)
  }
  if ($entries.Count -eq 0) {
    return [pscustomobject]@{ ok = $false; reason = 'plan_entries_missing'; plan_type = $null; evidence_ref = $null }
  }

  $today = (Get-Date).Date
  foreach ($entry in $entries) {
    if ($null -eq $entry) { continue }
    $planType = ""
    if ($null -ne $entry.PSObject.Properties['plan_type']) {
      $planType = ([string]$entry.plan_type).Trim().ToLowerInvariant()
    }
    if (@("merge", "deprecation") -notcontains $planType) { continue }

    $status = "active"
    if ($null -ne $entry.PSObject.Properties['status'] -and -not [string]::IsNullOrWhiteSpace([string]$entry.status)) {
      $status = ([string]$entry.status).Trim().ToLowerInvariant()
    }
    if (@("active", "approved", "in_progress") -notcontains $status) { continue }

    if ($null -ne $entry.PSObject.Properties['expires_at'] -and -not [string]::IsNullOrWhiteSpace([string]$entry.expires_at)) {
      $expiresAtText = ([string]$entry.expires_at).Trim()
      $expiresAt = [DateTime]::MinValue
      if (-not [DateTime]::TryParseExact($expiresAtText, "yyyy-MM-dd", [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::None, [ref]$expiresAt)) {
        continue
      }
      if ($expiresAt.Date -lt $today) { continue }
    }

    $evidenceRef = $null
    if ($null -ne $entry.PSObject.Properties['evidence_ref']) {
      $evidenceRef = [string]$entry.evidence_ref
    }
    return [pscustomobject]@{ ok = $true; reason = 'plan_active'; plan_type = $planType; evidence_ref = $evidenceRef }
  }

  return [pscustomobject]@{ ok = $false; reason = 'no_active_merge_or_deprecation_plan'; plan_type = $null; evidence_ref = $null }
}

$limitsOut = @{
  max_file_lines = $maxFileLines
  max_consecutive_non_empty_lines = $maxBlockLines
  max_estimated_tokens_per_file = $maxTokensPerFile
  max_estimated_tokens_total_pending = $maxTokensTotalPending
  max_duplicate_line_occurrences = $maxDuplicateLines
  chars_per_token = $charsPerToken
}

if (-not $enabled) {
  $disabledResult = New-ResultObject -Status 'PASS' -RepoRootResolved $repoRootResolved -PolicyPathResolved $policyPathResolved -TokenBudgetModeResolved $tokenBudgetModeResolved -Scope 'disabled' -ScannedFiles @() -Violations @() -Warnings @('anti-bloat policy disabled') -TotalEstimatedTokens 0 -Limits $limitsOut
  Write-Result -Result $disabledResult -BlockOnViolation $false
  exit 0
}

if ($PolicyOnly) {
  $policyOnlyResult = New-ResultObject -Status 'PASS' -RepoRootResolved $repoRootResolved -PolicyPathResolved $policyPathResolved -TokenBudgetModeResolved $tokenBudgetModeResolved -Scope 'policy_only' -ScannedFiles @() -Violations @() -Warnings @() -TotalEstimatedTokens 0 -Limits $limitsOut
  Write-Result -Result $policyOnlyResult -BlockOnViolation $false
  exit 0
}

$includeSet = ConvertTo-Set -Items @($includeExts)
$candidatePaths = [System.Collections.Generic.List[string]]::new()
$scopeUsed = 'none'

$gitRootProbe = Invoke-GitSilent -RepoRoot $repoRootResolved -ArgsText "rev-parse --show-toplevel"
$gitRootRaw = (($gitRootProbe.output | Out-String).Trim())
$gitAvailable = ($gitRootProbe.exit_code -eq 0 -and -not [string]::IsNullOrWhiteSpace([string]$gitRootRaw))
$gitRoot = $repoRootResolved
if ($gitAvailable) {
  $gitRoot = [System.IO.Path]::GetFullPath($gitRootRaw)
}

if ($scopePreferPending -and $gitAvailable) {
  $statusProbe = Invoke-GitSilent -RepoRoot $gitRoot -ArgsText "status --porcelain --untracked-files=normal"
  $statusLines = @($statusProbe.output | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
  foreach ($line in $statusLines) {
    $lineText = [string]$line
    if ($lineText.Length -lt 4) { continue }

    $xy = $lineText.Substring(0, 2)
    if (-not $scopeIncludeUntracked -and $xy -eq '??') {
      continue
    }

    $pathPart = $lineText.Substring(3).Trim()
    if ($pathPart.Contains(' -> ')) {
      $parts = $pathPart.Split(' -> ')
      $pathPart = $parts[$parts.Length - 1].Trim()
    }

    if ([string]::IsNullOrWhiteSpace($pathPart)) { continue }

    try {
      $fullPath = Join-Path $gitRoot $pathPart
      if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
        [void]$candidatePaths.Add([System.IO.Path]::GetFullPath($fullPath))
      }
    } catch {
      continue
    }
  }

  if ($candidatePaths.Count -gt 0) {
    $scopeUsed = 'pending'
  }
}

if ($candidatePaths.Count -eq 0 -and $scopeScanRepoWhenNoPending) {
  if ($gitAvailable) {
    $trackedProbe = Invoke-GitSilent -RepoRoot $gitRoot -ArgsText "ls-files"
    $trackedFiles = @($trackedProbe.output | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
    foreach ($tracked in $trackedFiles) {
      try {
        $fullPath = Join-Path $gitRoot ([string]$tracked)
        if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
          [void]$candidatePaths.Add([System.IO.Path]::GetFullPath($fullPath))
        }
      } catch {
        continue
      }
    }
  } else {
    $allFiles = @(Get-ChildItem -LiteralPath $repoRootResolved -Recurse -File -ErrorAction SilentlyContinue)
    foreach ($item in $allFiles) {
      [void]$candidatePaths.Add([System.IO.Path]::GetFullPath($item.FullName))
    }
  }

  if ($scopeMaxRepoFiles -gt 0 -and $candidatePaths.Count -gt $scopeMaxRepoFiles) {
    $candidatePaths = [System.Collections.Generic.List[string]]::new(@($candidatePaths | Select-Object -First $scopeMaxRepoFiles))
  }

  if ($candidatePaths.Count -gt 0) {
    $scopeUsed = 'repo'
  }
}

if ($candidatePaths.Count -eq 0) {
  $idleResult = New-ResultObject -Status 'PASS' -RepoRootResolved $repoRootResolved -PolicyPathResolved $policyPathResolved -TokenBudgetModeResolved $tokenBudgetModeResolved -Scope 'no_files' -ScannedFiles @() -Violations @() -Warnings @('no eligible files in current scope') -TotalEstimatedTokens 0 -Limits $limitsOut
  Write-Result -Result $idleResult -BlockOnViolation $false
  exit 0
}

$scanned = [System.Collections.Generic.List[object]]::new()
$violations = [System.Collections.Generic.List[object]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$totalEstimatedTokens = 0
$uniqueFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

foreach ($fullPath in @($candidatePaths)) {
  if (-not $uniqueFiles.Add($fullPath)) { continue }
  try {
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) { continue }
  } catch {
    continue
  }

  $baseRoot = if ($gitAvailable) { $gitRoot } else { $repoRootResolved }
  $relativePath = Get-RelativePathCompat -BasePath $baseRoot -TargetPath $fullPath
  $relativePath = Normalize-RelPath $relativePath

  if (Test-ExcludedPath -RelativePath $relativePath -Excludes @($excludePaths)) {
    continue
  }

  $ext = ""
  try {
    $ext = ([System.IO.Path]::GetExtension($fullPath)).ToLowerInvariant()
  } catch {
    continue
  }
  if (-not $includeSet.Contains($ext)) {
    continue
  }

  $content = ''
  try {
    $content = Get-Content -LiteralPath $fullPath -Raw
  } catch {
    [void]$warnings.Add("failed to read file: $relativePath")
    continue
  }

  $lines = @($content -split "`r?`n")
  $lineCount = $lines.Count
  $estimatedTokens = [int][math]::Ceiling(([double][Math]::Max(0, $content.Length)) / [double][Math]::Max(1, $charsPerToken))
  $maxBlock = Get-MaxConsecutiveNonEmptyLines -Lines $lines
  $maxDuplicateLineCount = Get-MaxDuplicateNormalizedLineCount -Lines $lines

  $totalEstimatedTokens += $estimatedTokens

  [void]$scanned.Add([pscustomobject]@{
    file = $relativePath
    lines = $lineCount
    estimated_tokens = $estimatedTokens
    max_consecutive_non_empty_lines = $maxBlock
    max_duplicate_line_occurrences = $maxDuplicateLineCount
  })

  if ($lineCount -gt $maxFileLines) {
    [void]$violations.Add([pscustomobject]@{
      type = 'file_lines'
      file = $relativePath
      actual = $lineCount
      limit = $maxFileLines
      suggestion = 'split responsibilities: extract modules and keep each file focused.'
    })
  }

  if ($estimatedTokens -gt $maxTokensPerFile) {
    [void]$violations.Add([pscustomobject]@{
      type = 'file_estimated_tokens'
      file = $relativePath
      actual = $estimatedTokens
      limit = $maxTokensPerFile
      suggestion = 'reduce token footprint: remove duplicated branches and compress helper layers.'
    })
  }

  if ($maxBlock -gt $maxBlockLines) {
    [void]$violations.Add([pscustomobject]@{
      type = 'max_consecutive_non_empty_lines'
      file = $relativePath
      actual = $maxBlock
      limit = $maxBlockLines
      suggestion = 'add seam points: split long blocks into small functions with explicit contracts.'
    })
  }

  if ($maxDuplicateLineCount -gt $maxDuplicateLines) {
    [void]$violations.Add([pscustomobject]@{
      type = 'duplicate_line_occurrences'
      file = $relativePath
      actual = $maxDuplicateLineCount
      limit = $maxDuplicateLines
      suggestion = 'deduplicate repeated logic: move to shared function or table-driven structure.'
    })
  }
}

if ($scopeUsed -eq 'pending' -and $totalEstimatedTokens -gt $maxTokensTotalPending) {
  [void]$violations.Add([pscustomobject]@{
    type = 'pending_total_estimated_tokens'
    file = '(pending-set)'
    actual = $totalEstimatedTokens
    limit = $maxTokensTotalPending
    suggestion = 'narrow current change set: split into smaller commits or reduce feature surface.'
  })
}

if ($violations.Count -gt 0 -and $requirePlanForViolation) {
  $planCheck = Get-ComplexityPlanValidation -PlanPathResolved $complexityPlanPathResolved
  if ($planCheck.ok) {
    if ($allowWithPlan) {
      [void]$warnings.Add(("complexity budget exceeded but allowed by active {0} plan: {1}" -f [string]$planCheck.plan_type, ($complexityPlanPathResolved -replace '\\', '/')))
      $violations = [System.Collections.Generic.List[object]]::new()
    }
  } else {
    [void]$violations.Add([pscustomobject]@{
      type = 'missing_merge_or_deprecation_plan'
      file = '(policy)'
      actual = [string]$planCheck.reason
      limit = 'active merge/deprecation plan required'
      suggestion = ('add an active merge/deprecation plan at {0} before accepting complexity budget overages.' -f ($complexityPlanPathResolved -replace '\\', '/'))
    })
  }
}

$status = if ($violations.Count -eq 0) { 'PASS' } else { 'FAIL' }
$result = New-ResultObject -Status $status -RepoRootResolved $repoRootResolved -PolicyPathResolved $policyPathResolved -TokenBudgetModeResolved $tokenBudgetModeResolved -Scope $scopeUsed -ScannedFiles @($scanned) -Violations @($violations) -Warnings @($warnings) -TotalEstimatedTokens $totalEstimatedTokens -Limits $limitsOut
Write-Result -Result $result -BlockOnViolation $enforceBlock

if ($violations.Count -gt 0 -and $enforceBlock) {
  exit 1
}

exit 0
