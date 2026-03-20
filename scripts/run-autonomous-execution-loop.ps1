Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Error @"
DEPRECATED_ENTRYPOINT: scripts/run-autonomous-execution-loop.ps1 is retired and no longer executable.
Use the unified entrypoint instead:
powershell -File scripts/run-unattended-loop.ps1 -Mode refactor -RefactorModeId architecture-refactor
"@
exit 1
