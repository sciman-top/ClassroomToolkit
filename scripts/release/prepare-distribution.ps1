param(
    [string]$Version = "",
    [string]$RuntimeInstallerPath = "",
    [switch]$EnsureLatestRuntime,
    [string]$RuntimeChannel = "",
    [string]$ReleaseNotesSourceUrl = "",
    [ValidateSet("both", "standard", "offline")]
    [string]$PackageMode = "both",
    [ValidateSet("zip", "7z")]
    [string]$ArchiveFormat = "zip",
    [switch]$SkipZip,
    [switch]$SkipPublish,
    [switch]$RunDefenderScan,
    [switch]$FailOnDefenderScanError,
    [switch]$AllowOverwriteVersion
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
    if ([string]::IsNullOrWhiteSpace([string]$config.Runtime.Channel)) {
        throw "Runtime.Channel missing in release config: $ConfigPath"
    }
    if ([string]::IsNullOrWhiteSpace([string]$config.Runtime.Architecture)) {
        throw "Runtime.Architecture missing in release config: $ConfigPath"
    }
    if ([string]::IsNullOrWhiteSpace([string]$config.Publish.Rid)) {
        throw "Publish.Rid missing in release config: $ConfigPath"
    }

    return $config
}

function Invoke-DotnetOrThrow {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Write-Sha256Sums {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TargetDirectory
    )

    $outputPath = Join-Path $TargetDirectory "SHA256SUMS.txt"
    $items = Get-ChildItem -Path $TargetDirectory -Recurse -File |
        Where-Object { $_.FullName -ne $outputPath } |
        Sort-Object FullName

    $lines = foreach ($item in $items) {
        $hash = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $relative = $item.FullName.Substring($TargetDirectory.Length).TrimStart('\\')
        "$hash  $relative"
    }

    Set-Content -Path $outputPath -Value $lines -Encoding UTF8
}

function Render-Template {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TemplatePath,
        [Parameter(Mandatory = $true)]
        [string]$OutputPath,
        [Parameter(Mandatory = $true)]
        [hashtable]$Tokens
    )

    $content = Get-Content -LiteralPath $TemplatePath -Raw -Encoding UTF8
    foreach ($key in $Tokens.Keys) {
        $content = $content.Replace("__$key" + "__", [string]$Tokens[$key])
    }

    Set-Content -LiteralPath $OutputPath -Value $content -Encoding UTF8
}

function Resolve-LatestRuntimeInstaller {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Channel,
        [Parameter(Mandatory = $true)]
        [string]$Architecture
    )

    $akaUrl = "https://aka.ms/dotnet/$Channel/windowsdesktop-runtime-win-$Architecture.exe"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

    try {
        $response = Invoke-WebRequest -Uri $akaUrl -Method Head -MaximumRedirection 10 -UseBasicParsing
    }
    catch {
        throw "Failed to resolve latest runtime from '$akaUrl'. $_"
    }

    $finalUri = $response.BaseResponse.ResponseUri
    if ($null -eq $finalUri) {
        throw "Unable to resolve runtime redirect URI from '$akaUrl'."
    }

    $fileName = [System.IO.Path]::GetFileName($finalUri.AbsolutePath)
    if ([string]::IsNullOrWhiteSpace($fileName)) {
        throw "Unable to parse installer file name from '$($finalUri.AbsoluteUri)'."
    }

    return @{
        Url = $finalUri.AbsoluteUri
        FileName = $fileName
    }
}

