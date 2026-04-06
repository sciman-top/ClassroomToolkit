param(
    [string]$Configuration = "Debug",
    [string]$Solution = "ClassroomToolkit.sln",
    [string]$TestProject = "tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj",
    [string]$HotspotScript = "scripts/quality/check-hotspot-line-budgets.ps1",
    [string]$ContractFilter = "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests",
    [bool]$IncludeRelease = $true,
    [switch]$SkipManualRegression,
    [string]$ManualSkipReason = "User explicitly requested skipping manual final regression gate.",
    [string]$OutputPath = "",
    [string]$ManualGateExpiresAt = "",
    [string]$RecoveryPlan = "Run docs/validation/manual-final-regression-checklist.md and update acceptance docs before next release."
)

$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param([Parameter(Mandatory = $true)][string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }
    return Join-Path (Get-Location) $Path
}

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string[]]$Command
    )

    $started = [DateTimeOffset]::UtcNow
    $output = & $Command[0] @($Command | Select-Object -Skip 1) 2>&1
    $exitCode = $LASTEXITCODE
    $finished = [DateTimeOffset]::UtcNow
    $durationMs = [Math]::Round(($finished - $started).TotalMilliseconds)
    $outputText = ($output | Out-String).Trim()
    if ($outputText.Length -gt 4000) {
        $outputText = $outputText.Substring(0, 4000) + "`n...[truncated]"
    }

    $script:StepResults.Add([ordered]@{
        name = $Name
        command = [string]::Join(" ", $Command)
        exit_code = $exitCode
        started_utc = $started.ToString("o")
        finished_utc = $finished.ToString("o")
        duration_ms = $durationMs
        key_output = $outputText
    }) | Out-Null

    if ($exitCode -ne 0) {
        throw "[final-acceptance] Step failed: $Name (exit=$exitCode)"
    }
}

function New-DefaultOutputPath {
    $date = Get-Date -Format "yyyy-MM-dd"
    return "docs/validation/evidence/$date-auto-final-acceptance.md"
}

$script:StepResults = New-Object System.Collections.Generic.List[object]

$resolvedOutputPath = if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    Resolve-AbsolutePath -Path (New-DefaultOutputPath)
}
else {
    Resolve-AbsolutePath -Path $OutputPath
}

$manualExpires = if ([string]::IsNullOrWhiteSpace($ManualGateExpiresAt)) {
    (Get-Date).AddDays(30).ToString("yyyy-MM-dd")
}
else {
    $ManualGateExpiresAt
}

Invoke-Step -Name "build" -Command @("dotnet", "build", $Solution, "-c", $Configuration)
Invoke-Step -Name "test" -Command @("dotnet", "test", $TestProject, "-c", $Configuration)
Invoke-Step -Name "contract_invariant" -Command @("dotnet", "test", $TestProject, "-c", $Configuration, "--filter", $ContractFilter)
Invoke-Step -Name "hotspot" -Command @("powershell", "-File", $HotspotScript)

if ($IncludeRelease) {
    Invoke-Step -Name "release_test" -Command @("dotnet", "test", $TestProject, "-c", "Release")
}

$outputDirectory = Split-Path -Parent $resolvedOutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -Path $outputDirectory -ItemType Directory -Force | Out-Null
}

$manualGateStatus = if ($SkipManualRegression.IsPresent) { "gate_na" } else { "required" }
$manualAlternative = if ($SkipManualRegression.IsPresent) {
    "Use automated gate sequence build->test->contract/invariant->hotspot" + $(if ($IncludeRelease) { "->release_test" } else { "" }) + " as temporary alternative evidence."
}
else {
    "N/A"
}

$relativeEvidencePath = $resolvedOutputPath
if ($resolvedOutputPath.StartsWith((Get-Location).Path, [System.StringComparison]::OrdinalIgnoreCase)) {
    $relativeEvidencePath = $resolvedOutputPath.Substring((Get-Location).Path.Length).TrimStart('\', '/')
}

$builder = New-Object System.Text.StringBuilder
$null = $builder.AppendLine("# Final Acceptance Evidence")
$null = $builder.AppendLine("")
$null = $builder.AppendLine("- generated_at_utc: $([DateTimeOffset]::UtcNow.ToString('o'))")
$null = $builder.AppendLine("- configuration: $Configuration")
$null = $builder.AppendLine("- solution: $Solution")
$null = $builder.AppendLine("- test_project: $TestProject")
$null = $builder.AppendLine("- output_path: $relativeEvidencePath")
$null = $builder.AppendLine("")
$null = $builder.AppendLine("## Gate Result")
$null = $builder.AppendLine("- status: passed")
$null = $builder.AppendLine("- gate_order: build -> test -> contract/invariant -> hotspot" + $(if ($IncludeRelease) { " -> release_test" } else { "" }))
$null = $builder.AppendLine("")
$null = $builder.AppendLine("## Manual Gate")
$null = $builder.AppendLine("- manual_final_regression: $manualGateStatus")
if ($SkipManualRegression.IsPresent) {
    $null = $builder.AppendLine("- reason: $ManualSkipReason")
    $null = $builder.AppendLine("- alternative_verification: $manualAlternative")
    $null = $builder.AppendLine("- evidence_link: $relativeEvidencePath")
    $null = $builder.AppendLine("- expires_at: $manualExpires")
    $null = $builder.AppendLine("- recovery_plan: $RecoveryPlan")
}
$null = $builder.AppendLine("")
$null = $builder.AppendLine("## Commands")
for ($i = 0; $i -lt $script:StepResults.Count; $i++) {
    $step = $script:StepResults[$i]
    $null = $builder.AppendLine("### Step $($i + 1): $($step.name)")
    $null = $builder.AppendLine("- cmd: $($step.command)")
    $null = $builder.AppendLine("- exit_code: $($step.exit_code)")
    $null = $builder.AppendLine("- started_utc: $($step.started_utc)")
    $null = $builder.AppendLine("- finished_utc: $($step.finished_utc)")
    $null = $builder.AppendLine("- duration_ms: $($step.duration_ms)")
    $null = $builder.AppendLine("- key_output:")
    $null = $builder.AppendLine('```text')
    $null = $builder.AppendLine($step.key_output)
    $null = $builder.AppendLine('```')
    $null = $builder.AppendLine("")
}

[System.IO.File]::WriteAllText($resolvedOutputPath, $builder.ToString(), [System.Text.Encoding]::UTF8)
Write-Host "[final-acceptance] Evidence written: $resolvedOutputPath"


