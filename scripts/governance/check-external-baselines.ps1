param(
  [string]$RepoRoot = ".",
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$policyPath = Join-Path $repoPath ".governance\external-baseline-policy.json"

$defaultPolicy = [pscustomobject]@{
  schema_version = "1.0"
  enforcement = [pscustomobject]@{
    block_on_warn = $false
    block_on_advisory = $false
  }
  checks = [pscustomobject]@{
    ssdf = [pscustomobject]@{
      enabled = $true
      level = "recommended"
      evidence_any_of = @(
        "docs/governance/ssdf-mapping.md",
        ".governance/ssdf-mapping.md",
        "docs/governance/ssdf-checklist.md"
      )
    }
    slsa = [pscustomobject]@{
      enabled = $true
      level = "recommended"
      evidence_any_of = @(
        ".github/workflows/slsa.yml",
        ".github/workflows/slsa-provenance.yml",
        "docs/governance/slsa-target-level.md"
      )
    }
    sbom = [pscustomobject]@{
      enabled = $true
      level = "recommended"
      evidence_any_of = @(
        "scripts/quality/run-supply-chain-checks.ps1",
        ".github/workflows/sbom.yml",
        "docs/governance/sbom-policy.md"
      )
    }
    scorecard = [pscustomobject]@{
      enabled = $true
      level = "recommended"
      evidence_any_of = @(
        ".github/workflows/scorecard.yml",
        "docs/governance/scorecard-policy.md"
      )
    }
    code_scanning = [pscustomobject]@{
      enabled = $true
      level = "recommended"
      evidence_any_of = @(
        ".github/workflows/codeql.yml"
      )
    }
    dependency_review = [pscustomobject]@{
      enabled = $true
      level = "recommended"
      evidence_any_of = @(
        ".github/workflows/dependency-review.yml"
      )
    }
    codeowners = [pscustomobject]@{
      enabled = $true
      level = "recommended"
      evidence_any_of = @(
        ".github/CODEOWNERS"
      )
    }
    repository_rulesets = [pscustomobject]@{
      enabled = $true
      level = "recommended"
      evidence_any_of = @(
        ".governance/repository-ruleset-policy.json",
        ".github/rulesets/default.json",
        ".governance/repository-ruleset-config.json",
        "docs/governance/repository-ruleset-policy.md"
      )
    }
  }
}

function Read-PolicyOrDefault {
  param([string]$Path)
  if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    return $defaultPolicy
  }
  try {
    return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
  } catch {
    return $defaultPolicy
  }
}