function Get-OrDownloadLatestRuntimeInstaller {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Channel,
        [Parameter(Mandatory = $true)]
        [string]$Architecture,
        [Parameter(Mandatory = $true)]
        [string]$PrereqDirectory
    )

    New-Item -Path $PrereqDirectory -ItemType Directory -Force | Out-Null

    $latest = Resolve-LatestRuntimeInstaller -Channel $Channel -Architecture $Architecture
    $existing = Get-ChildItem -LiteralPath $PrereqDirectory -File |
        Where-Object { $_.Name -ieq $latest.FileName } |
        Select-Object -First 1

    if ($existing -ne $null) {
        return $existing.FullName
    }

    $targetPath = Join-Path $PrereqDirectory $latest.FileName
    Write-Host "Downloading latest runtime installer: $($latest.Url)"
    Invoke-WebRequest -Uri $latest.Url -OutFile $targetPath -UseBasicParsing
    return $targetPath
}

function Compress-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,
        [Parameter(Mandatory = $true)]
        [string]$DestinationBasePath,
        [Parameter(Mandatory = $true)]
        [string]$Format
    )

    if ($Format -eq "7z") {
        $sevenZip = Get-Command 7z -ErrorAction SilentlyContinue
        if ($sevenZip -eq $null) {
            $sevenZip = Get-Command 7za -ErrorAction SilentlyContinue
        }

        if ($sevenZip -ne $null) {
            $outputPath = "$DestinationBasePath.7z"
            if (Test-Path -LiteralPath $outputPath) {
                Remove-Item -LiteralPath $outputPath -Force
            }

            & $sevenZip.Source a -t7z -mx=9 -y $outputPath (Join-Path $SourceDirectory "*") | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "7z archive creation failed for $SourceDirectory."
            }

            return $outputPath
        }

        Write-Warning "7z command not found. Falling back to zip."
    }

    $zipPath = "$DestinationBasePath.zip"
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $SourceDirectory "*") -DestinationPath $zipPath -Force
    return $zipPath
}

function Get-GitCommit {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    Push-Location $RepositoryRoot
    try {
        $commit = (& git rev-parse --short HEAD 2>$null)
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) {
            return "unknown"
        }

        return $commit.Trim()
    }
    finally {
        Pop-Location
    }
}

function Get-GitRemoteOriginUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    Push-Location $RepositoryRoot
    try {
        $remoteUrl = (& git remote get-url origin 2>$null)
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($remoteUrl)) {
            return ""
        }

        return $remoteUrl.Trim()
    }
    finally {
        Pop-Location
    }
}

function Convert-ToGithubHttpsRemoteUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RemoteUrl
    )

    if ($RemoteUrl -match "^https://github\.com/[^/\s]+/[^/\s]+(\.git)?/?$") {
        return $RemoteUrl -replace "\.git/?$", ""
    }
    if ($RemoteUrl -match "^git@github\.com:([^/\s]+)/([^/\s]+?)(\.git)?$") {
        return "https://github.com/$($Matches[1])/$($Matches[2])"
    }
    if ($RemoteUrl -match "^ssh://git@github\.com/([^/\s]+)/([^/\s]+?)(\.git)?/?$") {
        return "https://github.com/$($Matches[1])/$($Matches[2])"
    }

    return ""
}

function Resolve-ReleaseNotesSourceUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,
        [AllowEmptyString()]
        [string]$ExplicitSourceUrl,
        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitSourceUrl)) {
        return $ExplicitSourceUrl.Trim()
    }

    $originRemote = Get-GitRemoteOriginUrl -RepositoryRoot $RepositoryRoot
    $githubRemote = Convert-ToGithubHttpsRemoteUrl -RemoteUrl $originRemote
    if (-not [string]::IsNullOrWhiteSpace($githubRemote)) {
        return "$githubRemote/releases/tag/v$Version"
    }

    throw "Unable to infer release notes source URL from git remote '$originRemote'. Pass -ReleaseNotesSourceUrl explicitly."
}

function Get-UnicodeText {
    param(
        [Parameter(Mandatory = $true)]
        [int[]]$CodePoints
    )

    return -join ($CodePoints | ForEach-Object { [char]$_ })
}

