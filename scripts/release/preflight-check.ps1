param(
    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-PathExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required path missing: $Path"
    }
}

function Invoke-DotnetOrThrow {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & dotnet @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Assert-DesktopRuntimeInstallerExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PrereqDirectory,
        [Parameter(Mandatory = $true)]
        [string]$RuntimeMajor
    )

    if (-not (Test-Path -LiteralPath $PrereqDirectory)) {
        throw "Runtime prereq directory missing: $PrereqDirectory"
    }

    $installer = Get-ChildItem -LiteralPath $PrereqDirectory -Filter "*desktop-runtime*$RuntimeMajor*win-x64*.exe" -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $installer) {
        throw "Runtime installer not found in $PrereqDirectory for major $RuntimeMajor (win-x64)."
    }
}

function Assert-PathExistsOrThrow {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required artifact missing: $Path"
    }
}

function Assert-WindowsDesktopRuntimeMajor {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RuntimeConfigPath,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedMajor
    )

    Assert-PathExistsOrThrow -Path $RuntimeConfigPath
    $runtimeConfig = Get-Content -LiteralPath $RuntimeConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($null -eq $runtimeConfig.runtimeOptions -or $null -eq $runtimeConfig.runtimeOptions.frameworks) {
        throw "runtimeconfig missing frameworks node: $RuntimeConfigPath"
    }

    $desktopFramework = $runtimeConfig.runtimeOptions.frameworks |
        Where-Object { $_.name -eq "Microsoft.WindowsDesktop.App" } |
        Select-Object -First 1
    if ($null -eq $desktopFramework) {
        throw "Microsoft.WindowsDesktop.App not declared in runtimeconfig: $RuntimeConfigPath"
    }

    if (-not ($desktopFramework.version -like "$ExpectedMajor.*")) {
        throw "Unexpected WindowsDesktop runtime major in runtimeconfig: $($desktopFramework.version), expected $ExpectedMajor.x"
    }

    return [string]$desktopFramework.version
}

function Invoke-PublishCompatibilityProbe {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    $probeRoot = Join-Path $RepoRoot "artifacts\release\preflight-compatibility-probe"
    $fddDir = Join-Path $probeRoot "fdd"
    $scdDir = Join-Path $probeRoot "scd"

    if (Test-Path -LiteralPath $probeRoot) {
        Remove-Item -LiteralPath $probeRoot -Recurse -Force
    }
    New-Item -Path $fddDir -ItemType Directory -Force | Out-Null
    New-Item -Path $scdDir -ItemType Directory -Force | Out-Null

    Push-Location $RepoRoot
    try {
        Invoke-DotnetOrThrow -Arguments @(
            "publish",
            $ProjectPath,
            "-c", "Release",
            "-r", "win-x64",
            "--self-contained", "false",
            "-p:PublishSingleFile=false",
            "-p:PublishTrimmed=false",
            "-o", $fddDir
        )

        Invoke-DotnetOrThrow -Arguments @(
            "publish",
            $ProjectPath,
            "-c", "Release",
            "-r", "win-x64",
            "--self-contained", "true",
            "-p:PublishSingleFile=false",
            "-p:PublishTrimmed=false",
            "-o", $scdDir
        )
    }
    finally {
        Pop-Location
    }

    $runtimeConfigPath = Join-Path $fddDir "ClassroomToolkit.App.runtimeconfig.json"
    $windowsDesktopRuntimeVersion = Assert-WindowsDesktopRuntimeMajor -RuntimeConfigPath $runtimeConfigPath -ExpectedMajor "10"

    $fddPdfiumPath = Join-Path $fddDir "x64\pdfium.dll"
    $fddSqlitePath = Join-Path $fddDir "e_sqlite3.dll"
    Assert-PathExistsOrThrow -Path $fddPdfiumPath
    Assert-PathExistsOrThrow -Path $fddSqlitePath

    $scdHostFxrPath = Join-Path $scdDir "hostfxr.dll"
    $scdCoreClrPath = Join-Path $scdDir "coreclr.dll"
    $scdVcruntimePath = Join-Path $scdDir "vcruntime140_cor3.dll"
    $scdSqlitePath = Join-Path $scdDir "e_sqlite3.dll"
    Assert-PathExistsOrThrow -Path $scdHostFxrPath
    Assert-PathExistsOrThrow -Path $scdCoreClrPath
    Assert-PathExistsOrThrow -Path $scdVcruntimePath
    Assert-PathExistsOrThrow -Path $scdSqlitePath

    return [pscustomobject]@{
        GeneratedAtUtc = [DateTime]::UtcNow.ToString("o")
        RuntimeConfigPath = $runtimeConfigPath
        WindowsDesktopRuntimeVersion = $windowsDesktopRuntimeVersion
        Outputs = [pscustomobject]@{
            Fdd = $fddDir
            Scd = $scdDir
        }
        RequiredArtifacts = [pscustomobject]@{
            FddPdfium = $fddPdfiumPath
            FddSqlite = $fddSqlitePath
            ScdHostFxr = $scdHostFxrPath
            ScdCoreClr = $scdCoreClrPath
            ScdVcruntime = $scdVcruntimePath
            ScdSqlite = $scdSqlitePath
        }
    }
}

