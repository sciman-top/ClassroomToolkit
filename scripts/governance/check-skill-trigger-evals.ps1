param(
  [string]$RepoRoot = ".",
  [string]$InputRelativePath = ".governance/skill-candidates/trigger-eval-runs.jsonl",
  [string]$OutputRelativePath = ".governance/skill-candidates/trigger-eval-summary.json",
  [double]$TriggerRateThreshold = 0.50,
  [ValidateSet("validation", "train")]
  [string]$DefaultSplit = "validation",
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

function Read-JsonLines([string]$PathText) {
  if (-not (Test-Path -LiteralPath $PathText -PathType Leaf)) { return @() }
  $items = New-Object System.Collections.Generic.List[object]
  foreach ($line in @(Get-Content -LiteralPath $PathText -Encoding utf8)) {
    $text = [string]$line
    if ([string]::IsNullOrWhiteSpace($text)) { continue }
    try {
      $items.Add(($text | ConvertFrom-Json)) | Out-Null
    } catch {
      continue
    }
  }
  return @($items.ToArray())
}

$repoPath = Resolve-NormalizedPath $RepoRoot
$inputPath = Join-Path ($repoPath -replace '/', '\') ($InputRelativePath -replace '/', '\')
$outputPath = Join-Path ($repoPath -replace '/', '\') ($OutputRelativePath -replace '/', '\')

$rawItems = @()
if (Test-Path -LiteralPath $inputPath -PathType Leaf) {
  $rawItems = @(Read-JsonLines $inputPath)
}

$groupMap = @{}
$totalRuns = 0
foreach ($item in @($rawItems)) {
  if ($null -eq $item) { continue }
  if ($null -eq $item.PSObject.Properties['query']) { continue }
  if ($null -eq $item.PSObject.Properties['should_trigger']) { continue }
  if ($null -eq $item.PSObject.Properties['triggered']) { continue }

  $query = [string]$item.query
  if ([string]::IsNullOrWhiteSpace($query)) { continue }
  $should = [bool]$item.should_trigger
  $triggered = [bool]$item.triggered
  $split = $DefaultSplit
  if ($null -ne $item.PSObject.Properties['split'] -and -not [string]::IsNullOrWhiteSpace([string]$item.split)) {
    $split = ([string]$item.split).Trim().ToLowerInvariant()
    if (@("train", "validation") -notcontains $split) { $split = $DefaultSplit }
  }
  $evalType = "standard"
  if ($null -ne $item.PSObject.Properties['eval_type'] -and -not [string]::IsNullOrWhiteSpace([string]$item.eval_type)) {
    $candidateType = ([string]$item.eval_type).Trim().ToLowerInvariant()
    if (@("standard", "adversarial") -contains $candidateType) {
      $evalType = $candidateType
    }
  }

  $key = "{0}|{1}|{2}|{3}" -f $split, ($should.ToString().ToLowerInvariant()), $evalType, $query.Trim()
  if (-not $groupMap.ContainsKey($key)) {
    $groupMap[$key] = [pscustomobject]@{
      split = $split
      eval_type = $evalType
      query = $query.Trim()
      should_trigger = $should
      run_count = 0
      trigger_count = 0
    }
  }

  $bucket = $groupMap[$key]
  $bucket.run_count = [int]$bucket.run_count + 1
  if ($triggered) { $bucket.trigger_count = [int]$bucket.trigger_count + 1 }
  $totalRuns++
}

$queryResults = New-Object System.Collections.Generic.List[object]
foreach ($k in @($groupMap.Keys | Sort-Object)) {
  $b = $groupMap[$k]
  if ([int]$b.run_count -le 0) { continue }
  $rate = [double]$b.trigger_count / [double]$b.run_count
  $predictedTrigger = ($rate -ge $TriggerRateThreshold)
  $passed = (($b.should_trigger -and $predictedTrigger) -or ((-not $b.should_trigger) -and (-not $predictedTrigger)))
  $queryResults.Add([pscustomobject]@{
    split = [string]$b.split
    eval_type = [string]$b.eval_type
    query = [string]$b.query
    should_trigger = [bool]$b.should_trigger
    run_count = [int]$b.run_count
    trigger_count = [int]$b.trigger_count
    trigger_rate = [Math]::Round($rate, 6)
    predicted_trigger = [bool]$predictedTrigger
    passed = [bool]$passed
  }) | Out-Null
}

$validationItems = @($queryResults | Where-Object { ([string]$_.split) -eq "validation" })
$trainItems = @($queryResults | Where-Object { ([string]$_.split) -eq "train" })
$standardValidationItems = @($validationItems | Where-Object { ([string]$_.eval_type) -eq "standard" })
$adversarialValidationItems = @($validationItems | Where-Object { ([string]$_.eval_type) -eq "adversarial" })

function Get-PassRate([object[]]$Items) {
  if ($null -eq $Items -or $Items.Count -eq 0) { return $null }
  $ok = @($Items | Where-Object { [bool]$_.passed }).Count
  return [Math]::Round(([double]$ok / [double]$Items.Count), 6)
}

function Get-FalseTriggerRate([object[]]$Items) {
  if ($null -eq $Items -or $Items.Count -eq 0) { return $null }
  $negatives = @($Items | Where-Object { -not [bool]$_.should_trigger })
  if ($negatives.Count -eq 0) { return $null }
  $falsePositives = @($negatives | Where-Object { [bool]$_.predicted_trigger }).Count
  return [Math]::Round(([double]$falsePositives / [double]$negatives.Count), 6)
}

$validationPassRate = Get-PassRate $validationItems
$trainPassRate = Get-PassRate $trainItems
$validationFalseTriggerRate = Get-FalseTriggerRate $validationItems
$trainFalseTriggerRate = Get-FalseTriggerRate $trainItems
$standardValidationPassRate = Get-PassRate $standardValidationItems
$standardValidationFalseTriggerRate = Get-FalseTriggerRate $standardValidationItems
$adversarialValidationPassRate = Get-PassRate $adversarialValidationItems
$adversarialValidationFalseTriggerRate = Get-FalseTriggerRate $adversarialValidationItems

$status = "ok"
if ($queryResults.Count -eq 0) {
  $status = "no_data"
} elseif ($validationItems.Count -eq 0) {
  $status = "no_validation_split"
}

$result = [ordered]@{
  schema_version = "1.0"
  status = $status
  generated_at = (Get-Date).ToString("o")
  repo_root = $repoPath
  input_path = ($inputPath -replace '\\', '/')
  output_path = ($outputPath -replace '\\', '/')
  trigger_rate_threshold = [Math]::Round($TriggerRateThreshold, 6)
  total_runs = [int]$totalRuns
  grouped_query_count = [int]$queryResults.Count
  train_query_count = [int]$trainItems.Count
  validation_query_count = [int]$validationItems.Count
  standard_validation_query_count = [int]$standardValidationItems.Count
  adversarial_validation_query_count = [int]$adversarialValidationItems.Count
  train_pass_rate = $trainPassRate
  validation_pass_rate = $validationPassRate
  standard_validation_pass_rate = $standardValidationPassRate
  adversarial_validation_pass_rate = $adversarialValidationPassRate
  train_false_trigger_rate = $trainFalseTriggerRate
  validation_false_trigger_rate = $validationFalseTriggerRate
  standard_validation_false_trigger_rate = $standardValidationFalseTriggerRate
  adversarial_validation_false_trigger_rate = $adversarialValidationFalseTriggerRate
  query_results = @($queryResults.ToArray())
}

Ensure-ParentDirectory $outputPath
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outputPath -Encoding utf8

if ($AsJson) {
  $result | ConvertTo-Json -Depth 12 | Write-Output
} else {
  Write-Host ("skill_trigger_eval.status={0}" -f $status)
  Write-Host ("skill_trigger_eval.grouped_query_count={0}" -f [int]$queryResults.Count)
  Write-Host ("skill_trigger_eval.validation_pass_rate={0}" -f $validationPassRate)
  Write-Host ("skill_trigger_eval.validation_false_trigger_rate={0}" -f $validationFalseTriggerRate)
  Write-Host ("skill_trigger_eval.adversarial_validation_pass_rate={0}" -f $adversarialValidationPassRate)
  Write-Host ("skill_trigger_eval.adversarial_validation_false_trigger_rate={0}" -f $adversarialValidationFalseTriggerRate)
  Write-Host ("skill_trigger_eval.output={0}" -f ($outputPath -replace '\\', '/'))
}
