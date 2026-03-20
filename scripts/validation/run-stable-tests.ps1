param(
    [string]$Configuration = "Debug",
    [string]$TestProject = "tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj",
    [switch]$SkipBuild,
    [ValidateSet("quick", "standard", "full")]
    [string]$Profile = "standard",
    [string]$FilterConfigPath = (Join-Path $PSScriptRoot "stable-test-filters.json"),
    [string]$OutputJsonPath = "artifacts/TestResults/stable-tests-summary.json",
    [string[]]$Filters = @(
        "FullyQualifiedName~ContractTests|FullyQualifiedName~InkReplay",
        "FullyQualifiedName~InkExportCoordinateInvariantTests|FullyQualifiedName~InkExportServiceTests|FullyQualifiedName~InkPersistenceServiceTests|FullyQualifiedName~InkWriteAheadLogServiceTests|FullyQualifiedName~CrossPagePointerUpDecisionPolicyTests|FullyQualifiedName~CrossPagePointerUpExecutionPlanPolicyTests|FullyQualifiedName~CrossPagePointerUpDeferredStatePolicyTests|FullyQualifiedName~CrossPagePointerUpPostExecutionPolicyTests|FullyQualifiedName~CrossPagePostInputDelayPolicyTests|FullyQualifiedName~CrossPageDeferredRefreshCoordinatorTests|FullyQualifiedName~CrossPageUpdateSourceClassifierTests|FullyQualifiedName~CrossPageUpdateSourceParserTests"
    )
)

$ErrorActionPreference = "Stop"

function Invoke-DotNetWithRetry {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Args,
        [Parameter(Mandatory = $true)]
        [string]$StepName,
        [int]$MaxRetries = 3
    )

    for ($attempt = 1; $attempt -le $MaxRetries; $attempt++)
    {
        Write-Host "[stable-tests] $StepName (attempt $attempt/$MaxRetries)"
        $output = & dotnet @Args 2>&1
        $exitCode = $LASTEXITCODE
        $output | ForEach-Object { Write-Host $_ }

        if ($exitCode -eq 0)
        {
            $combined = ($output | Out-String)
            $noMatch = $combined -match "没有测试匹配" -or $combined -match "No test matches"
            if ($noMatch)
            {
                throw "[stable-tests] $StepName produced no matching tests."
            }

            return
        }

        $combined = ($output | Out-String)
        $isLockConflict = $combined -match "CS2012" -or $combined -match "MSB3026" -or $combined -match "being used by another process"
        if ($isLockConflict -and $attempt -lt $MaxRetries)
        {
            Start-Sleep -Milliseconds 1200
            continue
        }

        throw "[stable-tests] $StepName failed with exit code $exitCode."
    }
}

