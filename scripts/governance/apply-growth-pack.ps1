param(
  [string]$RepoPath,
  [ValidateSet("plan", "safe")]
  [string]$Mode = "safe",
  [switch]$Overwrite,
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
    return ([System.IO.Path]::GetFullPath(($Path -replace '/', '\\')) -replace '\\', '/').TrimEnd('/')
  }
}

function Get-GrowthPolicy([string]$Root) {
  $path = Join-Path $Root "config\growth-pack-policy.json"
  return Read-JsonFile -Path $path -DefaultValue $null
}

function Test-RootApplyEnabled([psobject]$Policy, [string]$RepoPathAbs) {
  if ($null -eq $Policy) { return $false }
  $enabled = $false
  if ($null -ne $Policy.PSObject.Properties['root_apply_enabled_by_default']) {
    $enabled = [bool]$Policy.root_apply_enabled_by_default
  }
  $repoNorm = Normalize-Repo $RepoPathAbs
  $repoName = Split-Path -Leaf $repoNorm
  foreach ($entry in @($Policy.repo_overrides)) {
    if ($null -eq $entry) { continue }
    $match = $false
    if ($entry.PSObject.Properties['repo'] -and -not [string]::IsNullOrWhiteSpace([string]$entry.repo)) {
      if ((Normalize-Repo ([string]$entry.repo)).Equals($repoNorm, [System.StringComparison]::OrdinalIgnoreCase)) { $match = $true }
    }
    if (-not $match -and $entry.PSObject.Properties['repoName'] -and -not [string]::IsNullOrWhiteSpace([string]$entry.repoName)) {
      if (([string]$entry.repoName).Equals($repoName, [System.StringComparison]::OrdinalIgnoreCase)) { $match = $true }
    }
    if (-not $match) { continue }
    if ($entry.PSObject.Properties['root_apply_enabled']) {
      $enabled = [bool]$entry.root_apply_enabled
    }
  }
  return $enabled
}

$repos = @()
if (-not [string]::IsNullOrWhiteSpace($RepoPath)) {
  $repos = @([System.IO.Path]::GetFullPath(($RepoPath -replace '/', '\\')))
} else {
  $reposPath = Join-Path $kitRoot "config\repositories.json"
  $repos = @((Read-JsonArray $reposPath) | ForEach-Object { [System.IO.Path]::GetFullPath(([string]$_ -replace '/', '\\')) })
}

$policy = Get-GrowthPolicy -Root $kitRoot
$mapping = @(
  @{ src = ".governance/growth-pack/README.template.md"; dst = "README.md" },
  @{ src = ".governance/growth-pack/RELEASE_TEMPLATE.md"; dst = "RELEASE_TEMPLATE.md" },
  @{ src = ".governance/growth-pack/CONTRIBUTING.template.md"; dst = "CONTRIBUTING.md" },
  @{ src = ".governance/growth-pack/SECURITY.template.md"; dst = "SECURITY.md" },
  @{ src = ".governance/growth-pack/ISSUE_TEMPLATE/bug_report.yml"; dst = ".github/ISSUE_TEMPLATE/bug_report.yml" },
  @{ src = ".governance/growth-pack/ISSUE_TEMPLATE/feature_request.yml"; dst = ".github/ISSUE_TEMPLATE/feature_request.yml" },
  @{ src = ".governance/growth-pack/pull_request_template.md"; dst = ".github/pull_request_template.md" }
)

$items = [System.Collections.Generic.List[object]]::new()
$summary = [ordered]@{ repos = 0; copied = 0; skipped = 0; missing_source = 0; disabled = 0 }

foreach ($repo in $repos) {
  if (-not (Test-Path -LiteralPath $repo -PathType Container)) {
    continue
  }
  $summary.repos++

  if (-not (Test-RootApplyEnabled -Policy $policy -RepoPathAbs $repo)) {
    $summary.disabled++
    $items.Add([pscustomobject]@{ repo = $repo; action = "SKIP_DISABLED"; source = ""; target = "" }) | Out-Null
    continue
  }

  foreach ($pair in $mapping) {
    $src = Join-Path $repo (($pair.src) -replace '/', '\\')
    $dst = Join-Path $repo (($pair.dst) -replace '/', '\\')

    if (-not (Test-Path -LiteralPath $src -PathType Leaf)) {
      $summary.missing_source++
      $items.Add([pscustomobject]@{ repo = $repo; action = "SKIP_SOURCE_MISSING"; source = $pair.src; target = $pair.dst }) | Out-Null
      continue
    }

    $dstExists = Test-Path -LiteralPath $dst -PathType Leaf
    if ($dstExists -and -not $Overwrite) {
      $summary.skipped++
      $items.Add([pscustomobject]@{ repo = $repo; action = "SKIP_EXISTS"; source = $pair.src; target = $pair.dst }) | Out-Null
      continue
    }

    if ($Mode -eq "plan") {
      $items.Add([pscustomobject]@{ repo = $repo; action = if ($dstExists) { "PLAN_OVERWRITE" } else { "PLAN_CREATE" }; source = $pair.src; target = $pair.dst }) | Out-Null
      continue
    }

    $dstDir = Split-Path -Parent $dst
    if (-not (Test-Path -LiteralPath $dstDir -PathType Container)) {
      New-Item -ItemType Directory -Path $dstDir -Force | Out-Null
    }
    Copy-Item -LiteralPath $src -Destination $dst -Force
    $summary.copied++
    $items.Add([pscustomobject]@{ repo = $repo; action = if ($dstExists) { "OVERWRITE" } else { "CREATE" }; source = $pair.src; target = $pair.dst }) | Out-Null
  }
}

if ($AsJson) {
  [pscustomobject]@{
    mode = $Mode
    overwrite = [bool]$Overwrite
    summary = [pscustomobject]$summary
    items = @($items)
  } | ConvertTo-Json -Depth 8 | Write-Output
  exit 0
}

Write-Host ("apply_growth_pack.mode={0}" -f $Mode)
Write-Host ("apply_growth_pack.repos={0}" -f $summary.repos)
Write-Host ("apply_growth_pack.copied={0}" -f $summary.copied)
Write-Host ("apply_growth_pack.skipped={0}" -f $summary.skipped)
Write-Host ("apply_growth_pack.missing_source={0}" -f $summary.missing_source)
Write-Host ("apply_growth_pack.disabled={0}" -f $summary.disabled)
