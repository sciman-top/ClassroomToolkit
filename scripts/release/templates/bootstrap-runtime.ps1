param(
    [string]$RequiredMajor = "10"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-DesktopRuntime {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Major
    )

    $runtimeList = & dotnet --list-runtimes 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $false
    }

    foreach ($line in $runtimeList) {
        if ($line -match "^Microsoft\.WindowsDesktop\.App\s+$Major(\.|$)") {
            return $true
        }
    }

    return $false
}

if (Test-DesktopRuntime -Major $RequiredMajor) {
    Write-Host ".NET Desktop Runtime $RequiredMajor.x already installed."
    exit 0
}

$preReqDir = Join-Path $PSScriptRoot "prereq"
if (-not (Test-Path -LiteralPath $preReqDir)) {
    Write-Host "Runtime prereq directory missing: $preReqDir"
    exit 2
}

$installer = Get-ChildItem -LiteralPath $preReqDir -Filter "*desktop-runtime*$RequiredMajor*win-x64*.exe" -File |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($installer -eq $null) {
    Write-Host "Runtime installer not found under: $preReqDir"
    exit 3
}

Write-Host ".NET Desktop Runtime $RequiredMajor.x is missing."
Write-Host "Installer: $($installer.FullName)"
Write-Host "Please install runtime manually, then relaunch sciman Classroom Toolkit."
exit 10
