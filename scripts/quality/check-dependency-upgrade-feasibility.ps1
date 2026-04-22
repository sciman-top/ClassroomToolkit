[CmdletBinding()]
param(
    [string]$Solution = "ClassroomToolkit.sln",
    [string]$WaiverPath = "scripts/quality/dependency-outdated-waivers.json",
    [switch]$FailOnStableOutdated = $true
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

function Get-PackageRowsFromDotnetOutput {
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
        if ($tokens.Count -lt 3) {
            continue
        }

        $rows += [pscustomobject]@{
            package = $tokens[0]
            resolved = $tokens[$tokens.Count - 2]
            latest = $tokens[$tokens.Count - 1]
            raw = $line
        }
    }

    return $rows
}

function Read-Waivers {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        return @()
    }

    $raw = Get-Content -LiteralPath $Path -Raw
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return @()
    }

    $json = $raw | ConvertFrom-Json
    if ($null -eq $json.waivers) {
        return @()
    }

    return @($json.waivers)
}

function Test-Waived {
    param(
        [Parameter(Mandatory = $true)][string]$Package,
        [Parameter(Mandatory = $true)]$Waivers,
        [Parameter(Mandatory = $true)][DateTime]$NowUtc
    )

    foreach ($waiver in $Waivers) {
        if ($null -eq $waiver.package -or [string]::IsNullOrWhiteSpace($waiver.package)) {
            continue
        }

        $nameMatches = $false
        if ($Package.Equals([string]$waiver.package, [StringComparison]::OrdinalIgnoreCase)) {
            $nameMatches = $true
        }
        else {
            $regexProperty = $waiver.PSObject.Properties["package_regex"]
            if ($null -ne $regexProperty -and -not [string]::IsNullOrWhiteSpace([string]$regexProperty.Value)) {
                $nameMatches = $Package -match [string]$regexProperty.Value
            }
        }

        if (-not $nameMatches) {
            continue
        }

        if ($null -eq $waiver.expires_at -or [string]::IsNullOrWhiteSpace([string]$waiver.expires_at)) {
            continue
        }

        $expiresUtc = [DateTime]::Parse([string]$waiver.expires_at).ToUniversalTime()
        if ($NowUtc -le $expiresUtc) {
            return $true
        }
    }

    return $false
}

$resolvedSolution = Resolve-AbsolutePath -Path $Solution
if (-not (Test-Path -LiteralPath $resolvedSolution)) {
    throw "[dependency] Missing solution: $resolvedSolution"
}

$resolvedWaiverPath = Resolve-AbsolutePath -Path $WaiverPath
$waivers = Read-Waivers -Path $resolvedWaiverPath
$nowUtc = [DateTime]::UtcNow

Write-Host "[dependency] START scan"
$previousLanguage = $env:DOTNET_CLI_UI_LANGUAGE
$env:DOTNET_CLI_UI_LANGUAGE = "en"
try {
    $stableOutput = & dotnet list $resolvedSolution package --outdated --include-transitive 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "[dependency] dotnet list (stable) failed (exit=$LASTEXITCODE)."
    }

    $preOutput = & dotnet list $resolvedSolution package --outdated --include-transitive --include-prerelease 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "[dependency] dotnet list (prerelease) failed (exit=$LASTEXITCODE)."
    }
}
finally {
    $env:DOTNET_CLI_UI_LANGUAGE = $previousLanguage
}

$stableRows = Get-PackageRowsFromDotnetOutput -Lines @($stableOutput)
$preRows = Get-PackageRowsFromDotnetOutput -Lines @($preOutput)

if ($stableRows.Count -eq 0) {
    if ($preRows.Count -gt 0) {
        Write-Host "[dependency] INFO prerelease-only updates detected; no stable update available."
        $preRows | ForEach-Object { Write-Host ("  - {0}: resolved={1}, latest={2}" -f $_.package, $_.resolved, $_.latest) }
    }
    else {
        Write-Host "[dependency] PASS no outdated packages detected."
    }
    exit 0
}

$unwaived = @()
$waived = @()
foreach ($row in $stableRows) {
    if (Test-Waived -Package $row.package -Waivers $waivers -NowUtc $nowUtc) {
        $waived += $row
    }
    else {
        $unwaived += $row
    }
}

if ($waived.Count -gt 0) {
    Write-Host "[dependency] INFO stable outdated packages under active waiver:"
    $waived | ForEach-Object { Write-Host ("  - {0}: resolved={1}, latest={2}" -f $_.package, $_.resolved, $_.latest) }
}

if ($unwaived.Count -eq 0) {
    Write-Host "[dependency] PASS all stable outdated packages are covered by active waivers."
    exit 0
}

Write-Host "[dependency] FAIL unwaived stable outdated packages detected:"
$unwaived | ForEach-Object { Write-Host ("  - {0}: resolved={1}, latest={2}" -f $_.package, $_.resolved, $_.latest) }

if ($FailOnStableOutdated.IsPresent) {
    exit 2
}

Write-Host "[dependency] WARN fail-on-stable-outdated disabled; continuing."
exit 0
