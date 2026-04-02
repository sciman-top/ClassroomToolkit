param(
  [string]$OutputDir = "docs/governance/reports",
  [ValidateSet("changed", "all")]
  [string]$EvidenceMode = "changed",
  [double]$EvidenceThreshold = 98.0,
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-RepoPath {
  param([Parameter(Mandatory = $true)][string]$RelativePath)
  $normalized = $RelativePath -replace '/', '\'
  return Join-Path (Get-Location).Path $normalized
}

function Invoke-ScriptJson {
  param(
    [Parameter(Mandatory = $true)][string]$ScriptPath,
    [string[]]$Arguments = @()
  )

  $jsonText = ""
  $rawText = ""
  $status = "PASS"
  $errorText = $null

  try {
    $cmdArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $ScriptPath) + $Arguments + @("-AsJson")
    $rawLines = & powershell @cmdArgs 2>&1
    $rawText = ($rawLines | ForEach-Object { [string]$_ }) -join [Environment]::NewLine
    if (Test-Path variable:LASTEXITCODE) {
      if ($LASTEXITCODE -ne 0) {
        $status = "FAIL"
      }
    }

    $jsonMatch = [regex]::Match($rawText, '(?s)\{[\s\S]*\}\s*$')
    if ($jsonMatch.Success) {
      $jsonText = $jsonMatch.Value
    }

    if ([string]::IsNullOrWhiteSpace($jsonText)) {
      $status = "FAIL"
      $errorText = "json_not_found_in_output"
    }
  } catch {
    $status = "FAIL"
    $errorText = $_.Exception.Message
  }

  if ([string]::IsNullOrWhiteSpace([string]$jsonText) -and $status -eq "FAIL") {
    return [pscustomobject]@{ status = $status; parse_ok = $false; error = $errorText; data = $null }
  }

  try {
    $data = $jsonText | ConvertFrom-Json
    return [pscustomobject]@{
      status = if ($status -eq "FAIL") { "FAIL" } else { [string]$data.status }
      parse_ok = $true
      error = $errorText
      data = $data
    }
  } catch {
    return [pscustomobject]@{
      status = "FAIL"
      parse_ok = $false
      error = "json_parse_failed: $($_.Exception.Message)"
      data = $null
    }
  }
}

function Get-PrecheckResult {
  $checks = @(
    @{ name = "dotnet"; ok = [bool](Get-Command dotnet -ErrorAction SilentlyContinue) },
    @{ name = "powershell"; ok = [bool](Get-Command powershell -ErrorAction SilentlyContinue) },
    @{ name = "tests_project"; ok = Test-Path -LiteralPath "tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj" -PathType Leaf }
  )

  $failed = @($checks | Where-Object { -not $_.ok })
  return [pscustomobject]@{
    status = if ($failed.Count -eq 0) { "PASS" } else { "FAIL" }
    checks = $checks
    failed = $failed
  }
}

function Compute-Score {
  param(
    [Parameter(Mandatory = $true)]$Precheck,
    [Parameter(Mandatory = $true)]$Waiver,
    [Parameter(Mandatory = $true)]$Evidence,
    [Parameter(Mandatory = $true)]$Hotspot
  )

  $score = 0
  if ($Precheck.status -eq "PASS") { $score += 20 }
  if ($Waiver.status -eq "PASS") { $score += 30 }
  if ($Evidence.status -eq "PASS") { $score += 30 }
  if ($Hotspot.status -eq "PASS") { $score += 20 }
  return $score
}

$repoRoot = (Get-Location).Path
$governanceDir = Resolve-RepoPath -RelativePath "scripts/governance"
$qualityDir = Resolve-RepoPath -RelativePath "scripts/quality"

$waiverScript = Join-Path $governanceDir "check-waiver-health.ps1"
$evidenceScript = Join-Path $governanceDir "check-evidence-completeness.ps1"
$hotspotScript = Join-Path $qualityDir "check-hotspot-line-budgets.ps1"