function Test-AnyEvidenceExists {
  param(
    [string]$Root,
    [object[]]$Candidates
  )

  $matched = [System.Collections.Generic.List[string]]::new()
  foreach ($rel in @($Candidates)) {
    $relText = [string]$rel
    if ([string]::IsNullOrWhiteSpace($relText)) { continue }
    $abs = Join-Path $Root ($relText -replace '/', '\')
    if (Test-Path -LiteralPath $abs -PathType Leaf) {
      $matched.Add($relText.Replace('\', '/')) | Out-Null
    }
  }
  return @($matched)
}

$policy = Read-PolicyOrDefault -Path $policyPath
$checkNames = @("ssdf", "slsa", "sbom", "scorecard", "code_scanning", "dependency_review", "codeowners", "repository_rulesets")
$items = [System.Collections.Generic.List[object]]::new()

$warnCount = 0
$advisoryCount = 0
$passCount = 0
$enabledCount = 0

foreach ($name in $checkNames) {
  $check = $null
  if ($null -ne $policy -and $null -ne $policy.checks -and $null -ne $policy.checks.PSObject.Properties[$name]) {
    $check = $policy.checks.$name
  } elseif ($null -ne $defaultPolicy.checks.PSObject.Properties[$name]) {
    $check = $defaultPolicy.checks.$name
  }
  if ($null -eq $check) { continue }

  $enabled = $true
  if ($null -ne $check.PSObject.Properties["enabled"]) {
    $enabled = [bool]$check.enabled
  }
  if (-not $enabled) {
    $items.Add([pscustomobject]@{
      id = $name
      level = [string]$check.level
      enabled = $false
      status = "SKIP"
      reason = "check disabled by policy"
      evidence = @()
    }) | Out-Null
    continue
  }
  $enabledCount++

  $level = "recommended"
  if ($null -ne $check.PSObject.Properties["level"] -and -not [string]::IsNullOrWhiteSpace([string]$check.level)) {
    $level = ([string]$check.level).Trim().ToLowerInvariant()
  }
  if (@("required", "recommended", "optional") -notcontains $level) {
    $level = "recommended"
  }

  $candidates = @()
  if ($null -ne $check.PSObject.Properties["evidence_any_of"] -and $null -ne $check.evidence_any_of) {
    $candidates = @($check.evidence_any_of)
  }
  $matched = @(Test-AnyEvidenceExists -Root $repoPath -Candidates $candidates)

  if ($matched.Count -gt 0) {
    $status = "PASS"
    $reason = "evidence found"
    $passCount++
  } else {
    if ($level -eq "required") {
      $status = "WARN"
      $reason = "required baseline evidence missing"
      $warnCount++
    } elseif ($level -eq "recommended") {
      $status = "ADVISORY"
      $reason = "recommended baseline evidence missing"
      $advisoryCount++
    } else {
      $status = "PASS"
      $reason = "optional baseline evidence missing"
      $passCount++
    }
  }

  $items.Add([pscustomobject]@{
    id = $name
    level = $level
    enabled = $enabled
    status = $status
    reason = $reason
    evidence = @($matched)
    expected_evidence = @($candidates)
  }) | Out-Null
}

$overall = "OK"
if ($warnCount -gt 0) {
  $overall = "WARN"
} elseif ($advisoryCount -gt 0) {
  $overall = "ADVISORY"
}

$blockOnWarn = $false
$blockOnAdvisory = $false
if ($null -ne $policy -and $null -ne $policy.PSObject.Properties["enforcement"] -and $null -ne $policy.enforcement) {
  if ($null -ne $policy.enforcement.PSObject.Properties["block_on_warn"]) {
    $blockOnWarn = [bool]$policy.enforcement.block_on_warn
  }
  if ($null -ne $policy.enforcement.PSObject.Properties["block_on_advisory"]) {
    $blockOnAdvisory = [bool]$policy.enforcement.block_on_advisory
  }
}
$shouldFailGate = ($blockOnWarn -and $warnCount -gt 0) -or ($blockOnAdvisory -and $advisoryCount -gt 0)

$result = [pscustomobject]@{
  schema_version = "1.0"
  generated_at = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
  repo_root = ($repoPath -replace '\\', '/')
  status = $overall
  policy_path = ($policyPath -replace '\\', '/')
  summary = [pscustomobject]@{
    enabled_check_count = $enabledCount
    pass_count = $passCount
    advisory_count = $advisoryCount
    warn_count = $warnCount
    should_fail_gate = $shouldFailGate
  }
  enforcement = [pscustomobject]@{
    block_on_warn = $blockOnWarn
    block_on_advisory = $blockOnAdvisory
  }
  checks = @($items)
}

if ($AsJson) {
  $result | ConvertTo-Json -Depth 8 | Write-Output
  exit 0
}

Write-Host "EXTERNAL_BASELINES"
Write-Host ("status={0}" -f $result.status)
Write-Host ("enabled_check_count={0}" -f $result.summary.enabled_check_count)
Write-Host ("pass_count={0}" -f $result.summary.pass_count)
Write-Host ("advisory_count={0}" -f $result.summary.advisory_count)
Write-Host ("warn_count={0}" -f $result.summary.warn_count)
Write-Host ("should_fail_gate={0}" -f $result.summary.should_fail_gate)
foreach ($item in $result.checks) {
  Write-Host ("[{0}] id={1} level={2} reason={3}" -f $item.status, $item.id, $item.level, $item.reason)
}
if ($shouldFailGate) { exit 1 }
exit 0
