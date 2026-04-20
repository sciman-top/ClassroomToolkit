[CmdletBinding()]
param(
    [int]$MaxLines = 1200
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$srcRoot = Join-Path $repoRoot "src"

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
    exit 2
}

Write-Host "[hotspot] PASS - all .cs files within line budget (max=$MaxLines)"
