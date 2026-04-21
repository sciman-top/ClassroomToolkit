[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [ValidateSet("all", "standard", "offline")]
    [string]$PackageMode = "all",
    [string]$Configuration = "",
    [string]$RuntimeIdentifier = "",
    [string]$ProjectPath = "",
    [string]$OutputRoot = "artifacts/release",
    [string]$ConfigPath = "scripts/release/release-config.json",
    [switch]$SkipPublish,
    [switch]$SkipZip,
    [switch]$AllowOverwriteVersion,
    [switch]$EnsureLatestRuntime
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

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )

    Write-Host "[release-package] START $Name"
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "[release-package] FAIL  $Name (exit=$LASTEXITCODE)"
    }
    Write-Host "[release-package] PASS  $Name"
}

function Test-GitLfsPointer {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    try {
        $firstLine = Get-Content -LiteralPath $Path -TotalCount 1 -ErrorAction Stop
        return $firstLine -like "version https://git-lfs.github.com/spec/v1*"
    }
    catch {
        return $false
    }
}

function Ensure-RuntimeInstaller {
    param(
        [Parameter(Mandatory = $true)][string]$InstallerPath,
        [Parameter(Mandatory = $true)][string]$DownloadUrl,
        [string]$DownloadTargetPath = "",
        [switch]$AllowDownload
    )

    $hasBinary = (Test-Path -LiteralPath $InstallerPath) -and -not (Test-GitLfsPointer -Path $InstallerPath)
    if ($hasBinary) {
        return $InstallerPath
    }

    if (-not $AllowDownload) {
        throw "Runtime installer missing or still a Git LFS pointer: $InstallerPath. Run 'git lfs pull' or pass -EnsureLatestRuntime."
    }

    $targetPath = if ([string]::IsNullOrWhiteSpace($DownloadTargetPath)) {
        $InstallerPath
    }
    else {
        $DownloadTargetPath
    }

    $installerDir = Split-Path -Parent $targetPath
    New-Item -ItemType Directory -Path $installerDir -Force | Out-Null
    Invoke-Step -Name "download-runtime-installer" -Action {
        Invoke-WebRequest -Uri $DownloadUrl -OutFile $targetPath
    }

    return $targetPath
}

function Assert-FileExists {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing required file: $Label -> $Path"
    }
}

function Assert-FileExistsByName {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $hit = Get-ChildItem -LiteralPath $Root -Recurse -File -Filter $Name -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $hit) {
        throw "Missing required dependency '$Name' under $Root"
    }
}

function Write-FileSha256 {
    param([Parameter(Mandatory = $true)][string]$Root)

    $sumPath = Join-Path $Root "SHA256SUMS.txt"
    if (Test-Path -LiteralPath $sumPath) {
        Remove-Item -LiteralPath $sumPath -Force
    }

    $lines = New-Object System.Collections.Generic.List[string]
    $files = Get-ChildItem -LiteralPath $Root -Recurse -File | Sort-Object FullName
    foreach ($file in $files) {
        if ($file.FullName -eq $sumPath) {
            continue
        }

        $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName
        $relative = $file.FullName.Substring($Root.Length).TrimStart('\').Replace('\', '/')
        $lines.Add(("{0} *{1}" -f $hash.Hash.ToLowerInvariant(), $relative)) | Out-Null
    }

    Set-Content -LiteralPath $sumPath -Value $lines -Encoding UTF8
}

function Write-StandardBootstrap {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$AppExeName,
        [Parameter(Mandatory = $true)][string]$RuntimeMajor,
        [Parameter(Mandatory = $true)][string]$RuntimeInstallerFileName
    )

    $script = @"
param()

`$ErrorActionPreference = 'Stop'
`$root = Split-Path -Parent `$MyInvocation.MyCommand.Path
`$appExe = Join-Path `$root 'app\$AppExeName'
`$installer = Join-Path `$root 'prereq\$RuntimeInstallerFileName'
`$requiredPrefix = 'Microsoft.WindowsDesktop.App $RuntimeMajor.'

if (-not (Test-Path -LiteralPath `$appExe)) {
    Write-Host "找不到应用程序: `$appExe" -ForegroundColor Red
    exit 2
}

`$hasRuntime = `$false
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    `$runtimeList = & dotnet --list-runtimes 2>`$null
    foreach (`$line in `$runtimeList) {
        if (`$line -like "`$requiredPrefix*") {
            `$hasRuntime = `$true
            break
        }
    }
}

if (-not `$hasRuntime) {
    Write-Host "检测到未安装 .NET Desktop Runtime $RuntimeMajor.x (x64)。" -ForegroundColor Yellow
    if (Test-Path -LiteralPath `$installer) {
        Write-Host "将打开运行时安装包，请完成安装后再重新启动本工具。" -ForegroundColor Yellow
        Start-Process -FilePath `$installer
    }
    else {
        Write-Host "未找到离线安装包: `$installer" -ForegroundColor Red
    }
    exit 3
}

