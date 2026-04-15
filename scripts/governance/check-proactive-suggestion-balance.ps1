param(
  [string]$RepoRoot = ".",
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Add-Warning {
  param(
    [System.Collections.Generic.List[object]]$Warnings,
    [string]$Rule,
    [string]$Message
  )
  [void]$Warnings.Add([pscustomobject]@{
      rule = $Rule
      message = $Message
    })
}

function Add-Violation {
  param(
    [System.Collections.Generic.List[object]]$Violations,
    [string]$Rule,
    [string]$Message
  )
  [void]$Violations.Add([pscustomobject]@{
      rule = $Rule
      message = $Message
    })
}

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$policyPath = Join-Path $repoPath ".governance\proactive-suggestion-policy.json"
$warnings = [System.Collections.Generic.List[object]]::new()
$violations = [System.Collections.Generic.List[object]]::new()

if (-not (Test-Path -LiteralPath $policyPath -PathType Leaf)) {
  Add-Warning -Warnings $warnings -Rule "policy_present" -Message "policy file not found: $policyPath"
  $result = [pscustomobject]@{
    schema_version = "1.0"
    status = "ADVISORY"
    repo_root = ($repoPath -replace '\\', '/')
    policy_path = ($policyPath -replace '\\', '/')
    warning_count = $warnings.Count
    violation_count = 0
    warnings = @($warnings)
    violations = @()
  }
  if ($AsJson) { $result | ConvertTo-Json -Depth 8 | Write-Output } else { Write-Host "proactive_suggestion_balance.status=ADVISORY" }
  exit 0
}

$policy = $null
try {
  $policy = Get-Content -LiteralPath $policyPath -Raw -Encoding UTF8 | ConvertFrom-Json
} catch {
  Add-Violation -Violations $violations -Rule "policy_json_valid" -Message "invalid JSON: $policyPath"
}

if ($null -eq $policy) {
  $result = [pscustomobject]@{
    schema_version = "1.0"
    status = "ALERT"
    repo_root = ($repoPath -replace '\\', '/')
    policy_path = ($policyPath -replace '\\', '/')
    warning_count = 0
    violation_count = $violations.Count
    warnings = @()
    violations = @($violations)
  }
  if ($AsJson) { $result | ConvertTo-Json -Depth 8 | Write-Output } else { Write-Host "proactive_suggestion_balance.status=ALERT" }
  exit 1
}

$enabled = $true
if ($null -ne $policy.PSObject.Properties['enabled']) { $enabled = [bool]$policy.enabled }
if (-not $enabled) {
  $result = [pscustomobject]@{
    schema_version = "1.0"
    status = "DISABLED"
    repo_root = ($repoPath -replace '\\', '/')
    policy_path = ($policyPath -replace '\\', '/')
    warning_count = 0
    violation_count = 0
    warnings = @()
    violations = @()
  }
  if ($AsJson) { $result | ConvertTo-Json -Depth 8 | Write-Output } else { Write-Host "proactive_suggestion_balance.status=DISABLED" }
  exit 0
}

$validModes = @("silent", "lite", "standard")
$defaultMode = [string]$policy.default_mode
$fallbackMode = [string]$policy.fallback_mode

if ([string]::IsNullOrWhiteSpace($defaultMode) -or -not ($validModes -contains $defaultMode)) {
  Add-Violation -Violations $violations -Rule "default_mode_valid" -Message "default_mode must be one of: silent/lite/standard"
}
if ([string]::IsNullOrWhiteSpace($fallbackMode) -or -not ($validModes -contains $fallbackMode)) {
  Add-Violation -Violations $violations -Rule "fallback_mode_valid" -Message "fallback_mode must be one of: silent/lite/standard"
}

$liteConfig = $null
$standardConfig = $null
if ($null -ne $policy.PSObject.Properties['modes'] -and $null -ne $policy.modes) {
  if ($null -ne $policy.modes.PSObject.Properties['lite']) { $liteConfig = $policy.modes.lite }
  if ($null -ne $policy.modes.PSObject.Properties['standard']) { $standardConfig = $policy.modes.standard }
}

if ($null -eq $liteConfig) {
  Add-Violation -Violations $violations -Rule "mode_lite_present" -Message "modes.lite is required"
} else {
  $liteMaxSuggestions = [int]$liteConfig.max_suggestions
  if ($liteMaxSuggestions -lt 0 -or $liteMaxSuggestions -gt 2) {
    Add-Violation -Violations $violations -Rule "lite_max_suggestions_range" -Message "modes.lite.max_suggestions should be in [0,2]"
  }
  $liteWords = [int]$liteConfig.max_words_per_suggestion
  if ($liteWords -lt 8 -or $liteWords -gt 30) {
    Add-Violation -Violations $violations -Rule "lite_words_range" -Message "modes.lite.max_words_per_suggestion should be in [8,30]"
  }
}

if ($null -eq $standardConfig) {
  Add-Violation -Violations $violations -Rule "mode_standard_present" -Message "modes.standard is required"
} else {
  $standardMaxSuggestions = [int]$standardConfig.max_suggestions
  if ($standardMaxSuggestions -lt 1 -or $standardMaxSuggestions -gt 3) {
    Add-Violation -Violations $violations -Rule "standard_max_suggestions_range" -Message "modes.standard.max_suggestions should be in [1,3]"
  }
  $standardWords = [int]$standardConfig.max_words_per_suggestion
  if ($standardWords -lt 12 -or $standardWords -gt 60) {
    Add-Violation -Violations $violations -Rule "standard_words_range" -Message "modes.standard.max_words_per_suggestion should be in [12,60]"
  }
}

$turnWordBudget = $null
$issueWordBudget = $null
if ($null -ne $policy.PSObject.Properties['token_guard'] -and $null -ne $policy.token_guard) {
  if ($null -ne $policy.token_guard.PSObject.Properties['max_total_suggestion_words_per_turn']) {
    $turnWordBudget = [int]$policy.token_guard.max_total_suggestion_words_per_turn
  }
  if ($null -ne $policy.token_guard.PSObject.Properties['max_total_suggestion_words_per_issue']) {
    $issueWordBudget = [int]$policy.token_guard.max_total_suggestion_words_per_issue
  }
}

if ($null -eq $turnWordBudget -or $turnWordBudget -lt 10 -or $turnWordBudget -gt 80) {
  Add-Violation -Violations $violations -Rule "turn_word_budget_range" -Message "token_guard.max_total_suggestion_words_per_turn should be in [10,80]"
}
if ($null -eq $issueWordBudget -or $issueWordBudget -lt 60 -or $issueWordBudget -gt 400) {
  Add-Violation -Violations $violations -Rule "issue_word_budget_range" -Message "token_guard.max_total_suggestion_words_per_issue should be in [60,400]"
}
if ($null -ne $turnWordBudget -and $null -ne $issueWordBudget -and $issueWordBudget -lt $turnWordBudget) {
  Add-Violation -Violations $violations -Rule "budget_monotonic" -Message "issue-level word budget must be >= turn-level word budget"
}

if ($defaultMode -eq "silent") {
  Add-Warning -Warnings $warnings -Rule "default_mode_too_tight" -Message "default_mode is silent; confirm this does not suppress needed risk hints"
}
if ($defaultMode -eq "standard" -and $null -ne $turnWordBudget -and $turnWordBudget -gt 50) {
  Add-Warning -Warnings $warnings -Rule "default_mode_too_loose" -Message "default_mode is standard with high turn budget; verify token efficiency trend regularly"
}

$status = if ($violations.Count -gt 0) { "ALERT" } elseif ($warnings.Count -gt 0) { "ADVISORY" } else { "OK" }
$result = [pscustomobject]@{
  schema_version = "1.0"
  status = $status
  repo_root = ($repoPath -replace '\\', '/')
  policy_path = ($policyPath -replace '\\', '/')
  default_mode = $defaultMode
  fallback_mode = $fallbackMode
  warning_count = $warnings.Count
  violation_count = $violations.Count
  warnings = @($warnings)
  violations = @($violations)
}

if ($AsJson) {
  $result | ConvertTo-Json -Depth 8 | Write-Output
  if ($status -eq "ALERT") { exit 1 } else { exit 0 }
}

Write-Host ("proactive_suggestion_balance.status={0}" -f $status)
Write-Host ("proactive_suggestion_balance.default_mode={0}" -f $defaultMode)
Write-Host ("proactive_suggestion_balance.fallback_mode={0}" -f $fallbackMode)
Write-Host ("proactive_suggestion_balance.warning_count={0}" -f $warnings.Count)
Write-Host ("proactive_suggestion_balance.violation_count={0}" -f $violations.Count)
foreach ($w in @($warnings)) { Write-Host ("[WARN] rule={0} message={1}" -f $w.rule, $w.message) }
foreach ($v in @($violations)) { Write-Host ("[VIOLATION] rule={0} message={1}" -f $v.rule, $v.message) }
if ($status -eq "ALERT") { exit 1 } else { exit 0 }