function Get-PresentationSignatureMatrixSummary {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot
    )

    $matrixPath = Join-Path $RepoRoot "tests\ClassroomToolkit.Tests\Fixtures\presentation-classifier-compatibility-matrix.json"
    if (-not (Test-Path -LiteralPath $matrixPath)) {
        throw "Presentation signature matrix missing: $matrixPath"
    }

    $matrix = Get-Content -LiteralPath $matrixPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $classification = @($matrix.classification)
    $slideshow = @($matrix.slideshow)

    return [pscustomobject]@{
        MatrixPath = $matrixPath
        ClassificationCount = $classification.Count
        SlideshowCount = $slideshow.Count
    }
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot "..\..")).Path
$csprojPath = Join-Path $repoRoot "src\ClassroomToolkit.App\ClassroomToolkit.App.csproj"

Assert-PathExists -Path (Join-Path $scriptRoot "prepare-distribution.ps1")
Assert-PathExists -Path (Join-Path $scriptRoot "templates\start-standard.bat")
Assert-PathExists -Path (Join-Path $scriptRoot "templates\bootstrap-runtime.ps1")
Assert-PathExists -Path (Join-Path $scriptRoot "templates\user-guide-standard.md")
Assert-PathExists -Path (Join-Path $scriptRoot "templates\user-guide-offline.md")
Assert-DesktopRuntimeInstallerExists -PrereqDirectory (Join-Path $scriptRoot "prereq") -RuntimeMajor "10"
Assert-PathExists -Path (Join-Path $repoRoot "docs\runbooks\release-prevention-checklist.md")

[xml]$csprojXml = Get-Content -LiteralPath $csprojPath -Raw -Encoding UTF8
$propertyGroup = $csprojXml.Project.PropertyGroup | Select-Object -First 1
if ($null -eq $propertyGroup) {
    throw "csproj property group missing: $csprojPath"
}

$expectedCompany = "sciman$([char]0x9038)$([char]0x5C45)"
if ($propertyGroup.Authors -ne "sciman") {
    throw "Unexpected Authors in csproj. Current=$($propertyGroup.Authors)"
}
if ($propertyGroup.Company -ne $expectedCompany) {
    throw "Unexpected Company in csproj. Current=$($propertyGroup.Company)"
}
if ($propertyGroup.Product -ne "ClassroomToolkit") {
    throw "Unexpected Product in csproj. Current=$($propertyGroup.Product)"
}
if ([string]::IsNullOrWhiteSpace($propertyGroup.Description)) {
    throw "Description missing in csproj."
}

if (-not $SkipTests) {
    Push-Location $repoRoot
    try {
        Invoke-DotnetOrThrow -Arguments @(
            "test",
            "tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj",
            "-c", "Debug",
            "--filter",
            "FullyQualifiedName~PresentationControlServiceTests|FullyQualifiedName~PresentationClassifierTests|FullyQualifiedName~PresentationClassifierOverridesTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~RollCallRemoteHookLifecycleContractTests|FullyQualifiedName~ConfigurationServiceTests"
        )
    }
    finally {
        Pop-Location
    }
}

$probeReport = Invoke-PublishCompatibilityProbe -RepoRoot $repoRoot -ProjectPath "src/ClassroomToolkit.App/ClassroomToolkit.App.csproj"
$presentationSignatureSummary = Get-PresentationSignatureMatrixSummary -RepoRoot $repoRoot
$probeReportPath = Join-Path $repoRoot "artifacts\release\preflight-compatibility-report.json"
([pscustomobject]@{
    GeneratedAtUtc = [DateTime]::UtcNow.ToString("o")
    PublishProbe = $probeReport
    PresentationSignatureCoverage = $presentationSignatureSummary
}) | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $probeReportPath -Encoding UTF8

Write-Host "Release preflight check passed."
Write-Host "Compatibility report generated: $probeReportPath"