function Write-ReleaseManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ManifestPath,
        [Parameter(Mandatory = $true)]
        [string]$Version,
        [Parameter(Mandatory = $true)]
        [string]$GeneratedAtUtc,
        [Parameter(Mandatory = $true)]
        [string]$GitCommit,
        [Parameter(Mandatory = $true)]
        [string]$PackageMode,
        [Parameter(Mandatory = $true)]
        [bool]$BuildStandard,
        [Parameter(Mandatory = $true)]
        [bool]$BuildOffline,
        [AllowEmptyCollection()]
        [string[]]$Archives,
        [Parameter(Mandatory = $true)]
        [bool]$DefenderScanEnabled,
        [Parameter(Mandatory = $true)]
        [string]$DefenderScanStatus
    )

    $manifest = [pscustomobject]@{
        version = $Version
        generatedAtUtc = $GeneratedAtUtc
        gitCommit = $GitCommit
        packageMode = $PackageMode
        packages = [pscustomobject]@{
            standard = $BuildStandard
            offline = $BuildOffline
        }
        archives = $Archives
        defenderScan = [pscustomobject]@{
            enabled = $DefenderScanEnabled
            status = $DefenderScanStatus
        }
    }

    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ManifestPath -Encoding UTF8
}

function Invoke-DefenderScan {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScanPath,
        [switch]$FailOnError
    )

    if (-not (Get-Command Start-MpScan -ErrorAction SilentlyContinue)) {
        $message = "Start-MpScan not found. Skip Defender scan."
        if ($FailOnError) {
            throw $message
        }

        Write-Warning $message
        return "skipped-command-missing"
    }

    try {
        Start-MpScan -ScanType CustomScan -ScanPath $ScanPath | Out-Null
        return "completed"
    }
    catch {
        if ($FailOnError) {
            throw "Defender scan failed. $_"
        }

        Write-Warning "Defender scan failed and was ignored: $_"
        return "failed-ignored"
    }
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot "..\..")).Path
$releaseConfigPath = Join-Path $scriptRoot "release-config.json"
$releaseConfig = Get-ReleaseConfig -ConfigPath $releaseConfigPath
$runtimeMajor = [string]$releaseConfig.Runtime.Major
$runtimeArchitecture = [string]$releaseConfig.Runtime.Architecture
$publishRid = [string]$releaseConfig.Publish.Rid
$resolvedRuntimeChannel = if ([string]::IsNullOrWhiteSpace($RuntimeChannel)) { [string]$releaseConfig.Runtime.Channel } else { $RuntimeChannel }
$generatedAt = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Date).ToString("yyyy.MM.dd-HHmm")
}

$releaseRoot = Join-Path $repoRoot "artifacts\release\$Version"
$standardDir = Join-Path $releaseRoot "standard"
$offlineDir = Join-Path $releaseRoot "offline"
$templateDir = Join-Path $scriptRoot "templates"
$launcherFileName = Get-UnicodeText -CodePoints 0x542F,0x52A8,0x002E,0x0062,0x0061,0x0074
$userGuideFileName = Get-UnicodeText -CodePoints 0x4F7F,0x7528,0x8BF4,0x660E,0x002E,0x006D,0x0064
$releaseNotesFileName = Get-UnicodeText -CodePoints 0x53D1,0x5E03,0x8BF4,0x660E,0x002E,0x0074,0x0078,0x0074
$buildStandard = $PackageMode -eq "both" -or $PackageMode -eq "standard"
$buildOffline = $PackageMode -eq "both" -or $PackageMode -eq "offline"

if (Test-Path -LiteralPath $releaseRoot) {
    if (-not $AllowOverwriteVersion) {
        throw "Release directory already exists for version '$Version': $releaseRoot. Refuse overwrite by default. Use -AllowOverwriteVersion to override."
    }

    Remove-Item -LiteralPath $releaseRoot -Recurse -Force
}

if ($buildStandard) {
    New-Item -Path $standardDir -ItemType Directory -Force | Out-Null
}
if ($buildOffline) {
    New-Item -Path $offlineDir -ItemType Directory -Force | Out-Null
}

