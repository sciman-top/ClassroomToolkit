[CmdletBinding()]
param(
    # Empty means "use the configuration declared by the baseline". An explicit
    # value that disagrees with the baseline is a hard error: CA counts from
    # different configurations are not comparable.
    [string]$Configuration = "",
    [string]$BaselinePath = "scripts/quality/analyzer-backlog-baseline.json",
    [string]$ReportPath = "",
    # Regenerate the baseline from the current scan instead of comparing
    # against it. Use after intentionally fixing or accepting CA findings;
    # keeps the previous notes field and prints the old/new totals.
    [switch]$RefreshBaseline,
    # Skip the implicit NuGet restore in each project build. Only safe when a
    # prior full-solution restore already ran in the same session (quality
    # gates always build the solution first).
    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$artifactLayoutModule = Join-Path $PSScriptRoot "..\artifacts\ArtifactLayout.psm1"
Import-Module -Name $artifactLayoutModule -Force
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path (Get-ClassroomToolkitArtifactPath -Name EvidenceQualityCurrent) "analyzer-backlog-report.json"
}

$environmentBootstrap = Join-Path $PSScriptRoot "..\env\Initialize-WindowsProcessEnvironment.ps1"
if (Test-Path -LiteralPath $environmentBootstrap) {
    . $environmentBootstrap
}

$scriptHelpersModule = Join-Path $PSScriptRoot "..\artifacts\ScriptHelpers.psm1"
Import-Module -Name $scriptHelpersModule -Force

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
$resolvedBaselinePath = Join-Path $repoRoot $BaselinePath
$resolvedReportPath = Join-Path $repoRoot $ReportPath
$srcRoot = Join-Path $repoRoot "src"

function Convert-ToRepoRelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$Path
    )

    try {
        $fullPath = [System.IO.Path]::GetFullPath($Path)
        return [System.IO.Path]::GetRelativePath($repoRoot, $fullPath).Replace('\', '/')
    }
    catch {
        return $Path
    }
}

function Test-IsTransientAnalyzerBuildFailure {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][AllowEmptyCollection()][string[]]$Output
    )

    # 输出为空说明 dotnet 根本没起来（启动瞬态失败），按可重试处理而非参数绑定崩溃。
    if ($Output.Count -eq 0) {
        return $true
    }

    $outputText = [string]::Join([Environment]::NewLine, @($Output))
    $isFileLock = (Test-ContainsOrdinalIgnoreCase -Text $outputText -Value "because it is being used by another process") `
        -or (Test-ContainsOrdinalIgnoreCase -Text $outputText -Value "已被另一进程使用")
    $isWpfTempProjectDrift = (Test-ContainsOrdinalIgnoreCase -Text $outputText -Value "_wpftmp.csproj") `
        -and (
            (Test-ContainsOrdinalIgnoreCase -Text $outputText -Value ".g.cs") `
            -or (Test-ContainsOrdinalIgnoreCase -Text $outputText -Value "InitializeComponent") `
            -or (Test-ContainsOrdinalIgnoreCase -Text $outputText -Value "未能找到源文件")
        )

    return $isFileLock -or $isWpfTempProjectDrift
}

function Invoke-AnalyzerBuild {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$ProjectName,
        [Parameter(Mandatory = $true)][string]$Configuration,
        [Parameter()][switch]$NoRestore,
        [Parameter()][int]$RetryCount = 1,
        [Parameter()][int]$RetryDelaySeconds = 2
    )

    $attempt = 0
    while ($true) {
        $attempt++
        $buildArgs = @(
            "build", $ProjectPath,
            "-c", $Configuration,
            "--no-incremental",
            "-m:1",
            "-p:TreatWarningsAsErrors=false",
            "-p:EnableNETAnalyzers=true",
            "-p:AnalysisLevel=latest-all"
        )
        if ($NoRestore) {
            $buildArgs += "--no-restore"
        }
        $output = @(& dotnet @buildArgs 2>&1)

        # dotnet build 正常必有输出；空输出即便退出码为 0 也按失败处理，
        # 否则该项目会被静默漏扫，analyzer 门禁假绿。
        if ($LASTEXITCODE -eq 0 -and @($output).Count -gt 0) {
            return $output
        }

        if ($attempt -le $RetryCount -and (Test-IsTransientAnalyzerBuildFailure -Output $output)) {
            Write-Warning ("[analyzer-backlog] RETRY {0} after transient analyzer build failure (attempt={1})" -f $ProjectName, $attempt)
            & dotnet build-server shutdown 2>&1 | ForEach-Object { Write-Host $_ }
            Start-Sleep -Seconds $RetryDelaySeconds
            continue
        }

        throw ("[analyzer-backlog] dotnet build failed for {0} (exit={1})" -f $ProjectName, $LASTEXITCODE)
    }
}

if (-not (Test-Path -LiteralPath $resolvedBaselinePath)) {
    throw "[analyzer-backlog] Missing baseline file: $BaselinePath"
}

