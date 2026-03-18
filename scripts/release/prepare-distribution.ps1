param(
    [string]$Version = "",
    [string]$RuntimeInstallerPath = "",
    [switch]$EnsureLatestRuntime,
    [string]$RuntimeChannel = "10.0",
    [ValidateSet("both", "standard", "offline")]
    [string]$PackageMode = "both",
    [ValidateSet("zip", "7z")]
    [string]$ArchiveFormat = "zip",
    [switch]$SkipZip,
    [switch]$SkipPublish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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
        $relative = $item.FullName.Substring($TargetDirectory.Length).TrimStart('\')
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
        [string]$VersionValue,
        [Parameter(Mandatory = $true)]
        [string]$GeneratedAt
    )

    $content = Get-Content -LiteralPath $TemplatePath -Raw -Encoding UTF8
    $content = $content.Replace("__VERSION__", $VersionValue)
    $content = $content.Replace("__GENERATED_AT__", $GeneratedAt)
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

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot "..\..")).Path
$generatedAt = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Date).ToString("yyyy.MM.dd-HHmm")
}

$releaseRoot = Join-Path $repoRoot "artifacts\release\$Version"
$standardDir = Join-Path $releaseRoot "standard"
$offlineDir = Join-Path $releaseRoot "offline"
$templateDir = Join-Path $scriptRoot "templates"
$buildStandard = $PackageMode -eq "both" -or $PackageMode -eq "standard"
$buildOffline = $PackageMode -eq "both" -or $PackageMode -eq "offline"

if (Test-Path -LiteralPath $releaseRoot) {
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
                "-r", "win-x64",
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
                "-r", "win-x64",
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

$preReqDir = Join-Path $standardDir "prereq"
if ($buildStandard) {
    New-Item -Path $preReqDir -ItemType Directory -Force | Out-Null
}

$resolvedInstaller = ""
$sourcePrereqDir = Join-Path $scriptRoot "prereq"
if (-not [string]::IsNullOrWhiteSpace($RuntimeInstallerPath)) {
    $candidate = (Resolve-Path -LiteralPath $RuntimeInstallerPath -ErrorAction SilentlyContinue)
    if ($candidate -eq $null) {
        throw "Runtime installer path not found: $RuntimeInstallerPath"
    }
    $resolvedInstaller = $candidate.Path
}
elseif ($EnsureLatestRuntime) {
    $resolvedInstaller = Get-OrDownloadLatestRuntimeInstaller -Channel $RuntimeChannel -Architecture "x64" -PrereqDirectory $sourcePrereqDir
}
else {
    $localPreReq = $sourcePrereqDir
    if (Test-Path -LiteralPath $localPreReq) {
        $defaultInstaller = Get-ChildItem -LiteralPath $localPreReq -Filter "*desktop-runtime*10*win-x64*.exe" -File |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($defaultInstaller -ne $null) {
            $resolvedInstaller = $defaultInstaller.FullName
        }
    }
}

if ($buildStandard -and -not [string]::IsNullOrWhiteSpace($resolvedInstaller)) {
    $targetInstallerPath = Join-Path $preReqDir ([System.IO.Path]::GetFileName($resolvedInstaller))
    if (-not (Test-Path -LiteralPath $targetInstallerPath)) {
        Copy-Item -LiteralPath $resolvedInstaller -Destination $targetInstallerPath -Force
    }
}

if ($buildStandard) {
    Copy-Item -LiteralPath (Join-Path $templateDir "start-standard.bat") -Destination (Join-Path $standardDir "启动.bat") -Force
    Copy-Item -LiteralPath (Join-Path $templateDir "bootstrap-runtime.ps1") -Destination (Join-Path $standardDir "bootstrap-runtime.ps1") -Force
}

if ($buildStandard) {
    Render-Template -TemplatePath (Join-Path $templateDir "user-guide-standard.md") -OutputPath (Join-Path $standardDir "使用说明.md") -VersionValue $Version -GeneratedAt $generatedAt
}
if ($buildOffline) {
    Render-Template -TemplatePath (Join-Path $templateDir "user-guide-offline.md") -OutputPath (Join-Path $offlineDir "使用说明.md") -VersionValue $Version -GeneratedAt $generatedAt
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
        $standardBase = Join-Path $releaseRoot "ClassroomToolkit-$Version-standard"
        $standardArchive = Compress-Directory -SourceDirectory $standardDir -DestinationBasePath $standardBase -Format $ArchiveFormat
        $archives.Add($standardArchive) | Out-Null
    }
    if ($buildOffline) {
        $offlineBase = Join-Path $releaseRoot "ClassroomToolkit-$Version-offline"
        $offlineArchive = Compress-Directory -SourceDirectory $offlineDir -DestinationBasePath $offlineBase -Format $ArchiveFormat
        $archives.Add($offlineArchive) | Out-Null
    }
}

Write-Host "Release preparation completed."
Write-Host "Version: $Version"
Write-Host "Package mode: $PackageMode"
if ($buildStandard) {
    Write-Host "Standard package: $standardDir"
}
if ($buildOffline) {
    Write-Host "Offline package: $offlineDir"
}
if ($buildStandard) {
    if ([string]::IsNullOrWhiteSpace($resolvedInstaller)) {
        Write-Host "Runtime installer: not bundled (drop installer into scripts/release/prereq or pass -RuntimeInstallerPath)."
    }
    else {
        Write-Host "Runtime installer bundled: $resolvedInstaller"
    }
}
if (-not $SkipZip -and $archives.Count -gt 0) {
    foreach ($archive in $archives) {
        Write-Host "Archive: $archive"
    }
}
