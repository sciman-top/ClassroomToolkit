param(
    [string]$Configuration = "Debug",
    [string]$TestProject = "tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj",
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

Invoke-DotNetWithRetry -Args @("build", $TestProject, "-c", $Configuration) -StepName "Build test project once"

foreach ($filter in $Filters)
{
    Invoke-DotNetWithRetry -Args @("test", $TestProject, "-c", $Configuration, "--no-build", "--filter", $filter) -StepName "Run filter: $filter"
}

Write-Host "[stable-tests] Completed."