Start-Process -FilePath `$appExe
"@

    $bat = @"
@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0bootstrap-runtime.ps1"
exit /b %errorlevel%
"@

    Set-Content -LiteralPath (Join-Path $Root "bootstrap-runtime.ps1") -Value $script -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $Root "启动.bat") -Value $bat -Encoding ASCII
}

function Write-OfflineLauncher {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$AppExeName
    )

    $bat = @"
@echo off
setlocal
start "" "%~dp0app\$AppExeName"
"@

    Set-Content -LiteralPath (Join-Path $Root "启动.bat") -Value $bat -Encoding ASCII
}

function Invoke-Publish {
    param(
        [Parameter(Mandatory = $true)][string]$Project,
        [Parameter(Mandatory = $true)][string]$ConfigurationValue,
        [Parameter(Mandatory = $true)][string]$Rid,
        [Parameter(Mandatory = $true)][bool]$SelfContained,
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [switch]$ReadyToRun
    )

    $publishArgs = @(
        "publish",
        $Project,
        "-c",
        $ConfigurationValue,
        "-r",
        $Rid,
        "--self-contained",
        $(if ($SelfContained) { "true" } else { "false" }),
        "-p:UseAppHost=true",
        "-p:PublishSingleFile=false",
        "-p:PublishTrimmed=false",
        "-p:DebugType=None",
        "-p:DebugSymbols=false",
        "-p:ContinuousIntegrationBuild=true",
        "-o",
        $OutputPath
    )

    if ($ReadyToRun) {
        $publishArgs += "-p:PublishReadyToRun=true"
    }

    Invoke-Step -Name ("dotnet-publish-{0}" -f $(if ($SelfContained) { "offline" } else { "standard" })) -Action {
        dotnet @publishArgs
    }
}

$resolvedConfigPath = Resolve-AbsolutePath -Path $ConfigPath
Assert-FileExists -Path $resolvedConfigPath -Label "release-config.json"

$configRoot = Split-Path -Parent $resolvedConfigPath
$config = Get-Content -LiteralPath $resolvedConfigPath -Raw | ConvertFrom-Json
if ($null -eq $config.release) {
    throw "Invalid release config format: missing 'release' root."
}

$releaseConfig = $config.release
$resolvedProjectPath = Resolve-AbsolutePath -Path $(if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $releaseConfig.projectPath } else { $ProjectPath })
$resolvedConfiguration = if ([string]::IsNullOrWhiteSpace($Configuration)) { [string]$releaseConfig.configuration } else { $Configuration }
$resolvedRid = if ([string]::IsNullOrWhiteSpace($RuntimeIdentifier)) { [string]$releaseConfig.runtimeIdentifier } else { $RuntimeIdentifier }
$appExeName = [string]$releaseConfig.appExecutableName
$runtimeInstallerFileName = [string]$releaseConfig.runtimeInstaller.fileName
$runtimeRequiredMajor = [string]$releaseConfig.runtimeInstaller.requiredMajor
$runtimeDownloadUrl = [string]$releaseConfig.runtimeInstaller.downloadUrl

Assert-FileExists -Path $resolvedProjectPath -Label "project"

$releaseRoot = Join-Path (Resolve-AbsolutePath -Path $OutputRoot) $Version
if (Test-Path -LiteralPath $releaseRoot) {
    if (-not $AllowOverwriteVersion) {
        throw "Release directory already exists: $releaseRoot. Pass -AllowOverwriteVersion to overwrite."
    }

    Remove-Item -LiteralPath $releaseRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

$standardRoot = Join-Path $releaseRoot "standard"
$offlineRoot = Join-Path $releaseRoot "offline"
$standardApp = Join-Path $standardRoot "app"
$offlineApp = Join-Path $offlineRoot "app"
$runtimeInstallerPath = Join-Path (Join-Path $configRoot "prereq") $runtimeInstallerFileName
$runtimeInstallerCachePath = Join-Path (Join-Path $releaseRoot "_runtime-cache") $runtimeInstallerFileName

$buildStandard = $PackageMode -eq "all" -or $PackageMode -eq "standard"
$buildOffline = $PackageMode -eq "all" -or $PackageMode -eq "offline"

if ($buildStandard) {
    New-Item -ItemType Directory -Path $standardApp -Force | Out-Null
    if (-not $SkipPublish) {
        Invoke-Publish -Project $resolvedProjectPath -ConfigurationValue $resolvedConfiguration -Rid $resolvedRid -SelfContained:$false -OutputPath $standardApp
    }

    Assert-FileExists -Path $standardApp -Label "standard app output folder"
    $resolvedRuntimeInstallerPath = Ensure-RuntimeInstaller `
        -InstallerPath $runtimeInstallerPath `
        -DownloadUrl $runtimeDownloadUrl `
        -DownloadTargetPath $runtimeInstallerCachePath `
        -AllowDownload:$EnsureLatestRuntime

    $prereqDir = Join-Path $standardRoot "prereq"
    New-Item -ItemType Directory -Path $prereqDir -Force | Out-Null
    Copy-Item -LiteralPath $resolvedRuntimeInstallerPath -Destination (Join-Path $prereqDir $runtimeInstallerFileName) -Force

    Write-StandardBootstrap -Root $standardRoot -AppExeName $appExeName -RuntimeMajor $runtimeRequiredMajor -RuntimeInstallerFileName $runtimeInstallerFileName
    Assert-FileExistsByName -Root $standardApp -Name "*.runtimeconfig.json"
    Assert-FileExistsByName -Root $standardApp -Name "pdfium.dll"
    Assert-FileExistsByName -Root $standardApp -Name "e_sqlite3.dll"
    Write-FileSha256 -Root $standardRoot

    if (-not $SkipZip) {
        $zipPath = Join-Path $releaseRoot ("ClassroomToolkit-{0}-standard.zip" -f $Version)
        if (Test-Path -LiteralPath $zipPath) {
            Remove-Item -LiteralPath $zipPath -Force
        }
        Invoke-Step -Name "zip-standard" -Action {
            Compress-Archive -Path (Join-Path $standardRoot "*") -DestinationPath $zipPath
        }
    }
}

if ($buildOffline) {
    New-Item -ItemType Directory -Path $offlineApp -Force | Out-Null
    if (-not $SkipPublish) {
        Invoke-Publish -Project $resolvedProjectPath -ConfigurationValue $resolvedConfiguration -Rid $resolvedRid -SelfContained:$true -OutputPath $offlineApp -ReadyToRun
    }

    Assert-FileExists -Path $offlineApp -Label "offline app output folder"
    Write-OfflineLauncher -Root $offlineRoot -AppExeName $appExeName
    Assert-FileExists -Path (Join-Path $offlineApp "hostfxr.dll") -Label "hostfxr.dll"
    Assert-FileExists -Path (Join-Path $offlineApp "coreclr.dll") -Label "coreclr.dll"
    Assert-FileExists -Path (Join-Path $offlineApp "vcruntime140_cor3.dll") -Label "vcruntime140_cor3.dll"
    Assert-FileExistsByName -Root $offlineApp -Name "e_sqlite3.dll"
    Write-FileSha256 -Root $offlineRoot

    if (-not $SkipZip) {
        $zipPath = Join-Path $releaseRoot ("ClassroomToolkit-{0}-offline.zip" -f $Version)
        if (Test-Path -LiteralPath $zipPath) {
            Remove-Item -LiteralPath $zipPath -Force
        }
        Invoke-Step -Name "zip-offline" -Action {
            Compress-Archive -Path (Join-Path $offlineRoot "*") -DestinationPath $zipPath
        }
    }
}

$manifest = [ordered]@{
    version = $Version
    generated_at_utc = [DateTimeOffset]::UtcNow.ToString("o")
    package_mode = $PackageMode
    configuration = $resolvedConfiguration
    runtime_identifier = $resolvedRid
    app_executable = $appExeName
    skip_publish = [bool]$SkipPublish
    skip_zip = [bool]$SkipZip
    ensure_latest_runtime = [bool]$EnsureLatestRuntime
    outputs = [ordered]@{
        root = $releaseRoot
        standard = if ($buildStandard) { $standardRoot } else { $null }
        offline = if ($buildOffline) { $offlineRoot } else { $null }
    }
}

$manifestPath = Join-Path $releaseRoot "release-manifest.json"
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Write-Host "[release-package] manifest: $manifestPath"
Write-Host "[release-package] DONE"
