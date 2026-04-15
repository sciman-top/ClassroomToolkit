param(
  [string]$RepoPath,
  [switch]$AsJson
)

$ErrorActionPreference = "Stop"
$kitRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$commonPath = Join-Path $kitRoot "scripts\lib\common.ps1"
if (Test-Path -LiteralPath $commonPath -PathType Leaf) {
  . $commonPath
}

if (-not (Get-Command -Name Read-JsonFile -ErrorAction SilentlyContinue)) {
  function Read-JsonFile {
    param([Parameter(Mandatory = $true)][string]$Path, [object]$DefaultValue = $null)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $DefaultValue }
    try { return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json) } catch { return $DefaultValue }
  }
}

if (-not (Get-Command -Name Read-JsonArray -ErrorAction SilentlyContinue)) {
  function Read-JsonArray([string]$Path) {
    $raw = Read-JsonFile -Path $Path -DefaultValue @()
    if ($null -eq $raw) { return @() }
    if ($raw -is [System.Array]) { return @($raw) }
    if ($raw.PSObject -and $raw.PSObject.Properties['value']) { return @($raw.value) }
    return @($raw)
  }
}

if (-not (Get-Command -Name Normalize-Repo -ErrorAction SilentlyContinue)) {
  function Normalize-Repo([string]$Path) {
    return ([System.IO.Path]::GetFullPath(($Path -replace '/', '\')) -replace '\\', '/').TrimEnd('/')
  }
}

if (-not (Get-Command -Name Get-GrowthPackFilesForRepo -ErrorAction SilentlyContinue)) {
  function Get-GrowthPackFilesForRepo([string]$KitRootArg, [string]$RepoPathArg, [string]$RepoNameArg) {
    $policyPath = Join-Path $KitRootArg "config\growth-pack-policy.json"
    $policy = Read-JsonFile -Path $policyPath -DefaultValue $null
    if ($null -eq $policy) { return @() }
    if ($null -eq $policy.PSObject.Properties['enabled'] -or -not [bool]$policy.enabled) { return @() }

    $repoNorm = Normalize-Repo $RepoPathArg
    $repoLeaf = if ([string]::IsNullOrWhiteSpace($RepoNameArg)) { Split-Path -Leaf $repoNorm } else { [string]$RepoNameArg }

    $tier = "starter"
    if ($null -ne $policy.PSObject.Properties['default_tier'] -and -not [string]::IsNullOrWhiteSpace([string]$policy.default_tier)) {
      $tier = ([string]$policy.default_tier).Trim().ToLowerInvariant()
    }

    foreach ($entry in @($policy.repo_overrides)) {
      if ($null -eq $entry) { continue }
      $match = $false
      if ($entry.PSObject.Properties['repo'] -and -not [string]::IsNullOrWhiteSpace([string]$entry.repo)) {
        if ((Normalize-Repo ([string]$entry.repo)).Equals($repoNorm, [System.StringComparison]::OrdinalIgnoreCase)) { $match = $true }
      }
      if (-not $match -and $entry.PSObject.Properties['repoName'] -and -not [string]::IsNullOrWhiteSpace([string]$entry.repoName)) {
        if (([string]$entry.repoName).Equals($repoLeaf, [System.StringComparison]::OrdinalIgnoreCase)) { $match = $true }
      }
      if (-not $match) { continue }
      if ($entry.PSObject.Properties['enabled'] -and -not [bool]$entry.enabled) { return @() }
      if ($entry.PSObject.Properties['tier'] -and -not [string]::IsNullOrWhiteSpace([string]$entry.tier)) {
        $tier = ([string]$entry.tier).Trim().ToLowerInvariant()
      }
    }

    if ($null -eq $policy.PSObject.Properties['tiers'] -or $null -eq $policy.tiers) { return @() }
    $tierProp = $policy.tiers.PSObject.Properties[$tier]
    if ($null -eq $tierProp -or $null -eq $tierProp.Value) { return @() }

    $files = [System.Collections.Generic.List[string]]::new()
    foreach ($f in @($tierProp.Value)) {
      if (-not [string]::IsNullOrWhiteSpace([string]$f)) {
        [void]$files.Add(([string]$f -replace '\\', '/').TrimStart('/'))
      }
    }
    return @($files.ToArray())
  }
}

function Test-QuickStartInReadme([string]$Path) {
  if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $false }
  $text = Get-Content -LiteralPath $Path -Raw
  $quickStartCn = ([string][char]0x5FEB) + [char]0x901F + [char]0x5F00 + [char]0x59CB
  return ([regex]::IsMatch($text, "(?im)quick\s*start") -or $text.Contains($quickStartCn))
}

$repos = @()
if (-not [string]::IsNullOrWhiteSpace($RepoPath)) {
  $repos = @(Normalize-Repo ([string]$RepoPath))
} else {
  $reposPath = Join-Path $kitRoot "config\repositories.json"
  $repos = @((Read-JsonArray $reposPath) | ForEach-Object { Normalize-Repo ([string]$_) })
}

$policyPath = Join-Path $kitRoot "config\growth-pack-policy.json"
$policy = Read-JsonFile -Path $policyPath -DefaultValue $null
$quickStartMode = "advisory"
if ($null -ne $policy -and $null -ne $policy.PSObject.Properties['readme_quickstart_mode'] -and -not [string]::IsNullOrWhiteSpace([string]$policy.readme_quickstart_mode)) {
  $quickStartMode = ([string]$policy.readme_quickstart_mode).Trim().ToLowerInvariant()
}

$items = [System.Collections.Generic.List[object]]::new()
$failed = 0
foreach ($repo in $repos) {
  $repoName = Split-Path -Leaf $repo
  if (-not (Test-Path -LiteralPath $repo -PathType Container)) {
    $items.Add([pscustomobject]@{
      repo = $repo
      repo_name = $repoName
      status = "FAIL"
      reason = "repo_not_found"
      expected_count = 0
      present_count = 0
      missing_files = @()
      advisory = @("repository path not found")
      readiness_score = 0
      quickstart_mode = $quickStartMode
    }) | Out-Null
    $failed++
    continue
  }

  $expectedFiles = @(Get-GrowthPackFilesForRepo $kitRoot $repo $repoName)
  $missing = [System.Collections.Generic.List[string]]::new()
  $present = 0
  foreach ($rel in $expectedFiles) {
    $abs = Join-Path $repo ($rel -replace '/', '\')
    if (Test-Path -LiteralPath $abs -PathType Leaf) {
      $present++
    } else {
      $missing.Add($rel) | Out-Null
    }
  }

  $advisory = [System.Collections.Generic.List[string]]::new()
  $hasQuickStart = Test-QuickStartInReadme -Path (Join-Path $repo "README.md")
  $quickStartMissing = -not $hasQuickStart
  if ($quickStartMissing) {
    $advisory.Add("README.md missing Quick Start section") | Out-Null
  }

  $hasReleaseTemplate = (Test-Path -LiteralPath (Join-Path $repo "RELEASE_TEMPLATE.md") -PathType Leaf) -or
    (Test-Path -LiteralPath (Join-Path $repo ".governance\growth-pack\RELEASE_TEMPLATE.md") -PathType Leaf)
  if (-not $hasReleaseTemplate) {
    $advisory.Add("release template not found in root or .governance/growth-pack") | Out-Null
  }

  $hasGitHubPresence = Test-Path -LiteralPath (Join-Path $repo ".governance\growth-pack\GITHUB-PRESENCE.md") -PathType Leaf
  if (-not $hasGitHubPresence) {
    $advisory.Add("GitHub presence playbook not found in .governance/growth-pack") | Out-Null
  }

  $hasIssueTemplate = (Test-Path -LiteralPath (Join-Path $repo ".github\ISSUE_TEMPLATE\bug_report.yml") -PathType Leaf) -or
    (Test-Path -LiteralPath (Join-Path $repo ".governance\growth-pack\ISSUE_TEMPLATE\bug_report.yml") -PathType Leaf)
  if (-not $hasIssueTemplate) {
    $advisory.Add("issue template not found in .github or .governance/growth-pack") | Out-Null
  }

  $quickStartGateFail = ($quickStartMode -eq "enforce" -and $quickStartMissing)
  $status = if ($missing.Count -eq 0 -and -not $quickStartGateFail) { "PASS" } else { "FAIL" }
  if ($status -eq "FAIL") { $failed++ }

  $reason = "ok"
  if ($status -eq "FAIL") {
    if ($quickStartGateFail -and $missing.Count -eq 0) {
      $reason = "quickstart_gate_failed"
    } else {
      $reason = "missing_growth_pack_files"
    }
  }

  $items.Add([pscustomobject]@{
    repo = $repo
    repo_name = $repoName
    status = $status
    reason = $reason
    expected_count = $expectedFiles.Count
    present_count = $present
    missing_files = @($missing)
    advisory = @($advisory)
    quickstart_mode = $quickStartMode
  }) | Out-Null
}

if ($AsJson) {
  [pscustomobject]@{
    schema_version = "1.0"
    generated_at = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    repo_count = $repos.Count
    failed_count = $failed
    status = if ($failed -eq 0) { "PASS" } else { "FAIL" }
    items = @($items)
  } | ConvertTo-Json -Depth 8 | Write-Output
  if ($failed -eq 0) { exit 0 } else { exit 1 }
}

foreach ($item in $items) {
  Write-Host ("[{0}] {1} coverage={2}/{3}" -f $item.status, $item.repo_name, $item.present_count, $item.expected_count)
  foreach ($m in @($item.missing_files)) { Write-Host ("  [MISS] " + $m) }
  foreach ($hint in @($item.advisory)) { Write-Host ("  [ADVISORY] " + $hint) }
}

if ($failed -eq 0) {
  Write-Host "growth-pack verification passed"
  exit 0
}

Write-Host ("growth-pack verification failed: failed_repos={0}" -f $failed)
if ($quickStartMode -eq "enforce") { Write-Host "[GATE] quickstart_mode=enforce" }
exit 1
