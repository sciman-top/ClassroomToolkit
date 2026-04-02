param(
  [string]$EvidenceDir = "docs/change-evidence",
  [string]$TemplateFile = "docs/change-evidence/template.md",
  [ValidateSet("changed", "all")]
  [string]$Mode = "changed",
  [double]$Threshold = 98.0,
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-RepoPath {
  param([Parameter(Mandatory = $true)][string]$RelativePath)
  $normalized = $RelativePath -replace '/', '\'
  return Join-Path (Get-Location).Path $normalized
}

function Parse-KeyValueFile {
  param([Parameter(Mandatory = $true)][string]$Path)
  $map = @{}
  foreach ($line in (Get-Content -LiteralPath $Path -Encoding UTF8)) {
    if ($line -match '^\s*([^#=\s][^=]*)\s*=\s*(.*)$') {
      $map[$Matches[1].Trim()] = $Matches[2].Trim()
    }
  }
  return $map
}

function Get-TemplateKeyOrder {
  param([Parameter(Mandatory = $true)][string]$Path)
  $ordered = New-Object System.Collections.Generic.List[string]
  foreach ($line in (Get-Content -LiteralPath $Path -Encoding UTF8)) {
    if ($line -match '^\s*([^#=\s][^=]*)\s*=') {
      $key = $Matches[1].Trim()
      if (-not [string]::IsNullOrWhiteSpace($key) -and -not $ordered.Contains($key)) {
        [void]$ordered.Add($key)
      }
    }
  }
  return @($ordered)
}

function Normalize-Path {
  param([string]$PathValue)
  return ($PathValue -replace '/', '\').Trim()
}

function Get-ChangedEvidenceFiles {
  param([string]$EvidenceDirNormalized)

  $paths = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
  $hasNativePreference = Test-Path variable:PSNativeCommandUseErrorActionPreference
  $oldNativePreference = $null
  if ($hasNativePreference) {
    $oldNativePreference = $PSNativeCommandUseErrorActionPreference
    $PSNativeCommandUseErrorActionPreference = $false
  }

  function Invoke-GitLines {
    param([string[]]$Args)
    $oldEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
      $result = & git @Args 2>$null
      return @($result)
    } catch {
      return @()
    } finally {
      $ErrorActionPreference = $oldEap
    }
  }

  try {
    $insideRepo = Invoke-GitLines -Args @("rev-parse", "--is-inside-work-tree")
    if ($LASTEXITCODE -ne 0 -or ($insideRepo -join "").Trim() -ne "true") {
      return @()
    }

    $candidateOutputs = @(
      (Invoke-GitLines -Args @("-c", "core.safecrlf=false", "diff", "--name-only", "--diff-filter=ACMRTUXB", "HEAD")),
      (Invoke-GitLines -Args @("-c", "core.safecrlf=false", "diff", "--name-only", "--diff-filter=ACMRTUXB", "HEAD~1", "HEAD")),
      (Invoke-GitLines -Args @("-c", "core.safecrlf=false", "ls-files", "--others", "--exclude-standard"))
    )

    foreach ($output in $candidateOutputs) {
      foreach ($line in $output) {
        $lineText = Normalize-Path -PathValue ([string]$line)
        if ([string]::IsNullOrWhiteSpace($lineText)) {
          continue
        }
        if (-not $lineText.StartsWith($EvidenceDirNormalized + "\")) {
          continue
        }
        if (-not $lineText.EndsWith(".md")) {
          continue
        }
        if ($lineText.EndsWith("\template.md")) {
          continue
        }
        [void]$paths.Add($lineText)
      }
    }
  } finally {
    if ($hasNativePreference) {
      $PSNativeCommandUseErrorActionPreference = $oldNativePreference
    }
  }

  return @($paths.ToArray() | Sort-Object)
}

$templatePath = Resolve-RepoPath -RelativePath $TemplateFile
if (!(Test-Path -LiteralPath $templatePath -PathType Leaf)) {
  throw "Template file not found: $templatePath"
}

$evidenceRoot = Resolve-RepoPath -RelativePath $EvidenceDir
if (!(Test-Path -LiteralPath $evidenceRoot -PathType Container)) {
  throw "Evidence directory not found: $evidenceRoot"
}

$templateKeys = Get-TemplateKeyOrder -Path $templatePath
if ($templateKeys.Count -eq 0) {
  throw "Template has no key=value fields: $templatePath"
}

$criticalKeys = @()
if ($templateKeys.Count -ge 9) {
  $criticalKeys = @($templateKeys[0], $templateKeys[6], $templateKeys[7], $templateKeys[8])
} else {
  $take = [Math]::Min(4, $templateKeys.Count)
  $criticalKeys = @($templateKeys | Select-Object -First $take)
}

$targets = @()
if ($Mode -eq "changed") {
  $changed = Get-ChangedEvidenceFiles -EvidenceDirNormalized (Normalize-Path -PathValue $EvidenceDir)
  foreach ($relative in $changed) {
    $absolute = Resolve-RepoPath -RelativePath $relative
    if (Test-Path -LiteralPath $absolute -PathType Leaf) {
      $targets += (Get-Item -LiteralPath $absolute)
    }
  }
} else {
  $targets = Get-ChildItem -LiteralPath $evidenceRoot -Filter *.md -File |
    Where-Object { $_.Name -ne "template.md" } |
    Sort-Object -Property FullName
}

$rows = @()
$violations = @()

foreach ($file in $targets) {
  $data = Parse-KeyValueFile -Path $file.FullName
  $presentCount = 0
  $missing = @()
  foreach ($key in $templateKeys) {
    if ($data.ContainsKey($key) -and -not [string]::IsNullOrWhiteSpace([string]$data[$key])) {
      $presentCount += 1
    } else {
      $missing += $key
    }
  }

  $missingCritical = @()
  foreach ($critical in $criticalKeys) {
    if (-not $data.ContainsKey($critical) -or [string]::IsNullOrWhiteSpace([string]$data[$critical])) {
      $missingCritical += $critical
    }
  }

  $coverage = [Math]::Round(($presentCount * 100.0 / $templateKeys.Count), 2)
  $ok = ($coverage -ge $Threshold) -and ($missingCritical.Count -eq 0)

  $row = [pscustomobject]@{
    file = $file.FullName
    coverage = $coverage
    missing_count = $missing.Count
    missing_critical = $missingCritical
    ok = $ok
  }
  $rows += $row
  if (-not $ok) {
    $violations += $row
  }
}

$overallCoverage = if ($rows.Count -eq 0) { 100.0 } else { [Math]::Round((($rows | Measure-Object -Property coverage -Average).Average), 2) }
$status = if ($violations.Count -eq 0) { "PASS" } else { "FAIL" }

Write-Host "[evidence] dir=$EvidenceDir mode=$Mode files=$($rows.Count) overall_coverage=$overallCoverage threshold=$Threshold status=$status"

if ($rows.Count -gt 0) {
  $rows | Select-Object file, coverage, missing_count, ok | Format-Table -AutoSize | Out-Host
}

if ($violations.Count -gt 0) {
  Write-Host "[evidence][FAIL] violations=$($violations.Count)"
  foreach ($v in $violations) {
    $criticalText = if ($v.missing_critical.Count -gt 0) { $v.missing_critical -join "," } else { "-" }
    Write-Host ("  - {0}: coverage={1}, missing_critical={2}" -f $v.file, $v.coverage, $criticalText)
  }
}

if ($AsJson) {
  [pscustomobject]@{
    status = $status
    evidenceDir = $EvidenceDir
    mode = $Mode
    threshold = $Threshold
    templateKeys = $templateKeys
    criticalKeys = $criticalKeys
    totalFiles = $rows.Count
    overallCoverage = $overallCoverage
    checked = $rows
    violations = $violations
  } | ConvertTo-Json -Depth 6
}

if ($status -ne "PASS") {
  exit 1
}

exit 0
