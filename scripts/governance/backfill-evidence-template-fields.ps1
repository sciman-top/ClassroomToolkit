param(
  [string]$EvidenceDir = "docs/change-evidence",
  [string]$TemplateFile = "docs/change-evidence/template.md",
  [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-RepoPath {
  param([Parameter(Mandatory = $true)][string]$RelativePath)
  $normalized = $RelativePath -replace '/', '\'
  return Join-Path (Get-Location).Path $normalized
}

function Parse-KeyValueMap {
  param([string[]]$Lines)
  $map = @{}
  foreach ($line in $Lines) {
    if ($line -match '^\s*([^#=\s][^=]*)\s*=\s*(.*)$') {
      $map[$Matches[1].Trim()] = $Matches[2].Trim()
    }
  }
  return $map
}

function Get-TemplateKeyOrder {
  param([string[]]$Lines)
  $ordered = New-Object System.Collections.Generic.List[string]
  foreach ($line in $Lines) {
    if ($line -match '^\s*([^#=\s][^=]*)\s*=') {
      $key = $Matches[1].Trim()
      if (-not [string]::IsNullOrWhiteSpace($key) -and -not $ordered.Contains($key)) {
        [void]$ordered.Add($key)
      }
    }
  }
  return @($ordered)
}

$templatePath = Resolve-RepoPath -RelativePath $TemplateFile
if (!(Test-Path -LiteralPath $templatePath -PathType Leaf)) {
  throw "Template file not found: $templatePath"
}

$evidenceRoot = Resolve-RepoPath -RelativePath $EvidenceDir
if (!(Test-Path -LiteralPath $evidenceRoot -PathType Container)) {
  throw "Evidence directory not found: $evidenceRoot"
}

$templateLines = Get-Content -LiteralPath $templatePath -Encoding UTF8
$templateKeys = Get-TemplateKeyOrder -Lines $templateLines
if ($templateKeys.Count -eq 0) {
  throw "Template has no key=value fields: $templatePath"
}

$defaultValues = @(
  "BACKFILL-LEGACY-EVIDENCE-2026-04-03",
  "legacy-governance-evidence",
  '${WORKSPACE_ROOT}/ClassroomToolkit/docs/change-evidence',
  '${WORKSPACE_ROOT}/repo-governance-hub/source/project/ClassroomToolkit/*',
  "2026-04-03-evidence-backfill",
  "Low(documentation backfill only)",
  "backfill-evidence-template-fields.ps1",
  "template-field-backfill-2026-04-03",
  "git revert evidence backfill commit"
)

$files = Get-ChildItem -LiteralPath $evidenceRoot -Filter *.md -File |
  Where-Object { $_.Name -ne "template.md" } |
  Sort-Object -Property FullName

$changed = 0
foreach ($file in $files) {
  $lines = Get-Content -LiteralPath $file.FullName -Encoding UTF8
  $map = Parse-KeyValueMap -Lines $lines

  $appendLines = @()
  for ($i = 0; $i -lt $templateKeys.Count; $i++) {
    $key = [string]$templateKeys[$i]
    $hasValue = $map.ContainsKey($key) -and -not [string]::IsNullOrWhiteSpace([string]$map[$key])
    if ($hasValue) {
      continue
    }

    $value = if ($i -lt $defaultValues.Count) { $defaultValues[$i] } else { "BACKFILL-2026-04-03" }
    $appendLines += ("{0}={1}" -f $key, $value)
  }

  if ($appendLines.Count -eq 0) {
    continue
  }

  $changed += 1
  Write-Host ("[backfill] {0} +{1} fields" -f $file.FullName, $appendLines.Count)

  if (-not $DryRun) {
    $newLines = @($lines + "" + "# Backfill 2026-04-03" + $appendLines)
    Set-Content -LiteralPath $file.FullName -Value $newLines -Encoding UTF8
  }
}

Write-Host ("[backfill] files_total={0} files_changed={1} dry_run={2}" -f $files.Count, $changed, [bool]$DryRun)

if (-not $DryRun) {
  exit 0
}


