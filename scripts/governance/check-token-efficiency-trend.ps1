param(
  [string]$RepoRoot = ".",
  [string]$PolicyRelativePath = ".governance/token-efficiency-trend-policy.json",
  [string]$MetricsRelativePath = "docs/governance/metrics-auto.md",
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-NormalizedPath([string]$PathText) {
  $resolved = Resolve-Path -LiteralPath $PathText -ErrorAction Stop
  return ([System.IO.Path]::GetFullPath($resolved.Path) -replace '\\', '/').TrimEnd('/')
}

function Read-Json([string]$PathText, [object]$DefaultValue) {
  if (-not (Test-Path -LiteralPath $PathText -PathType Leaf)) { return $DefaultValue }
  try {
    return (Get-Content -LiteralPath $PathText -Raw -Encoding UTF8 | ConvertFrom-Json)
  } catch {
    return $DefaultValue
  }
}

function Parse-KeyValueMap([string]$PathText) {
  $map = @{}
  if (-not (Test-Path -LiteralPath $PathText -PathType Leaf)) { return $map }
  foreach ($line in @(Get-Content -LiteralPath $PathText -Encoding UTF8)) {
    $text = [string]$line
    if ([string]::IsNullOrWhiteSpace($text)) { continue }
    if ($text.TrimStart().StartsWith("#")) { continue }
    $idx = $text.IndexOf("=")
    if ($idx -lt 1) { continue }
    $k = $text.Substring(0, $idx).Trim()
    $v = $text.Substring($idx + 1).Trim()
    if (-not [string]::IsNullOrWhiteSpace($k)) { $map[$k] = $v }
  }
  return $map
}

function Parse-DoubleOrNull([string]$Text) {
  if ([string]::IsNullOrWhiteSpace($Text)) { return $null }
  if ($Text -eq "N/A") { return $null }
  $v = 0.0
  if ([double]::TryParse($Text, [ref]$v)) { return $v }
  return $null
}

function Read-HistoryLines([string]$PathText) {
  $items = New-Object System.Collections.Generic.List[object]
  if (-not (Test-Path -LiteralPath $PathText -PathType Leaf)) { return @() }
  foreach ($line in @(Get-Content -LiteralPath $PathText -Encoding UTF8)) {
    $raw = [string]$line
    if ([string]::IsNullOrWhiteSpace($raw)) { continue }
    try {
      $obj = $raw | ConvertFrom-Json
      if ($null -ne $obj) { $items.Add($obj) | Out-Null }
    } catch {}
  }
  return @($items.ToArray())
}

$repoPath = Resolve-NormalizedPath $RepoRoot
$repoWin = $repoPath -replace '/', '\'
$policyPath = Join-Path $repoWin ($PolicyRelativePath -replace '/', '\')
$metricsPath = Join-Path $repoWin ($MetricsRelativePath -replace '/', '\')

$result = [ordered]@{
  schema_version = "1.0"
  generated_at = (Get-Date).ToString("o")
  repo_root = $repoPath
  policy_path = ($policyPath -replace '\\', '/')
  metrics_path = ($metricsPath -replace '\\', '/')
  status = "unknown"
  latest_value = $null
  history_count = 0
  trend_point_count = 0
  trend_values = @()
}

$policy = Read-Json -PathText $policyPath -DefaultValue $null
if ($null -eq $policy) {
  $result.status = "missing_policy"
  if ($AsJson) { $result | ConvertTo-Json -Depth 12 | Write-Output } else { Write-Host "token_efficiency_trend.status=missing_policy" }
  exit 1
}

$enabled = $true
if ($null -ne $policy.PSObject.Properties['enabled']) { $enabled = [bool]$policy.enabled }
if (-not $enabled) {
  $result.status = "disabled"
  if ($AsJson) { $result | ConvertTo-Json -Depth 12 | Write-Output } else { Write-Host "token_efficiency_trend.status=disabled" }
  exit 0
}

$historyRelative = ".governance/token-efficiency-history.jsonl"
if ($null -ne $policy.PSObject.Properties['history_file']) { $historyRelative = [string]$policy.history_file }
$historyPath = Join-Path $repoWin ($historyRelative -replace '/', '\')
$minPoints = 4
if ($null -ne $policy.PSObject.Properties['min_points_for_trend']) { try { $minPoints = [int]$policy.min_points_for_trend } catch { $minPoints = 4 } }
$maxIncreaseRatio = 0.02
if ($null -ne $policy.PSObject.Properties['max_allowed_increase_ratio']) { try { $maxIncreaseRatio = [double]$policy.max_allowed_increase_ratio } catch { $maxIncreaseRatio = 0.02 } }
$blockOnRegression = $true
if ($null -ne $policy.PSObject.Properties['block_on_regression']) { $blockOnRegression = [bool]$policy.block_on_regression }
$blockOnInsufficient = $false
if ($null -ne $policy.PSObject.Properties['block_on_insufficient_history']) { $blockOnInsufficient = [bool]$policy.block_on_insufficient_history }

$kv = Parse-KeyValueMap -PathText $metricsPath
$latestValue = Parse-DoubleOrNull -Text ([string]$kv["token_per_effective_conclusion"])
if ($null -eq $latestValue) {
  $result.status = "missing_metric"
  if ($AsJson) { $result | ConvertTo-Json -Depth 12 | Write-Output } else { Write-Host "token_efficiency_trend.status=missing_metric" }
  exit 0
}
$result.latest_value = $latestValue

$today = (Get-Date).ToString("yyyy-MM-dd")
$history = @(Read-HistoryLines -PathText $historyPath)
$historyByDate = @{}
foreach ($h in $history) {
  if ($null -eq $h) { continue }
  $d = [string]$h.date
  if ([string]::IsNullOrWhiteSpace($d)) { continue }
  $historyByDate[$d] = $h
}
$historyByDate[$today] = [pscustomobject]@{ date = $today; token_per_effective_conclusion = $latestValue }

$historyItems = @($historyByDate.GetEnumerator() | Sort-Object Name | ForEach-Object { $_.Value })
$historyDir = Split-Path -Parent $historyPath
if (-not (Test-Path -LiteralPath $historyDir -PathType Container)) {
  New-Item -Path $historyDir -ItemType Directory -Force | Out-Null
}
$historyContent = @()
foreach ($item in $historyItems) {
  $historyContent += ($item | ConvertTo-Json -Compress)
}
Set-Content -LiteralPath $historyPath -Value $historyContent -Encoding UTF8

$result.history_count = $historyItems.Count
$trendItems = @($historyItems | Sort-Object { [string]$_.date } | Select-Object -Last $minPoints)
$trendValues = New-Object System.Collections.Generic.List[double]
foreach ($t in $trendItems) {
  $v = Parse-DoubleOrNull -Text ([string]$t.token_per_effective_conclusion)
  if ($null -ne $v) { $trendValues.Add([double]$v) | Out-Null }
}
$result.trend_point_count = $trendValues.Count
$result.trend_values = @($trendValues.ToArray())

$status = "insufficient_history"
$shouldFail = $false
if ($trendValues.Count -lt $minPoints) {
  if ($blockOnInsufficient) { $shouldFail = $true }
} else {
  $first = [double]$trendValues[0]
  $last = [double]$trendValues[$trendValues.Count - 1]
  $allowedMax = $first * (1.0 + $maxIncreaseRatio)
  if ($last -gt $allowedMax) {
    $status = "regressing"
    if ($blockOnRegression) { $shouldFail = $true }
  } elseif ($last -lt $first) {
    $status = "improving"
  } else {
    $status = "stable"
  }
}

$result.status = $status

if ($AsJson) {
  $result | ConvertTo-Json -Depth 12 | Write-Output
} else {
  Write-Host ("token_efficiency_trend.status={0}" -f $result.status)
  Write-Host ("token_efficiency_trend.latest_value={0}" -f $result.latest_value)
  Write-Host ("token_efficiency_trend.history_count={0}" -f [int]$result.history_count)
  Write-Host ("token_efficiency_trend.trend_point_count={0}" -f [int]$result.trend_point_count)
}

if ($shouldFail) { exit 1 }
exit 0