function Resolve-OutputPath {
    param([string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path (Get-Location) $Path
}

function Invoke-StepAndRecord {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Args,
        [Parameter(Mandatory = $true)]
        [string]$StepName,
        [string]$FilterText = ""
    )

    $startedAt = [DateTimeOffset]::UtcNow
    try {
        Invoke-DotNetWithRetry -Args $Args -StepName $StepName
        $finishedAt = [DateTimeOffset]::UtcNow
        $durationMs = [Math]::Round(($finishedAt - $startedAt).TotalMilliseconds)
        $script:RunEntries.Add([ordered]@{
            step = $StepName
            filter = $FilterText
            status = "passed"
            startedUtc = $startedAt.ToString("o")
            finishedUtc = $finishedAt.ToString("o")
            durationMs = $durationMs
        }) | Out-Null
    }
    catch {
        $finishedAt = [DateTimeOffset]::UtcNow
        $durationMs = [Math]::Round(($finishedAt - $startedAt).TotalMilliseconds)
        $script:RunEntries.Add([ordered]@{
            step = $StepName
            filter = $FilterText
            status = "failed"
            startedUtc = $startedAt.ToString("o")
            finishedUtc = $finishedAt.ToString("o")
            durationMs = $durationMs
            error = $_.Exception.Message
        }) | Out-Null
        throw
    }
}

function Write-RunSummary {
    param(
        [string]$Path,
        [string]$RunStatus
    )

    $fullPath = Resolve-OutputPath -Path $Path
    $directory = Split-Path -Parent $fullPath
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $totalDurationMs = 0
    foreach ($entry in $script:RunEntries)
    {
        if ($entry.Contains("durationMs"))
        {
            $totalDurationMs += [int]$entry["durationMs"]
        }
    }

    $summary = [ordered]@{
        profile = $Profile
        configuration = $Configuration
        testProject = $TestProject
        status = $RunStatus
        generatedUtc = [DateTimeOffset]::UtcNow.ToString("o")
        totalDurationMs = $totalDurationMs
        entries = $script:RunEntries
        entriesByDurationDesc = @($script:RunEntries | Sort-Object -Property { [int]$_.durationMs } -Descending)
    }

    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $fullPath -Encoding UTF8
    Write-Host "[stable-tests] Summary written: $fullPath"

    $entryDir = Join-Path $directory "stable-tests-entries"
    New-Item -ItemType Directory -Path $entryDir -Force | Out-Null
    for ($i = 0; $i -lt $script:RunEntries.Count; $i++)
    {
        $entry = $script:RunEntries[$i]
        $safeBase = if ([string]::IsNullOrWhiteSpace($entry.filter)) {
            "full-suite"
        }
        else {
            $normalized = ($entry.filter -replace '[^A-Za-z0-9._-]', '_')
            if ($normalized.Length -gt 80) { $normalized.Substring(0, 80) } else { $normalized }
        }
        $entryPath = Join-Path $entryDir ("{0:00}-{1}.json" -f ($i + 1), $safeBase)
        $entry | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $entryPath -Encoding UTF8
    }

    Write-Host "[stable-tests] Entry summaries written: $entryDir"
}

$script:RunEntries = New-Object System.Collections.Generic.List[object]
$runStatus = "passed"

if (-not $SkipBuild)
{
    Invoke-StepAndRecord -Args @("build", $TestProject, "-c", $Configuration) -StepName "Build test project once"
}

if (-not $PSBoundParameters.ContainsKey("Filters"))
{
    if (Test-Path -LiteralPath $FilterConfigPath)
    {
        $config = Get-Content -LiteralPath $FilterConfigPath -Raw | ConvertFrom-Json
        if ($null -eq $config)
        {
            throw "[stable-tests] Invalid filter config payload ($FilterConfigPath)."
        }

        if ($null -ne $config.profiles)
        {
            $profileFilters = $config.profiles.$Profile
            if ($null -eq $profileFilters)
            {
                throw "[stable-tests] Profile '$Profile' not found in $FilterConfigPath."
            }

            $Filters = @($profileFilters)
        }
        elseif ($null -ne $config.filters)
        {
            # Backward compatibility for old format.
            $Filters = @($config.filters | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        }
        else
        {
            throw "[stable-tests] Invalid filter config: missing 'profiles' and legacy 'filters' ($FilterConfigPath)."
        }
    }
}

if ($null -eq $Filters -or $Filters.Count -eq 0)
{
    throw "[stable-tests] No test filters configured."
}

try {
    Write-Host "[stable-tests] Using profile: $Profile"

    foreach ($filter in $Filters)
    {
        if ([string]::IsNullOrWhiteSpace($filter))
        {
            Invoke-StepAndRecord -Args @("test", $TestProject, "-c", $Configuration, "--no-build") -StepName "Run full test suite"
        }
        else
        {
            Invoke-StepAndRecord -Args @("test", $TestProject, "-c", $Configuration, "--no-build", "--filter", $filter) -StepName "Run filter: $filter" -FilterText $filter
        }
    }

    Write-Host "[stable-tests] Completed."
}
catch {
    $runStatus = "failed"
    throw
}
finally {
    Write-RunSummary -Path $OutputJsonPath -RunStatus $runStatus
}
