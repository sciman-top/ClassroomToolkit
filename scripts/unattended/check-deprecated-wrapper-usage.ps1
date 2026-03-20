param(
    [string]$RepoRoot = ".",
    [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
Push-Location $repoPath
try {
    $deprecatedFiles = @(
        "scripts/terminal-closure.ps1",
        "scripts/run-autonomous-execution-loop.ps1"
    )

    $scanTargets = @(
        "docs/refactor/*.json",
        "docs/ui-refactor/*.json",
        ".github/workflows/*.yml",
        ".github/workflows/*.yaml",
        "scripts/**/*.ps1"
    )

    $matches = @()
    foreach ($target in $scanTargets) {
        foreach ($dep in $deprecatedFiles) {
            $result = rg -n `
                --glob $target `
                --glob "!$dep" `
                --glob "!scripts/unattended/check-deprecated-wrapper-usage.ps1" `
                --fixed-strings $dep .
            if ($LASTEXITCODE -eq 0) {
                $matches += @($result)
            }
        }
    }

    $uniqueMatches = @($matches | Sort-Object -Unique)
    $status = if ($uniqueMatches.Count -eq 0) { "ok" } else { "failed" }

    $report = [ordered]@{
        status = $status
        deprecated_files = $deprecatedFiles
        findings_count = $uniqueMatches.Count
        findings = $uniqueMatches
    }

    if ($AsJson) {
        $report | ConvertTo-Json -Depth 20
    }
    else {
        $report
    }

    if ($status -eq "ok") {
        exit 0
    }

    exit 2
}
finally {
    Pop-Location
}
