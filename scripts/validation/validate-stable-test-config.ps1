[CmdletBinding()]
param(
    [string]$StableTestsScript = "scripts/validation/run-stable-tests.ps1",
    [string]$TestProject = "tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param([Parameter(Mandatory = $true)][string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }
    return Join-Path (Get-Location) $Path
}

$resolvedScriptPath = Resolve-AbsolutePath -Path $StableTestsScript
$resolvedProjectPath = Resolve-AbsolutePath -Path $TestProject

if (-not (Test-Path -LiteralPath $resolvedScriptPath)) {
    throw "[stable-tests-config] Missing script: $resolvedScriptPath"
}

if (-not (Test-Path -LiteralPath $resolvedProjectPath)) {
    throw "[stable-tests-config] Missing test project: $resolvedProjectPath"
}

if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    throw "[stable-tests-config] dotnet command is not available."
}

$scriptSource = Get-Content -LiteralPath $resolvedScriptPath -Raw
if ($scriptSource -notmatch 'ValidateSet\("quick", "standard", "full"\)') {
    throw "[stable-tests-config] Profile ValidateSet contract is missing."
}

if ($scriptSource -notmatch 'Resolve-StableFilter') {
    throw "[stable-tests-config] Resolve-StableFilter definition is missing."
}

$profiles = @("quick", "standard", "full")
foreach ($profile in $profiles) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $resolvedScriptPath `
        -Profile $profile `
        -Configuration "Debug" `
        -TestProject $resolvedProjectPath `
        -SkipBuild `
        -DryRun
    if ($LASTEXITCODE -ne 0) {
        throw "[stable-tests-config] DryRun failed for profile '$profile' (exit=$LASTEXITCODE)."
    }
}

Write-Host "[stable-tests-config] PASS"
