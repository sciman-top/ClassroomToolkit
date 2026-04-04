param(
    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-ReleaseConfig {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConfigPath
    )

    if (-not (Test-Path -LiteralPath $ConfigPath)) {
        throw "Release config missing: $ConfigPath"
    }

    $config = Get-Content -LiteralPath $ConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($null -eq $config.Runtime -or $null -eq $config.Publish) {
        throw "Invalid release config structure: $ConfigPath"
    }
    if ([string]::IsNullOrWhiteSpace([string]$config.Runtime.Major)) {
        throw "Runtime.Major missing in release config: $ConfigPath"
    }
    if ([string]::IsNullOrWhiteSpace([string]$config.Runtime.Architecture)) {
        throw "Runtime.Architecture missing in release config: $ConfigPath"
    }
    if ([string]::IsNullOrWhiteSpace([string]$config.Publish.Rid)) {
        throw "Publish.Rid missing in release config: $ConfigPath"
    }

    return $config
}

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
        [string]$RuntimeMajor,
        [Parameter(Mandatory = $true)]
        [string]$RuntimeArchitecture
    )

    if (-not (Test-Path -LiteralPath $PrereqDirectory)) {
        throw "Runtime prereq directory missing: $PrereqDirectory"
    }

    $installer = Get-ChildItem -LiteralPath $PrereqDirectory -Filter "*desktop-runtime*$RuntimeMajor*win-$RuntimeArchitecture*.exe" -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $installer) {
        throw "Runtime installer not found in $PrereqDirectory for major $RuntimeMajor (win-$RuntimeArchitecture)."
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

function Assert-FileContainsAllPatterns {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string[]]$Patterns
    )

    $content = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    foreach ($pattern in $Patterns) {
        if (-not $content.Contains($pattern)) {
            throw "Expected pattern '$pattern' missing in file: $Path"
        }
    }
}

function Assert-PrepareDistributionPolicy {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PrepareScriptPath
    )

    Assert-FileContainsAllPatterns -Path $PrepareScriptPath -Patterns @(
        "release-config.json"
        '--self-contained", "false'
        '--self-contained", "true'
        "-p:PublishSingleFile=false",
        "-p:PublishTrimmed=false",
        "Write-Sha256Sums",
        "release-notes.txt",
        "release-manifest.json",
        "AllowOverwriteVersion",
        "RunDefenderScan",
        "Resolve-ReleaseNotesSourceUrl"
    )
}

function Assert-ReleaseNotesTemplatePlaceholders {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TemplatePath
    )

    Assert-FileContainsAllPatterns -Path $TemplatePath -Patterns @(
        "__VERSION__",
        "__PACKAGE_KIND__",
        "__GENERATED_AT__",
        "__SOURCE_URL__"
    )
}

