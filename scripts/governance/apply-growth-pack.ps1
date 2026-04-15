param(
  [string]$RepoPath,
  [ValidateSet("plan", "safe")]
  [string]$Mode = "safe",
  [ValidateSet("merge", "overwrite", "skip")]
  [string]$Strategy = "merge",
  [switch]$Overwrite,
  [switch]$AsJson
)

$ErrorActionPreference = "Stop"
$kitRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$commonPath = Join-Path $kitRoot "scripts\lib\common.ps1"
if (Test-Path -LiteralPath $commonPath -PathType Leaf) {
  . $commonPath
}

if ($Overwrite.IsPresent -and $Strategy -ne "overwrite") {
  Write-Host "[DEPRECATED] -Overwrite is kept for compatibility; use -Strategy overwrite."
  $Strategy = "overwrite"
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

function Write-TextFile([string]$Path, [string]$Content) {
  if (Get-Command -Name Write-Utf8NoBom -ErrorAction SilentlyContinue) {
    Write-Utf8NoBom -Path $Path -Content $Content
    return
  }
  Set-Content -LiteralPath $Path -Encoding UTF8 -Value $Content
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

function Get-RepoProfile([string]$RepoName) {
  $name = [string]$RepoName
  switch -Regex ($name) {
    '^ClassroomToolkit$' {
      return [ordered]@{
        project = "ClassroomToolkit"
        value = "Governance-aware toolkit for classroom-oriented repository operations."
        pain = "Repeated manual governance checks and inconsistent project setup across classroom repos."
        result = "Consistent setup, safer changes, and faster validation loops."
        diff = "Rule distribution and quality gates are executed as a repeatable workflow."
        persona = "Repository maintainers and teaching operations engineers"
        scenario = "Managing classroom templates, checks, and policy rollouts"
        threshold = "Use this when manual setup or validation starts causing repeated drift"
        prereq1 = "PowerShell 7+"
        prereq2 = "Git working copy with access to governance scripts"
        run = "powershell -File scripts/doctor.ps1"
        out1 = "HEALTH=GREEN in doctor output"
        out2 = "verify/target checks report PASS"
        wf1 = "Run doctor to validate current governance state"
        wf2 = "Run install in plan mode before safe distribution"
        wf3 = "Use cycle/autopilot scripts for governed iteration"
        faqQ = "Doctor reports FAIL"
        faqA = "Run scripts/verify.ps1 first, fix the first failing gate, then rerun doctor"
        lim1 = "Designed for governance-managed repositories"
        lim2 = "Requires policy and target mappings to be maintained"
      }
    }
    '^skills-manager$' {
      return [ordered]@{
        project = "skills-manager"
        value = "Skill lifecycle and governance manager for Codex agent repositories."
        pain = "Skill files drift across repos and lifecycle state becomes hard to audit."
        result = "Skill promotion, verification, and lifecycle review become repeatable."
        diff = "Governance scripts enforce skill quality and cross-repo consistency."
        persona = "Agent platform maintainers and automation engineers"
        scenario = "Managing shared skills, trigger evaluation, and promotion workflows"
        threshold = "Use this when skill updates are frequent and manual sync becomes error-prone"
        prereq1 = "PowerShell 7+"
        prereq2 = "Repository with governance scripts and policy files"
        run = "powershell -File scripts/doctor.ps1"
        out1 = "HEALTH=GREEN in doctor output"
        out2 = "Skill governance checks report PASS"
        wf1 = "Run verify before promoting skill candidates"
        wf2 = "Use lifecycle review to inspect merge/retire candidates"
        wf3 = "Distribute shared policies through install flow"
        faqQ = "Promotion is blocked"
        faqA = "Check trigger-eval status and required policy fields, then rerun promotion"
        lim1 = "Focuses on governance/lifecycle, not runtime model behavior"
        lim2 = "Relies on configured skill registry and policy files"
      }
    }
    default {
      return [ordered]@{
        project = $name
        value = "$name governance starter for repeatable setup and validation."
        pain = "Inconsistent repo setup and repeated manual checks."
        result = "Predictable setup and faster quality verification."
        diff = "Uses governance templates plus scripted validation."
        persona = "Repository maintainers"
        scenario = "Standardizing project governance and docs"
        threshold = "Use this when manual governance work starts to drift"
        prereq1 = "PowerShell 7+"
        prereq2 = "Git working copy"
        run = "powershell -File scripts/doctor.ps1"
        out1 = "HEALTH=GREEN in doctor output"
        out2 = "Verification gates report PASS"
        wf1 = "Verify current state"
        wf2 = "Run install and re-check"
        wf3 = "Track evidence and rollback path"
        faqQ = "Validation fails"
        faqA = "Fix the first failed gate from verify output and rerun doctor"
        lim1 = "Requires governance scripts to be present"
        lim2 = "Policy files must stay in sync with targets"
      }
    }
  }
}

function Materialize-TemplatePlaceholders([string]$TemplateText, [string]$RepoName) {
  $profile = Get-RepoProfile -RepoName $RepoName
  $text = [string]$TemplateText
  $replaceMap = [ordered]@{
    "<ProjectName>" = [string]$profile.project
    "<One-line value proposition for target users>" = [string]$profile.value
    "<what users struggle with today>" = [string]$profile.pain
    "<what changes after adopting this project>" = [string]$profile.result
    "<why this instead of alternatives>" = [string]$profile.diff
    "<persona or team>" = [string]$profile.persona
    "<scenario or workflow>" = [string]$profile.scenario
    "<problem threshold that justifies trying this>" = [string]$profile.threshold
    "<runtime and version>" = [string]$profile.prereq1
    "<platform support>" = [string]$profile.prereq2
    "<single shortest command>" = [string]$profile.run
    "<signal 1>" = [string]$profile.out1
    "<signal 2>" = [string]$profile.out2
    "<smallest useful workflow>" = [string]$profile.wf1
    "<second workflow>" = [string]$profile.wf2
    "<third workflow>" = [string]$profile.wf3
    "<common failure>" = [string]$profile.faqQ
    "<fix or command>" = [string]$profile.faqA
    "<known boundary>" = [string]$profile.lim1
    "<known compatibility note>" = [string]$profile.lim2
    "<docs link>" = "docs/"
    "<releases link>" = "RELEASE_TEMPLATE.md"
    "<issues or discussions link>" = "issues/"
  }
  foreach ($k in $replaceMap.Keys) {
    $v = [string]$replaceMap[$k]
    $text = $text.Replace($k, $v)
  }
  return $text
}

function Get-Level2Sections([string]$Text) {
  $lines = $Text -split "`r?`n"
  $sections = [System.Collections.Generic.List[object]]::new()
  $currentTitle = $null
  $buffer = [System.Collections.Generic.List[string]]::new()
  foreach ($line in $lines) {
    if ($line -match '^##\s+(.+?)\s*$') {
      if ($null -ne $currentTitle) {
        $sections.Add([pscustomobject]@{
          title = [string]$currentTitle
          content = [string]::Join([Environment]::NewLine, @($buffer))
        }) | Out-Null
        $buffer = [System.Collections.Generic.List[string]]::new()
      }
      $currentTitle = [string]$matches[1]
      continue
    }
    if ($null -ne $currentTitle) {
      $buffer.Add($line) | Out-Null
    }
  }
  if ($null -ne $currentTitle) {
    $sections.Add([pscustomobject]@{
      title = [string]$currentTitle
      content = [string]::Join([Environment]::NewLine, @($buffer))
    }) | Out-Null
  }
  return @($sections)
}

function Merge-Readme([string]$ExistingText, [string]$TemplateText, [string]$RepoName) {
  $materialized = Materialize-TemplatePlaceholders -TemplateText $TemplateText -RepoName $RepoName
  if ($ExistingText -match '<ProjectName>|<One-line value proposition|<what users struggle with today>') {
    return [pscustomobject]@{
      content = $materialized
      changed = $true
      reason = "materialized_template"
    }
  }

  $existingTitles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
  foreach ($m in [regex]::Matches($ExistingText, '(?m)^##\s+(.+?)\s*$')) {
    $existingTitles.Add([string]$m.Groups[1].Value.Trim()) | Out-Null
  }
  $sections = @(Get-Level2Sections -Text $materialized)
  $missing = [System.Collections.Generic.List[object]]::new()
  foreach ($sec in $sections) {
    if (-not $existingTitles.Contains([string]$sec.title)) {
      $missing.Add($sec) | Out-Null
    }
  }
  if ($missing.Count -eq 0) {
    return [pscustomobject]@{
      content = $ExistingText
      changed = $false
      reason = "unchanged"
    }
  }

  $merged = [string]$ExistingText.TrimEnd()
  foreach ($sec in $missing) {
    $merged += [Environment]::NewLine + [Environment]::NewLine + "## " + [string]$sec.title + [Environment]::NewLine + [string]$sec.content.Trim()
  }
  $merged += [Environment]::NewLine

  return [pscustomobject]@{
    content = $merged
    changed = $true
    reason = "appended_missing_sections"
  }
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
  @{ src = ".governance/growth-pack/README.template.md"; dst = "README.md"; kind = "readme_merge" },
  @{ src = ".governance/growth-pack/RELEASE_TEMPLATE.md"; dst = "RELEASE_TEMPLATE.md"; kind = "suggest_only" },
  @{ src = ".governance/growth-pack/CONTRIBUTING.template.md"; dst = "CONTRIBUTING.md"; kind = "suggest_only" },
  @{ src = ".governance/growth-pack/SECURITY.template.md"; dst = "SECURITY.md"; kind = "suggest_only" },
  @{ src = ".governance/growth-pack/ISSUE_TEMPLATE/bug_report.yml"; dst = ".github/ISSUE_TEMPLATE/bug_report.yml"; kind = "suggest_only" },
  @{ src = ".governance/growth-pack/ISSUE_TEMPLATE/feature_request.yml"; dst = ".github/ISSUE_TEMPLATE/feature_request.yml"; kind = "suggest_only" },
  @{ src = ".governance/growth-pack/pull_request_template.md"; dst = ".github/pull_request_template.md"; kind = "suggest_only" }
)

$items = [System.Collections.Generic.List[object]]::new()
$summary = [ordered]@{
  repos = 0
  copied = 0
  merged = 0
  suggested = 0
  skipped = 0
  missing_source = 0
  disabled = 0
}

foreach ($repo in $repos) {
  if (-not (Test-Path -LiteralPath $repo -PathType Container)) {
    continue
  }
  $summary.repos++
  $repoName = Split-Path -Leaf $repo

  if (-not (Test-RootApplyEnabled -Policy $policy -RepoPathAbs $repo)) {
    $summary.disabled++
    $items.Add([pscustomobject]@{ repo = $repo; action = "SKIP_DISABLED"; source = ""; target = ""; strategy = $Strategy }) | Out-Null
    continue
  }

  foreach ($pair in $mapping) {
    $src = Join-Path $repo (($pair.src) -replace '/', '\')
    $dst = Join-Path $repo (($pair.dst) -replace '/', '\')

    if (-not (Test-Path -LiteralPath $src -PathType Leaf)) {
      $summary.missing_source++
      $items.Add([pscustomobject]@{ repo = $repo; action = "SKIP_SOURCE_MISSING"; source = $pair.src; target = $pair.dst; strategy = $Strategy }) | Out-Null
      continue
    }

    $dstExists = Test-Path -LiteralPath $dst -PathType Leaf
    $srcText = Get-Content -LiteralPath $src -Raw

    if (-not $dstExists) {
      if ($Mode -eq "plan") {
        $items.Add([pscustomobject]@{ repo = $repo; action = "PLAN_CREATE"; source = $pair.src; target = $pair.dst; strategy = $Strategy }) | Out-Null
        continue
      }
      $dstDir = Split-Path -Parent $dst
      if (-not (Test-Path -LiteralPath $dstDir -PathType Container)) {
        New-Item -ItemType Directory -Path $dstDir -Force | Out-Null
      }
      $textToWrite = if ([string]$pair.kind -eq "readme_merge") { Materialize-TemplatePlaceholders -TemplateText $srcText -RepoName $repoName } else { $srcText }
      Write-TextFile -Path $dst -Content $textToWrite
      $summary.copied++
      $items.Add([pscustomobject]@{ repo = $repo; action = "CREATE"; source = $pair.src; target = $pair.dst; strategy = $Strategy }) | Out-Null
      continue
    }

    if ($Strategy -eq "skip") {
      $summary.skipped++
      $items.Add([pscustomobject]@{ repo = $repo; action = "SKIP_EXISTS"; source = $pair.src; target = $pair.dst; strategy = $Strategy }) | Out-Null
      continue
    }

    if ($Strategy -eq "overwrite") {
      if ($Mode -eq "plan") {
        $items.Add([pscustomobject]@{ repo = $repo; action = "PLAN_OVERWRITE"; source = $pair.src; target = $pair.dst; strategy = $Strategy }) | Out-Null
        continue
      }
      $textToWrite = if ([string]$pair.kind -eq "readme_merge") { Materialize-TemplatePlaceholders -TemplateText $srcText -RepoName $repoName } else { $srcText }
      Write-TextFile -Path $dst -Content $textToWrite
      $summary.copied++
      $items.Add([pscustomobject]@{ repo = $repo; action = "OVERWRITE"; source = $pair.src; target = $pair.dst; strategy = $Strategy }) | Out-Null
      continue
    }

    if ([string]$pair.kind -eq "readme_merge") {
      $dstText = Get-Content -LiteralPath $dst -Raw
      $merge = Merge-Readme -ExistingText $dstText -TemplateText $srcText -RepoName $repoName
      if (-not [bool]$merge.changed) {
        $summary.skipped++
        $items.Add([pscustomobject]@{ repo = $repo; action = "SKIP_UNCHANGED"; source = $pair.src; target = $pair.dst; strategy = $Strategy; reason = [string]$merge.reason }) | Out-Null
        continue
      }
      if ($Mode -eq "plan") {
        $items.Add([pscustomobject]@{ repo = $repo; action = "PLAN_MERGE"; source = $pair.src; target = $pair.dst; strategy = $Strategy; reason = [string]$merge.reason }) | Out-Null
        continue
      }
      Write-TextFile -Path $dst -Content ([string]$merge.content)
      $summary.merged++
      $items.Add([pscustomobject]@{ repo = $repo; action = "MERGE"; source = $pair.src; target = $pair.dst; strategy = $Strategy; reason = [string]$merge.reason }) | Out-Null
      continue
    }

    $suggestPath = $dst + ".growth-pack.suggested"
    $suggestText = if ([string]$pair.kind -eq "readme_merge") { Materialize-TemplatePlaceholders -TemplateText $srcText -RepoName $repoName } else { $srcText }
    if ($Mode -eq "plan") {
      $items.Add([pscustomobject]@{ repo = $repo; action = "PLAN_SUGGEST"; source = $pair.src; target = $pair.dst; strategy = $Strategy; suggestion = $suggestPath }) | Out-Null
      continue
    }
    Write-TextFile -Path $suggestPath -Content $suggestText
    $summary.suggested++
    $items.Add([pscustomobject]@{ repo = $repo; action = "MERGE_SUGGESTED"; source = $pair.src; target = $pair.dst; strategy = $Strategy; suggestion = $suggestPath }) | Out-Null
  }
}

if ($AsJson) {
  [pscustomobject]@{
    mode = $Mode
    strategy = $Strategy
    overwrite = [bool]($Strategy -eq "overwrite")
    summary = [pscustomobject]$summary
    items = @($items)
  } | ConvertTo-Json -Depth 8 | Write-Output
  exit 0
}

Write-Host ("apply_growth_pack.mode={0}" -f $Mode)
Write-Host ("apply_growth_pack.strategy={0}" -f $Strategy)
Write-Host ("apply_growth_pack.repos={0}" -f $summary.repos)
Write-Host ("apply_growth_pack.copied={0}" -f $summary.copied)
Write-Host ("apply_growth_pack.merged={0}" -f $summary.merged)
Write-Host ("apply_growth_pack.suggested={0}" -f $summary.suggested)
Write-Host ("apply_growth_pack.skipped={0}" -f $summary.skipped)
Write-Host ("apply_growth_pack.missing_source={0}" -f $summary.missing_source)
Write-Host ("apply_growth_pack.disabled={0}" -f $summary.disabled)
