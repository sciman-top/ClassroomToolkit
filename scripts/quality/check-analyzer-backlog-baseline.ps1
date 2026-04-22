[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [string]$BaselinePath = "scripts/quality/analyzer-backlog-baseline.json",
    [string]$ReportPath = "artifacts/quality/analyzer-backlog-report.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
$resolvedBaselinePath = Join-Path $repoRoot $BaselinePath
$resolvedReportPath = Join-Path $repoRoot $ReportPath
$srcRoot = Join-Path $repoRoot "src"

if (-not (Test-Path -LiteralPath $resolvedBaselinePath)) {
    throw "[analyzer-backlog] Missing baseline file: $BaselinePath"
}

$projectFiles = Get-ChildItem -Path $srcRoot -Filter "*.csproj" -File -Recurse | Sort-Object FullName
if (-not $projectFiles) {
    throw "[analyzer-backlog] No src csproj files found under: $srcRoot"
}

$diagnostics = New-Object System.Collections.Generic.List[object]
foreach ($project in $projectFiles) {
    Write-Host ("[analyzer-backlog] SCAN {0}" -f $project.Name)
    $output = & dotnet build $project.FullName `
        -c $Configuration `
        --no-incremental `
        -m:1 `
        -p:TreatWarningsAsErrors=false `
        -p:EnableNETAnalyzers=true `
        -p:AnalysisLevel=latest-all 2>&1

    if ($LASTEXITCODE -ne 0) {
        throw ("[analyzer-backlog] dotnet build failed for {0} (exit={1})" -f $project.Name, $LASTEXITCODE)
    }

    foreach ($line in $output) {
        if ($line -match "^(?<file>.+?)\((?<line>\d+),(?<column>\d+)\):\s+(?:warning|error)\s+(?<rule>CA\d{4}):") {
            [void]$diagnostics.Add([pscustomobject]@{
                project = $project.Name
                file = $matches.file
                line = [int]$matches.line
                column = [int]$matches.column
                rule = $matches.rule
            })
        }
    }
}

$uniqueDiagnostics = $diagnostics |
    Sort-Object project, file, line, column, rule -Unique

$projectCounts = $uniqueDiagnostics |
    Group-Object project |
    Sort-Object Name |
    ForEach-Object {
        [pscustomobject]@{
            project = $_.Name
            count = $_.Count
        }
    }

$ruleCounts = $uniqueDiagnostics |
    Group-Object rule |
    Sort-Object Name |
    ForEach-Object {
        [pscustomobject]@{
            rule = $_.Name
            count = $_.Count
        }
    }

$report = [pscustomobject]@{
    generated_at_utc = [DateTime]::UtcNow.ToString("o")
    configuration = $Configuration
    scope = "src-projects"
    analysis_level = "latest-all"
    diagnostics_total = $uniqueDiagnostics.Count
    project_counts = $projectCounts
    rule_counts = $ruleCounts
}

$reportDirectory = Split-Path -Parent $resolvedReportPath
if (-not [string]::IsNullOrWhiteSpace($reportDirectory)) {
    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
}

$report | ConvertTo-Json -Depth 6 | Set-Content -Path $resolvedReportPath -Encoding UTF8

$baseline = Get-Content -Path $resolvedBaselinePath -Raw | ConvertFrom-Json
$baselineRuleMap = @{}
foreach ($entry in $baseline.rule_counts) {
    $baselineRuleMap[$entry.rule] = [int]$entry.count
}
$baselineProjectMap = @{}
foreach ($entry in $baseline.project_counts) {
    $baselineProjectMap[$entry.project] = [int]$entry.count
}

$regressions = New-Object System.Collections.Generic.List[string]

foreach ($entry in $ruleCounts) {
    $rule = $entry.rule
    $current = [int]$entry.count
    if (-not $baselineRuleMap.ContainsKey($rule)) {
        [void]$regressions.Add(("new CA rule detected: {0}={1}" -f $rule, $current))
        continue
    }

    $baselineCount = $baselineRuleMap[$rule]
    if ($current -gt $baselineCount) {
        [void]$regressions.Add(("rule regression: {0} baseline={1} current={2}" -f $rule, $baselineCount, $current))
    }
}

foreach ($entry in $projectCounts) {
    $projectName = $entry.project
    $current = [int]$entry.count
    if (-not $baselineProjectMap.ContainsKey($projectName)) {
        [void]$regressions.Add(("new project detected in analyzer scope: {0}={1}" -f $projectName, $current))
        continue
    }

    $baselineCount = $baselineProjectMap[$projectName]
    if ($current -gt $baselineCount) {
        [void]$regressions.Add(("project regression: {0} baseline={1} current={2}" -f $projectName, $baselineCount, $current))
    }
}

if ($regressions.Count -gt 0) {
    Write-Host "[analyzer-backlog] FAIL"
    foreach ($message in $regressions) {
        Write-Host ("- {0}" -f $message)
    }
    Write-Host ("[analyzer-backlog] report: {0}" -f $ReportPath)
    exit 1
}

Write-Host ("[analyzer-backlog] PASS total={0} report={1}" -f $uniqueDiagnostics.Count, $ReportPath)
exit 0