function Invoke-PublishCompatibilityProbe {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,
        [Parameter(Mandatory = $true)]
        [string]$PublishRid,
        [Parameter(Mandatory = $true)]
        [string]$RuntimeMajor
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
            "-r", $PublishRid,
            "--self-contained", "false",
            "-p:PublishSingleFile=false",
            "-p:PublishTrimmed=false",
            "-o", $fddDir
        )

        Invoke-DotnetOrThrow -Arguments @(
            "publish",
            $ProjectPath,
            "-c", "Release",
            "-r", $PublishRid,
            "--self-contained", "true",
            "-p:PublishSingleFile=false",
            "-p:PublishTrimmed=false",
            "-o", $scdDir
        )
    }
    finally {
        Pop-Location
    }

    $runtimeConfigPath = Join-Path $fddDir "sciman Classroom Toolkit.runtimeconfig.json"
    $windowsDesktopRuntimeVersion = Assert-WindowsDesktopRuntimeMajor -RuntimeConfigPath $runtimeConfigPath -ExpectedMajor $RuntimeMajor

    $fddExePath = Join-Path $fddDir "sciman Classroom Toolkit.exe"
    $fddPdfiumPath = Join-Path $fddDir "x64\pdfium.dll"
    $fddSqlitePath = Join-Path $fddDir "e_sqlite3.dll"
    Assert-PathExistsOrThrow -Path $fddExePath
    Assert-PathExistsOrThrow -Path $fddPdfiumPath
    Assert-PathExistsOrThrow -Path $fddSqlitePath

    $scdExePath = Join-Path $scdDir "sciman Classroom Toolkit.exe"
    $scdHostFxrPath = Join-Path $scdDir "hostfxr.dll"
    $scdCoreClrPath = Join-Path $scdDir "coreclr.dll"
    $scdVcruntimePath = Join-Path $scdDir "vcruntime140_cor3.dll"
    $scdSqlitePath = Join-Path $scdDir "e_sqlite3.dll"
    Assert-PathExistsOrThrow -Path $scdExePath
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
            FddExe = $fddExePath
            FddPdfium = $fddPdfiumPath
            FddSqlite = $fddSqlitePath
            ScdExe = $scdExePath
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
$releaseConfigPath = Join-Path $scriptRoot "release-config.json"
$releaseConfig = Get-ReleaseConfig -ConfigPath $releaseConfigPath
$runtimeMajor = [string]$releaseConfig.Runtime.Major
$runtimeArchitecture = [string]$releaseConfig.Runtime.Architecture
$publishRid = [string]$releaseConfig.Publish.Rid
$csprojPath = Join-Path $repoRoot "src\ClassroomToolkit.App\ClassroomToolkit.App.csproj"
$prepareScriptPath = Join-Path $scriptRoot "prepare-distribution.ps1"
$releaseNotesTemplatePath = Join-Path $scriptRoot "templates\release-notes.txt"

Assert-PathExists -Path $prepareScriptPath
Assert-PathExists -Path (Join-Path $scriptRoot "templates\start-standard.bat")
Assert-PathExists -Path (Join-Path $scriptRoot "templates\bootstrap-runtime.ps1")
Assert-PathExists -Path (Join-Path $scriptRoot "templates\user-guide-standard.md")
Assert-PathExists -Path (Join-Path $scriptRoot "templates\user-guide-offline.md")
Assert-PathExists -Path $releaseNotesTemplatePath
Assert-DesktopRuntimeInstallerExists -PrereqDirectory (Join-Path $scriptRoot "prereq") -RuntimeMajor $runtimeMajor -RuntimeArchitecture $runtimeArchitecture
Assert-PathExists -Path (Join-Path $repoRoot "docs\runbooks\release-prevention-checklist.md")
Assert-PrepareDistributionPolicy -PrepareScriptPath $prepareScriptPath
Assert-ReleaseNotesTemplatePlaceholders -TemplatePath $releaseNotesTemplatePath

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
if ($propertyGroup.Product -ne "sciman Classroom Toolkit") {
    throw "Unexpected Product in csproj. Current=$($propertyGroup.Product)"
}
if ($propertyGroup.AssemblyTitle -ne "sciman Classroom Toolkit") {
    throw "Unexpected AssemblyTitle in csproj. Current=$($propertyGroup.AssemblyTitle)"
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

$probeReport = Invoke-PublishCompatibilityProbe -RepoRoot $repoRoot -ProjectPath "src/ClassroomToolkit.App/ClassroomToolkit.App.csproj" -PublishRid $publishRid -RuntimeMajor $runtimeMajor
$presentationSignatureSummary = Get-PresentationSignatureMatrixSummary -RepoRoot $repoRoot
$probeReportPath = Join-Path $repoRoot "artifacts\release\preflight-compatibility-report.json"
([pscustomobject]@{
    GeneratedAtUtc = [DateTime]::UtcNow.ToString("o")
    PolicyChecks = [pscustomobject]@{
        PrepareDistributionScript = $prepareScriptPath
        ReleaseNotesTemplate = $releaseNotesTemplatePath
        ReleaseConfig = $releaseConfigPath
        PublishPolicy = "$publishRid only; PublishSingleFile=false; PublishTrimmed=false; FDD+SCD"
        VersionImmutability = "prepare-distribution requires -AllowOverwriteVersion to overwrite"
    }
    PublishProbe = $probeReport
    PresentationSignatureCoverage = $presentationSignatureSummary
}) | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $probeReportPath -Encoding UTF8

Write-Host "Release preflight check passed."
Write-Host "Compatibility report generated: $probeReportPath"
