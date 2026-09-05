[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$OutputRoot = "",
    [string]$AppExecutableName = "sciman Classroom Toolkit.exe",
    [string]$RepositoryUrl = "https://github.com/sciman-top/ClassroomToolkit",
    [string]$SourceRef = "HEAD",
    [string]$ResolvedSourceCommit = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$artifactLayoutModule = Join-Path $PSScriptRoot "..\artifacts\ArtifactLayout.psm1"
Import-Module -Name $artifactLayoutModule -Force
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Get-ClassroomToolkitArtifactPath -Name ReleaseRoot
}

function Resolve-AbsolutePath {
    param([Parameter(Mandatory = $true)][string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path (Get-Location) $Path
}

function Assert-SafeReleaseVersionSegment {
    param([Parameter(Mandatory = $true)][string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value.Trim() -ne $Value -or $Value -in @('.', '..')) {
        throw "Invalid release version '$Value'."
    }
    if ($Value.Contains([System.IO.Path]::DirectorySeparatorChar) -or
        $Value.Contains([System.IO.Path]::AltDirectorySeparatorChar) -or
        $Value.IndexOfAny([System.IO.Path]::GetInvalidFileNameChars()) -ge 0) {
        throw "Invalid release version '$Value': version must be a single safe directory name."
    }
}

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )
    Write-Host "[portable-package] START $Name"
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "[portable-package] FAIL $Name (exit=$LASTEXITCODE)" }
    Write-Host "[portable-package] PASS  $Name"
}

Assert-SafeReleaseVersionSegment -Value $Version
$repositoryUri = $null
if (-not [Uri]::TryCreate($RepositoryUrl, [UriKind]::Absolute, [ref]$repositoryUri)) {
    throw "Invalid repository URL '$RepositoryUrl'."
}
if ($repositoryUri.Scheme -ne [Uri]::UriSchemeHttps) {
    throw "Repository URL must use HTTPS."
}

$releaseRoot = Join-Path (Resolve-AbsolutePath -Path $OutputRoot) $Version
$offlineRoot = Join-Path $releaseRoot "offline"
$offlineApp = Join-Path $offlineRoot "app"
$portableRoot = Join-Path $releaseRoot "_portable-build"
$portableApp = Join-Path $portableRoot "app"
$portableData = Join-Path $portableRoot "data"
$portableDeliveryRoot = Join-Path $releaseRoot "portable"
$portableZip = Join-Path $portableDeliveryRoot ("ClassroomToolkit-{0}-portable.zip" -f $Version)

if (-not (Test-Path -LiteralPath $offlineApp)) {
    throw "Offline publish output is required for the portable package: $offlineApp"
}
if (Test-Path -LiteralPath $portableRoot) {
    Remove-Item -LiteralPath $portableRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $portableApp, $portableData -Force | Out-Null
foreach ($item in Get-ChildItem -LiteralPath $offlineApp -Force) {
    Copy-Item -LiteralPath $item.FullName -Destination $portableApp -Recurse -Force
}

Set-Content -LiteralPath (Join-Path $portableRoot "portable.mode") -Value "mode=portable" -Encoding ASCII
$launcher = "@echo off`r`nsetlocal`r`nstart `"`" `"%~dp0app\$AppExecutableName`"`r`n"
Set-Content -LiteralPath (Join-Path $portableRoot "启动.bat") -Value $launcher -Encoding ASCII
$commitRef = if ([string]::IsNullOrWhiteSpace($ResolvedSourceCommit)) {
    "$SourceRef^{commit}"
}
else {
    "$ResolvedSourceCommit^{commit}"
}
$sourceCommit = (& git rev-parse --verify --end-of-options $commitRef).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceCommit -notmatch '^[0-9a-f]{40}$') {
    throw "Source reference does not resolve to a commit: $commitRef"
}
$repositoryPath = $repositoryUri.AbsolutePath.Trim('/').TrimEnd('/')
$apiUrl = "https://api.github.com/repos/$repositoryPath/releases/latest"
$metadata = [ordered]@{
    version = $Version
    sourceRef = $SourceRef
    source_commit = $sourceCommit
    latestReleaseApiUrl = $apiUrl
    releasesPageUrl = ($RepositoryUrl.TrimEnd('/') + "/releases/latest")
    checkIntervalHours = 24
    updatePolicy = "notify-and-open-download-page"
    dataDirectory = "data"
}
$metadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $portableRoot "portable-release.json") -Encoding UTF8
Set-Content -LiteralPath (Join-Path $portableData ".keep") -Value "Portable classroom data is created here." -Encoding UTF8

$templateRoot = Join-Path $PSScriptRoot "templates"
$sampleWorkbook = Join-Path $templateRoot "students-sample.xlsx"
if (Test-Path -LiteralPath $sampleWorkbook) {
    Copy-Item -LiteralPath $sampleWorkbook -Destination (Join-Path $portableData "students.xlsx")
    $photoHintRoot = Join-Path $portableData "student_photos"
    New-Item -ItemType Directory -Path $photoHintRoot -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $photoHintRoot "照片存放说明.txt") -Value @(
        "把学生照片放在这里：按班级建文件夹，文件夹名要和 students.xlsx 的工作表名一致。",
        "照片文件名用学号，例如 student_photos/1班/001.jpg；支持 jpg/jpeg/png/bmp。",
        "不放照片也可以正常点名，只是不显示头像。"
    ) -Encoding UTF8
}

if (Test-Path -LiteralPath $portableZip) {
    Remove-Item -LiteralPath $portableZip -Force
}
New-Item -ItemType Directory -Path $portableDeliveryRoot -Force | Out-Null
$portableManifestPath = Join-Path $portableDeliveryRoot "portable-package-manifest.json"
if (Test-Path -LiteralPath $portableManifestPath) {
    Remove-Item -LiteralPath $portableManifestPath -Force
}
Invoke-Step -Name "zip-portable" -Action {
    Compress-Archive -Path (Join-Path $portableRoot "*") -DestinationPath $portableZip
}

$manifest = [ordered]@{
    version = $Version
    source_ref = $SourceRef
    package = "portable"
    source_commit = $sourceCommit
    artifact = Split-Path -Leaf $portableZip
    update_policy = "notify-and-open-download-page"
    data_directory = "data/"
    excludes_local_classroom_data = $true
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $portableManifestPath -Encoding UTF8
$releaseManifestPath = Join-Path $releaseRoot "release-manifest.json"
if (Test-Path -LiteralPath $releaseManifestPath) {
    $releaseManifest = Get-Content -LiteralPath $releaseManifestPath -Raw | ConvertFrom-Json
    $releaseManifest.outputs | Add-Member -NotePropertyName portable -NotePropertyValue ("portable/{0}" -f (Split-Path -Leaf $portableZip)) -Force
    $releaseManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $releaseManifestPath -Encoding UTF8
}
Write-Host "[portable-package] DONE $portableZip"