if (-not $SkipPublish) {
    Push-Location $repoRoot
    try {
        if ($buildStandard) {
            Invoke-DotnetOrThrow -Arguments @(
                "publish",
                "src/ClassroomToolkit.App/ClassroomToolkit.App.csproj",
                "-c", "Release",
                "-r", $publishRid,
                "--self-contained", "false",
                "-p:PublishSingleFile=false",
                "-p:PublishTrimmed=false",
                "-o", $standardDir
            )
        }

        if ($buildOffline) {
            Invoke-DotnetOrThrow -Arguments @(
                "publish",
                "src/ClassroomToolkit.App/ClassroomToolkit.App.csproj",
                "-c", "Release",
                "-r", $publishRid,
                "--self-contained", "true",
                "-p:PublishSingleFile=false",
                "-p:PublishTrimmed=false",
                "-o", $offlineDir
            )
        }
    }
    finally {
        Pop-Location
    }
}

$offlinePreReqDir = Join-Path $offlineDir "prereq"
if ($buildOffline) {
    New-Item -Path $offlinePreReqDir -ItemType Directory -Force | Out-Null
}

$resolvedInstaller = ""
$sourcePrereqDir = Join-Path $scriptRoot "prereq"
if ($buildOffline) {
    if (-not [string]::IsNullOrWhiteSpace($RuntimeInstallerPath)) {
        $candidate = (Resolve-Path -LiteralPath $RuntimeInstallerPath -ErrorAction SilentlyContinue)
        if ($candidate -eq $null) {
            throw "Runtime installer path not found: $RuntimeInstallerPath"
        }

        $resolvedInstaller = $candidate.Path
    }
    elseif ($EnsureLatestRuntime) {
        $resolvedInstaller = Get-OrDownloadLatestRuntimeInstaller -Channel $resolvedRuntimeChannel -Architecture $runtimeArchitecture -PrereqDirectory $sourcePrereqDir
    }
    else {
        $localPreReq = $sourcePrereqDir
        if (Test-Path -LiteralPath $localPreReq) {
            $defaultInstaller = Get-ChildItem -LiteralPath $localPreReq -Filter "*desktop-runtime*$runtimeMajor*win-$runtimeArchitecture*.exe" -File |
                Sort-Object LastWriteTime -Descending |
                Select-Object -First 1
            if ($defaultInstaller -ne $null) {
                $resolvedInstaller = $defaultInstaller.FullName
            }
        }
    }
}

if ($buildOffline -and -not [string]::IsNullOrWhiteSpace($resolvedInstaller)) {
    $targetInstallerPath = Join-Path $offlinePreReqDir ([System.IO.Path]::GetFileName($resolvedInstaller))
    if (-not (Test-Path -LiteralPath $targetInstallerPath)) {
        Copy-Item -LiteralPath $resolvedInstaller -Destination $targetInstallerPath -Force
    }
}

if ($buildStandard) {
    Copy-Item -LiteralPath (Join-Path $templateDir "start-standard.bat") -Destination (Join-Path $standardDir $launcherFileName) -Force
    Copy-Item -LiteralPath (Join-Path $templateDir "bootstrap-runtime.ps1") -Destination (Join-Path $standardDir "bootstrap-runtime.ps1") -Force
}

if ($buildStandard) {
    Render-Template -TemplatePath (Join-Path $templateDir "user-guide-standard.md") -OutputPath (Join-Path $standardDir $userGuideFileName) -Tokens @{
        VERSION = $Version
        GENERATED_AT = $generatedAt
    }
}
if ($buildOffline) {
    Render-Template -TemplatePath (Join-Path $templateDir "user-guide-offline.md") -OutputPath (Join-Path $offlineDir $userGuideFileName) -Tokens @{
        VERSION = $Version
        GENERATED_AT = $generatedAt
    }
}

