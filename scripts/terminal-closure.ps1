Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Error @"
DEPRECATED_ENTRYPOINT: scripts/terminal-closure.ps1 is retired and no longer executable.
Use the unified entrypoint instead:
powershell -File scripts/run-unattended-loop.ps1 -Mode checklist -TaskFile docs/superpowers/plans/2026-03-20-terminal-architecture-closure.tasks.json -SkipManualValidation -ForceReleaseWithoutManual
"@
exit 1
