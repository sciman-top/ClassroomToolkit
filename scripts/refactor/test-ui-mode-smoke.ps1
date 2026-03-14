param(
    [string]$RepoRoot = ".",
    [string]$Scenario = "default"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Read-JsonFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "JSON file not found: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$registryPath = Join-Path $repoPath ".codex/refactor-modes.json"
$resolverPath = Join-Path $repoPath "scripts/refactor/resolve-refactor-mode.ps1"
$selectorPath = Join-Path $repoPath "scripts/refactor/select-next-task.ps1"
$stateUpdaterPath = Join-Path $repoPath "scripts/refactor/update-refactor-state.ps1"
$gateResumePath = Join-Path $repoPath "scripts/refactor/resume-manual-gate.ps1"
$reconPath = Join-Path $repoPath "scripts/refactor/test-governing-reconciliation.ps1"
$wrapperPath = Join-Path $repoPath "scripts/run-refactor-loop.ps1"
$installerPath = Join-Path $repoPath "scripts/refactor/install-refactor-adapter.ps1"
$exporterPath = Join-Path $repoPath "scripts/refactor/export-refactor-adapter.ps1"
$manifestPath = Join-Path $repoPath "scripts/refactor/refactor-adapter.manifest.json"
$uiStatePath = Join-Path $repoPath ".codex/ui-window-system-state.json"
$uiTaskPath = Join-Path $repoPath "docs/ui-refactor/tasks.json"
$uiConfigPath = Join-Path $repoPath ".codex/ui-window-system.config.json"
$acceptanceDoc = Join-Path $repoPath "docs/validation/ui-window-system-acceptance.md"

function Test-DefaultScenario {
    $registry = Read-JsonFile -Path $registryPath
    $uiMode = @($registry.modes | Where-Object { [string]$_.mode_id -eq "ui-window-system" })
    if ($uiMode.Count -ne 1) {
        throw "ui-window-system mode missing from registry."
    }

    $resolvedFamily = & powershell -File $resolverPath -RepoRoot $repoPath -Mode ui-overhaul -AsJson | ConvertFrom-Json
    if ([string]$resolvedFamily.mode_id -ne "ui-window-system") {
        throw "family mapping failed"
    }

    $resolvedConcrete = & powershell -File $resolverPath -RepoRoot $repoPath -Mode ui-window-system -AsJson | ConvertFrom-Json
    if ([string]$resolvedConcrete.mode_family -ne "ui-overhaul") {
        throw "concrete mode mapping failed"
    }

    $state = Read-JsonFile -Path $uiStatePath
    if ([string]$state.mode -ne "ui-window-system") {
        throw "wrong UI mode id"
    }
    if ([string]$state.mode_family -ne "ui-overhaul") {
        throw "wrong UI mode family"
    }

    $config = Read-JsonFile -Path $uiConfigPath
    if (-not [bool]$config.wrapper.reconciliation_refresh_on_gate_resume) {
        throw "wrapper reconciliation_refresh_on_gate_resume missing"
    }

    $selection = & powershell -File $selectorPath -TaskFile $uiTaskPath -StateFile $uiStatePath -AsJson | ConvertFrom-Json
    if ([string]$selection.id -ne "ui-foundation-bootstrap") {
        throw "unexpected initial UI task selection"
    }

    $taskDoc = Read-JsonFile -Path $uiTaskPath
    $expectedStages = @(
        "foundation",
        "controls",
        "window-shell",
        "main-scenes",
        "management-and-settings",
        "dialog-tail",
        "visual-regression"
    )
    foreach ($stage in $expectedStages) {
        if (-not (@($taskDoc.tasks | Where-Object { [string]$_.stage -eq $stage }).Count -ge 1)) {
            throw "stage missing from ui task graph: $stage"
        }
    }

    $expectedGates = @(
        "theme-freeze",
        "main-scene-freeze",
        "fullscreen-float-freeze",
        "final-visual-regression"
    )
    foreach ($gate in $expectedGates) {
        if (-not (@($taskDoc.tasks | Where-Object { [string]$_.manual_gate -eq $gate }).Count -eq 1)) {
            throw "manual gate missing or duplicated: $gate"
        }
    }

    $recon = & powershell -File $reconPath -TaskFile $uiTaskPath -ConfigFile $uiConfigPath -AsJson | ConvertFrom-Json
    if ([string]$recon.status -notin @("ok", "needs_reconciliation")) {
        throw "unexpected reconciliation status"
    }

    $manifest = Read-JsonFile -Path $manifestPath
    if (-not (@($manifest.entries | Where-Object { [string]$_.path -eq ".codex/refactor-modes.json" }).Count -ge 1)) {
        throw "registry missing from manifest"
    }
}

function Test-GateResumeScenario {
    $backup = Get-Content -LiteralPath $uiStatePath -Raw
    try {
        & powershell -File $stateUpdaterPath -Action block -StateFile $uiStatePath -TaskId ui-foundation-bootstrap -Summary "waiting for theme freeze" -Reason "visual_gate:theme-freeze" | Out-Null
        & powershell -File $gateResumePath -RepoRoot $repoPath -Mode ui-window-system -GateId theme-freeze -TaskId ui-foundation-bootstrap -EvidenceDoc $acceptanceDoc | Out-Null
        $state = Read-JsonFile -Path $uiStatePath
        if ([string]$state.tasks.'ui-foundation-bootstrap'.status -ne "pending") {
            throw "task not resumed to pending"
        }
        if (-not [bool]$state.theme_frozen) {
            throw "theme_frozen not set"
        }
        if ([string]::IsNullOrWhiteSpace([string]$state.governing_reconciliation.last_reconciled_at)) {
            throw "reconciliation refresh missing"
        }
    }
    finally {
        Set-Content -LiteralPath $uiStatePath -Value $backup -Encoding UTF8
    }
}

function Test-StaleReconciliationScenario {
    $backup = Get-Content -LiteralPath $uiTaskPath -Raw
    try {
        $doc = Read-JsonFile -Path $uiTaskPath
        $doc.governing_reconciliation.last_reconciled_at = "2000-01-01T00:00:00Z"
        $doc | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $uiTaskPath -Encoding UTF8
        $recon = & powershell -File $reconPath -TaskFile $uiTaskPath -ConfigFile $uiConfigPath -AsJson | ConvertFrom-Json
        if ([string]$recon.status -ne "needs_reconciliation") {
            throw "stale reconciliation should require refresh"
        }
    }
    finally {
        Set-Content -LiteralPath $uiTaskPath -Value $backup -Encoding UTF8
    }
}

function Test-LockContentionScenario {
    $lockPath = Join-Path $repoPath ".codex/refactor-loop.lock.json"
    $backupExists = Test-Path -LiteralPath $lockPath
    $backup = $null
    if ($backupExists) {
        $backup = Get-Content -LiteralPath $lockPath -Raw
    }

    try {
        [pscustomobject]@{
            owner_kind = "wrapper"
            loop_run_id = "smoke-test"
            pid = 999999
            started_at = [DateTime]::UtcNow.ToString("o")
            mode_id = "ui-window-system"
            mode_family = "ui-overhaul"
            repo_root = $repoPath
            state_file = ".codex/ui-window-system-state.json"
            task_file = "docs/ui-refactor/tasks.json"
        } | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $lockPath -Encoding UTF8

        $lock = Read-JsonFile -Path $lockPath
        if ([string]$lock.mode_id -ne "ui-window-system") {
            throw "mode_id missing from lock"
        }
    }
    finally {
        if ($backupExists) {
            Set-Content -LiteralPath $lockPath -Value $backup -Encoding UTF8
        }
        elseif (Test-Path -LiteralPath $lockPath) {
            Remove-Item -LiteralPath $lockPath -Force
        }
    }
}

function Test-InstallerExporterScenario {
    $manifest = Read-JsonFile -Path $manifestPath
    if (-not (@($manifest.entries | Where-Object { [string]$_.path -eq ".codex/ui-window-system-state.json" }).Count -ge 1)) {
        throw "ui state missing from manifest entries"
    }

    $installerText = Get-Content -LiteralPath $installerPath -Raw
    $exporterText = Get-Content -LiteralPath $exporterPath -Raw
    if ($installerText -notmatch "seed-state") {
        throw "installer does not handle seed-state entries"
    }
    if ($installerText -notmatch "mode_filter") {
        throw "installer does not contain mode-aware filtering"
    }
    if ($exporterText -notmatch "install-refactor-adapter.ps1") {
        throw "exporter no longer delegates to installer"
    }
    if ($exporterText -notmatch "-Mode") {
        throw "exporter does not forward mode"
    }
}

switch ($Scenario) {
    "default" { Test-DefaultScenario }
    "gate-resume-refresh" { Test-GateResumeScenario }
    "stale-reconciliation" { Test-StaleReconciliationScenario }
    "lock-contention" { Test-LockContentionScenario }
    "installer-exporter" { Test-InstallerExporterScenario }
    default { throw "Unsupported smoke scenario: $Scenario" }
}

Write-Host "PASS: $Scenario"