$resolvedSourceUrl = Resolve-ReleaseNotesSourceUrl -RepositoryRoot $repoRoot -ExplicitSourceUrl $ReleaseNotesSourceUrl -Version $Version
if ($buildStandard) {
    Render-Template -TemplatePath (Join-Path $templateDir "release-notes.txt") -OutputPath (Join-Path $standardDir $releaseNotesFileName) -Tokens @{
        VERSION = $Version
        GENERATED_AT = $generatedAt
        PACKAGE_KIND = "standard"
        SOURCE_URL = $resolvedSourceUrl
    }
}
if ($buildOffline) {
    Render-Template -TemplatePath (Join-Path $templateDir "release-notes.txt") -OutputPath (Join-Path $offlineDir $releaseNotesFileName) -Tokens @{
        VERSION = $Version
        GENERATED_AT = $generatedAt
        PACKAGE_KIND = "offline"
        SOURCE_URL = $resolvedSourceUrl
    }
}

if ($buildStandard) {
    Write-Sha256Sums -TargetDirectory $standardDir
}
if ($buildOffline) {
    Write-Sha256Sums -TargetDirectory $offlineDir
}

$archives = New-Object System.Collections.Generic.List[string]
if (-not $SkipZip) {
    if ($buildStandard) {
        $standardBase = Join-Path $releaseRoot "sciman-Classroom-Toolkit-$Version-standard"
        $standardArchive = Compress-Directory -SourceDirectory $standardDir -DestinationBasePath $standardBase -Format $ArchiveFormat
        $archives.Add($standardArchive) | Out-Null
    }
    if ($buildOffline) {
        $offlineBase = Join-Path $releaseRoot "sciman-Classroom-Toolkit-$Version-offline"
        $offlineArchive = Compress-Directory -SourceDirectory $offlineDir -DestinationBasePath $offlineBase -Format $ArchiveFormat
        $archives.Add($offlineArchive) | Out-Null
    }
}

$defenderScanStatus = "not-enabled"
if ($RunDefenderScan) {
    $defenderScanStatus = Invoke-DefenderScan -ScanPath $releaseRoot -FailOnError:$FailOnDefenderScanError
}

$gitCommit = Get-GitCommit -RepositoryRoot $repoRoot
$manifestPath = Join-Path $releaseRoot "release-manifest.json"
Write-ReleaseManifest `
    -ManifestPath $manifestPath `
    -Version $Version `
    -GeneratedAtUtc ([DateTime]::UtcNow.ToString("o")) `
    -GitCommit $gitCommit `
    -PackageMode $PackageMode `
    -BuildStandard $buildStandard `
    -BuildOffline $buildOffline `
    -Archives ($archives.ToArray()) `
    -DefenderScanEnabled ([bool]$RunDefenderScan) `
    -DefenderScanStatus $defenderScanStatus

Write-Host "Release preparation completed."
Write-Host "Version: $Version"
Write-Host "Package mode: $PackageMode"
Write-Host "Manifest: $manifestPath"
Write-Host "Release notes source URL: $resolvedSourceUrl"
if ($buildStandard) {
    Write-Host "Standard package: $standardDir"
}
if ($buildOffline) {
    Write-Host "Offline package: $offlineDir"
}
if ($buildOffline) {
    if ([string]::IsNullOrWhiteSpace($resolvedInstaller)) {
        Write-Host "Runtime installer: not bundled into offline package (drop installer into scripts/release/prereq or pass -RuntimeInstallerPath)."
    }
    else {
        Write-Host "Runtime installer bundled into offline package: $resolvedInstaller"
    }
}
if (-not $SkipZip -and $archives.Count -gt 0) {
    foreach ($archive in $archives) {
        Write-Host "Archive: $archive"
    }
}
if ($RunDefenderScan) {
    Write-Host "Defender scan status: $defenderScanStatus"
}