$baseline = Get-Content -LiteralPath $resolvedBaselinePath -Raw | ConvertFrom-Json
$baselineConfiguration = [string]$baseline.configuration
if ([string]::IsNullOrWhiteSpace($Configuration)) {
    $Configuration = $baselineConfiguration
}
elseif (-not [string]::Equals($Configuration, $baselineConfiguration, [StringComparison]::OrdinalIgnoreCase)) {
    throw ("[analyzer-backlog] Configuration mismatch: baseline was collected under '{0}' but check requested '{1}'. " +
        "CA counts are only comparable within the same configuration; rerun with -Configuration {0} or regenerate the baseline.") -f $baselineConfiguration, $Configuration
}

$projectFiles = Get-ChildItem -LiteralPath $srcRoot -Filter "*.csproj" -File -Recurse | Sort-Object FullName
if (-not $projectFiles) {
    throw "[analyzer-backlog] No src csproj files found under: $srcRoot"
}

$diagnostics = New-Object System.Collections.Generic.List[object]
foreach ($project in $projectFiles) {
    Write-Host ("[analyzer-backlog] SCAN {0}" -f $project.Name)
    $output = Invoke-AnalyzerBuild -ProjectPath $project.FullName -ProjectName $project.Name -Configuration $Configuration -NoRestore:$NoRestore

    foreach ($line in $output) {
        if ($line -match "^(?<file>.+?)\((?<line>\d+),(?<column>\d+)\):\s+(?:warning|error)\s+(?<rule>CA\d{4}):") {
            [void]$diagnostics.Add([pscustomobject]@{
                project = $project.Name
                file = Convert-ToRepoRelativePath -Path $matches.file
                line = [int]$matches.line
                column = [int]$matches.column
                rule = $matches.rule
            })
        }
    }
}

$uniqueDiagnostics = @($diagnostics |
    Sort-Object project, file, line, column, rule -Unique)

$projectCounts = @($uniqueDiagnostics |
    Group-Object project |
    Sort-Object Name |
    ForEach-Object {
        [pscustomobject]@{
            project = $_.Name
            count = $_.Count
        }
    })

$ruleCounts = @($uniqueDiagnostics |
    Group-Object rule |
    Sort-Object Name |
    ForEach-Object {
        [pscustomobject]@{
            rule = $_.Name
            count = $_.Count
        }
    })

$diagnosticDetails = @($uniqueDiagnostics |
    Sort-Object project, file, line, column, rule |
    ForEach-Object {
        [pscustomobject]@{
            project = $_.project
            file = $_.file
            line = $_.line
            column = $_.column
            rule = $_.rule
        }
    })

$report = [pscustomobject]@{
    generated_at_utc = [DateTime]::UtcNow.ToString("o")
    configuration = $Configuration
    scope = "src-projects"
    analysis_level = "latest-all"
    diagnostics_total = @($uniqueDiagnostics).Count
    project_counts = $projectCounts
    rule_counts = $ruleCounts
    diagnostics = $diagnosticDetails
}

$reportDirectory = Split-Path -Parent $resolvedReportPath
if (-not [string]::IsNullOrWhiteSpace($reportDirectory)) {
    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
}

$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $resolvedReportPath -Encoding UTF8

if ($RefreshBaseline) {
    $previousTotal = $null
    $previousNotes = $null
    if (Test-Path -LiteralPath $resolvedBaselinePath) {
        $previous = Get-Content -LiteralPath $resolvedBaselinePath -Raw | ConvertFrom-Json
        $previousTotal = [int]$previous.diagnostics_total
        $previousNotes = $previous.notes
    }

    # 基线正文与 report 同构；notes 手工维护的豁免理由在刷新时保留。
    $refreshed = [pscustomobject]@{
        generated_at_utc = $report.generated_at_utc
        configuration = $Configuration
        scope = "src-projects"
        analysis_level = "latest-all"
        diagnostics_total = $report.diagnostics_total
        project_counts = $report.project_counts
        rule_counts = $report.rule_counts
        diagnostics = $report.diagnostics
    }
    if ($previousNotes) {
        $refreshed | Add-Member -NotePropertyName notes -NotePropertyValue $previousNotes
    }
    $refreshed | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $resolvedBaselinePath -Encoding UTF8

    Write-Host ("[analyzer-backlog] BASELINE REFRESHED total {0} -> {1} baseline={2}" -f $previousTotal, @($uniqueDiagnostics).Count, $BaselinePath)
    exit 0
}

$baselineRuleMap = @{}
foreach ($entry in @($baseline.rule_counts)) {
    $baselineRuleMap[$entry.rule] = [int]$entry.count
}
$baselineProjectMap = @{}
foreach ($entry in @($baseline.project_counts)) {
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

Write-Host ("[analyzer-backlog] PASS total={0} report={1}" -f @($uniqueDiagnostics).Count, $ReportPath)
exit 0