foreach ($path in @($waiverScript, $evidenceScript, $hotspotScript)) {
  if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
    throw "Required script not found: $path"
  }
}

$precheck = Get-PrecheckResult
$waiver = Invoke-ScriptJson -ScriptPath $waiverScript
$evidenceThresholdText = [string]$EvidenceThreshold
$evidence = Invoke-ScriptJson -ScriptPath $evidenceScript -Arguments @("-Mode", $EvidenceMode, "-Threshold", $evidenceThresholdText)
$hotspot = Invoke-ScriptJson -ScriptPath $hotspotScript

$score = Compute-Score -Precheck $precheck -Waiver $waiver -Evidence $evidence -Hotspot $hotspot

$gaps = @()
if ($precheck.status -ne "PASS") {
  $gaps += [pscustomobject]@{ area = "precheck"; severity = "high"; detail = "missing required tooling or test project" }
}
if ($waiver.status -ne "PASS") {
  $gaps += [pscustomobject]@{ area = "waiver"; severity = "high"; detail = "waiver fields invalid or expired unrecovered waiver exists" }
}
if ($evidence.status -ne "PASS") {
  $gaps += [pscustomobject]@{ area = "evidence"; severity = "medium"; detail = "evidence completeness below threshold or critical fields missing" }
}
if ($hotspot.status -ne "PASS") {
  $gaps += [pscustomobject]@{ area = "hotspot"; severity = "high"; detail = "hotspot budget check failed" }
}

$overallStatus = if ($gaps.Count -eq 0) { "PASS" } else { "FAIL" }
$timestamp = Get-Date
$stamp = $timestamp.ToString("yyyyMMdd-HHmmss")

$outputRoot = Resolve-RepoPath -RelativePath $OutputDir
if (!(Test-Path -LiteralPath $outputRoot -PathType Container)) {
  New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
}

$jsonPath = Join-Path $outputRoot ("endstate-{0}.json" -f $stamp)
$mdPath = Join-Path $outputRoot ("endstate-{0}.md" -f $stamp)

$report = [pscustomobject]@{
  generated_at = $timestamp.ToString("s")
  repo_root = $repoRoot
  status = $overallStatus
  endstate_score = $score
  threshold = [pscustomobject]@{
    good = 85
    warning = 70
  }
  dimensions = [pscustomobject]@{
    precheck = $precheck
    waiver = $waiver
    evidence = $evidence
    hotspot = $hotspot
  }
  evidence_mode = $EvidenceMode
  gaps = $gaps
}

$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8

$gapLines = @()
if ($gaps.Count -eq 0) {
  $gapLines += "- none"
} else {
  foreach ($gap in $gaps) {
    $gapLines += ("- [{0}] {1}: {2}" -f $gap.severity, $gap.area, $gap.detail)
  }
}

$md = @"
# Endstate Doctor Report

generated_at=$($report.generated_at)
repo_root=$repoRoot
status=$overallStatus
endstate_score=$score
json_path=$jsonPath

## Dimension Status
- precheck=$($precheck.status)
- waiver=$($waiver.status)
- evidence=$($evidence.status)
- hotspot=$($hotspot.status)
- evidence_mode=$EvidenceMode

## Gaps
$($gapLines -join [Environment]::NewLine)
"@

$md | Set-Content -LiteralPath $mdPath -Encoding UTF8

Write-Host "[doctor] status=$overallStatus score=$score"
Write-Host "[doctor] markdown=$mdPath"
Write-Host "[doctor] json=$jsonPath"

if ($AsJson) {
  [pscustomobject]@{
    status = $overallStatus
    endstate_score = $score
    markdown = $mdPath
    json = $jsonPath
    gaps = $gaps
  } | ConvertTo-Json -Depth 6
}

if ($overallStatus -ne "PASS") {
  exit 1
}
