[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
$errors = New-Object System.Collections.Generic.List[string]

$requiredPaths = @(
    "scripts/quality/run-local-quality-gates.ps1",
    "scripts/quality/check-governance-truth-source.ps1",
    "scripts/quality/check-analyzer-backlog-baseline.ps1",
    "scripts/quality/analyzer-backlog-baseline.json",
    "azure-pipelines.yml",
    ".gitlab-ci.yml"
)

foreach ($relativePath in $requiredPaths) {
    $fullPath = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        [void]$errors.Add(("missing required governance path: {0}" -f $relativePath))
    }
}

$retiredPaths = @(
    "scripts/governance",
    ".github/workflows/quality-gate.yml",
    ".github/workflows/quality-gates.yml"
)

foreach ($relativePath in $retiredPaths) {
    $fullPath = Join-Path $repoRoot $relativePath
    if (Test-Path -LiteralPath $fullPath) {
        [void]$errors.Add(("retired governance path should not exist: {0}" -f $relativePath))
    }
}

$activeDocs = @(
    "README.md",
    "README.en.md",
    "docs/handover.md",
    "docs/runbooks/governance-endstate-maintenance.md"
)

$blockedTokens = @(
    "scripts/governance/",
    ".github/workflows/quality-gate.yml",
    ".github/workflows/quality-gates.yml"
)

foreach ($relativePath in $activeDocs) {
    $fullPath = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        [void]$errors.Add(("active governance doc is missing: {0}" -f $relativePath))
        continue
    }

    foreach ($token in $blockedTokens) {
        $hits = Select-String -Path $fullPath -SimpleMatch -Pattern $token
        foreach ($hit in $hits) {
            [void]$errors.Add(("retired reference found in {0}:{1} -> {2}" -f $relativePath, $hit.LineNumber, $token))
        }
    }
}

if ($errors.Count -gt 0) {
    Write-Host "[governance-truth] FAIL"
    foreach ($message in $errors) {
        Write-Host ("- {0}" -f $message)
    }
    exit 1
}

Write-Host "[governance-truth] PASS"
exit 0
