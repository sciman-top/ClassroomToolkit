[CmdletBinding()]
param(
    [string]$Solution = "ClassroomToolkit.sln",
    [string]$PackageSource = "https://api.nuget.org/v3/index.json",
    [switch]$FailOnVulnerability = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$environmentBootstrap = Join-Path $PSScriptRoot "..\env\Initialize-WindowsProcessEnvironment.ps1"
if (Test-Path -LiteralPath $environmentBootstrap) {
    . $environmentBootstrap
}

$scriptHelpersModule = Join-Path $PSScriptRoot "..\artifacts\ScriptHelpers.psm1"
Import-Module -Name $scriptHelpersModule -Force

function Get-VulnerabilityRowsFromDotnetOutput {
    param([Parameter()][AllowNull()][AllowEmptyCollection()][string[]]$Lines)

    $rows = @()
    if ($null -eq $Lines) {
        return $rows
    }

    foreach ($line in $Lines) {
        $trimmed = $line.Trim()
        if (-not $trimmed.StartsWith(">")) {
            continue
        }

        $payload = $trimmed.TrimStart(">").Trim()
        $tokens = [System.Text.RegularExpressions.Regex]::Split($payload, "\s+") | Where-Object { $_.Length -gt 0 }
        if ($tokens.Count -lt 4) {
            continue
        }

        $package = $tokens[0]
        $severity = $tokens[$tokens.Count - 2]
        $advisory = $tokens[$tokens.Count - 1]
        $resolved = $tokens[2]

        $rows += [pscustomobject]@{
            package = $package
            resolved = $resolved
            severity = $severity
            advisory = $advisory
            raw = $line
        }
    }

    return $rows
}

$resolvedSolution = Resolve-AbsolutePath -Path $Solution
if (-not (Test-Path -LiteralPath $resolvedSolution)) {
    throw "[dependency-vulnerability] Missing solution: $resolvedSolution"
}

Write-Host "[dependency-vulnerability] START scan"
$previousLanguage = $env:DOTNET_CLI_UI_LANGUAGE
$env:DOTNET_CLI_UI_LANGUAGE = "en"
try {
    $scanArgs = @(
        "list",
        $resolvedSolution,
        "package",
        "--vulnerable",
        "--include-transitive"
    )
    if (-not [string]::IsNullOrWhiteSpace($PackageSource)) {
        $scanArgs += "--source"
        $scanArgs += $PackageSource
    }

    $scanOutput = & dotnet @scanArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        $detail = Format-DotnetListFailureOutput -Lines @($scanOutput)
        throw "[dependency-vulnerability] dotnet list vulnerable scan failed (exit=$LASTEXITCODE). Output: $detail"
    }
}
finally {
    $env:DOTNET_CLI_UI_LANGUAGE = $previousLanguage
}

$rows = @(Get-VulnerabilityRowsFromDotnetOutput -Lines @($scanOutput))
if ($rows.Length -eq 0) {
    Write-Host "[dependency-vulnerability] PASS no vulnerable packages detected."
    exit 0
}

Write-Host "[dependency-vulnerability] FAIL vulnerable packages detected:"
foreach ($row in $rows) {
    Write-Host ("  - package={0} resolved={1} severity={2} advisory={3}" -f
        $row.package,
        $row.resolved,
        $row.severity,
        $row.advisory)
}

if ($FailOnVulnerability.IsPresent) {
    exit 2
}

Write-Host "[dependency-vulnerability] WARN fail-on-vulnerability disabled; continuing."
exit 0
