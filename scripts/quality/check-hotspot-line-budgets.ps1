[CmdletBinding()]
param(
    [int]$MaxLines = 1200,
    [int]$MinDecisionLines = 15
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$srcRoot = Join-Path $repoRoot "src"
$baselinePath = Join-Path $PSScriptRoot "hotspot-microclass-baseline.txt"

$violations = @()
$files = Get-ChildItem -Path $srcRoot -Recurse -Filter *.cs | Where-Object {
    $_.FullName -notmatch "\\obj\\" -and $_.FullName -notmatch "\\bin\\"
}

foreach ($file in $files) {
    $lineCount = (Get-Content -LiteralPath $file.FullName | Measure-Object -Line).Lines
    if ($lineCount -gt $MaxLines) {
        $relative = $file.FullName.Substring($repoRoot.Path.Length + 1).Replace('\', '/')
        $violations += "${relative}:$lineCount"
    }
}

if ($violations.Count -gt 0) {
    Write-Host "[hotspot] FAIL - file line budget exceeded (max=$MaxLines):"
    $violations | ForEach-Object { Write-Host "  - $_" }
}

# 碎片化门禁：新决策类文件不得低于行数下限；基线只收录存量，文件删除或长大后的基线条目必须修剪。
# 退役条件：基线清空后删除 hotspot-microclass-baseline.txt，行数下限检查自持。
$decisionSuffixes = @("Policy", "Executor", "StateUpdater", "Coordinator", "Defaults", "Thresholds")
$baseline = @()
if (Test-Path $baselinePath) {
    $baseline = Get-Content -LiteralPath $baselinePath | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" }
}

$decisionViolations = @()
$liveBaselineEntries = @{}
foreach ($file in $files) {
    $lineCount = (Get-Content -LiteralPath $file.FullName | Measure-Object -Line).Lines
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
    $isDecisionFile = $false
    foreach ($suffix in $decisionSuffixes) {
        if ($baseName.EndsWith($suffix, [System.StringComparison]::Ordinal)) {
            $isDecisionFile = $true
            break
        }
    }
    if (-not $isDecisionFile) {
        continue
    }

    $srcRelative = $file.FullName.Substring($srcRoot.Length + 1).Replace('\', '/')
    if ($lineCount -lt $MinDecisionLines) {
        if ($baseline -notcontains $srcRelative) {
            $decisionViolations += "$srcRelative ($lineCount lines < $MinDecisionLines; 新决策类不得低于 $MinDecisionLines 行)"
        }
        $liveBaselineEntries[$srcRelative] = $true
    }
}

$staleBaselineEntries = @($baseline | Where-Object { -not $liveBaselineEntries.ContainsKey($_) })
if ($staleBaselineEntries.Count -gt 0) {
    Write-Host "[hotspot] FAIL - stale microclass baseline entries (文件已删除或已超过下限，请从 hotspot-microclass-baseline.txt 修剪):"
    $staleBaselineEntries | ForEach-Object { Write-Host "  - $_" }
}

if ($decisionViolations.Count -gt 0) {
    Write-Host "[hotspot] FAIL - micro decision files below line floor (min=$MinDecisionLines):"
    $decisionViolations | ForEach-Object { Write-Host "  - $_" }
}

if ($violations.Count -gt 0 -or $decisionViolations.Count -gt 0 -or $staleBaselineEntries.Count -gt 0) {
    exit 2
}

Write-Host "[hotspot] PASS - all .cs files within line budget (max=$MaxLines) and decision files within fragmentation rules (min=$MinDecisionLines)"
