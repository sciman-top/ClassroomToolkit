# Unattended Loop Integration Runbook

## Goal
Unify unattended execution into one stable entrypoint and reduce token waste caused by script errors, blocking gates, or repeated reruns.

## Current entrypoints
1. `scripts/run-unattended-loop.ps1`
- Unified router.
- `-Mode checklist`: runs checklist task graph.
- `-Mode refactor`: runs stateful refactor loop (`run-refactor-loop.ps1`).

2. `scripts/run-checklist-loop.ps1`
- Generic checklist executor.
- Uses task descriptor (`tasks.json`) with per-task prompt + gates.
- Supports resume, preflight, retries, rollback, and structured summary.

3. `scripts/terminal-closure.ps1` (deprecated hard-stop)
- Retired compatibility wrapper.
- Always exits with migration instruction; do not use in automation.

4. `scripts/run-autonomous-execution-loop.ps1` (deprecated hard-stop)
- Retired compatibility wrapper.
- Always exits with migration instruction; do not use in automation.

## Checklist task schema
`run-checklist-loop.ps1` accepts gates in two forms:
1. Legacy string gate (still supported, not recommended).
2. Structured gate (recommended):

```json
{
  "command": "dotnet",
  "args": ["test", "tests/Project.Tests.csproj", "-c", "Debug", "--filter", "FullyQualifiedName~Smoke|FullyQualifiedName~Guard"]
}
```

Use structured gates to avoid shell parsing surprises (`|`, `&`, quotes).

## Portability to other projects
1. Copy these scripts:
- `scripts/run-unattended-loop.ps1`
- `scripts/run-checklist-loop.ps1`

2. Create a project task file, for example:
- `docs/unattended/tasks.json`

3. Run:
```powershell
powershell -File scripts/run-unattended-loop.ps1 `
  -Mode checklist `
  -TaskFile docs/unattended/tasks.json `
  -SkipManualValidation `
  -ForceReleaseWithoutManual `
  -CodexTimeoutSeconds 1200 `
  -CodexIdleTimeoutSeconds 180 `
  -GateTimeoutSeconds 900 `
  -GateIdleTimeoutSeconds 120
```

4. Keep project-specific checks in task gates only. Do not hardcode project paths in script logic.

## One-click adapter transfer
Use one command from source repo to install unattended/refactor adapter files into target repo:

```powershell
powershell -File scripts/refactor/transfer-refactor-adapter.ps1 `
  -TargetRepoRoot E:\CODE\TargetRepo `
  -Force
```

Then run smoke validation in target repo:

```powershell
powershell -File scripts/unattended/test-checklist-loop-smoke.ps1 -RepoRoot .
```

This gives a minimal migration + verification path without manually stitching multiple scripts.

## Deprecated Wrapper Policy
- Official executable entrypoint is only `scripts/run-unattended-loop.ps1`.
- `scripts/terminal-closure.ps1` and `scripts/run-autonomous-execution-loop.ps1` are hard-stopped and return non-zero.
- CI runs `scripts/unattended/check-deprecated-wrapper-usage.ps1` to block new active references to deprecated wrappers.

Cross-project portability regression (transfer + smoke in isolated target repo):

```powershell
powershell -File scripts/unattended/test-portability-regression.ps1 `
  -SourceRepoRoot . `
  -Clean
```

## Token waste prevention baseline
1. Preflight before any Codex iteration:
- task file parse
- command existence
- gate argument validity

2. Fail fast:
- stop on first non-recoverable error
- limit retries with `-MaxAttemptsPerTask`

3. Resume instead of rerun:
- use `-StartFromTaskId` after failure

4. Single runner lock:
- `run-checklist-loop.ps1` uses lock file (`.codex/checklist-loop.lock.json`) to prevent concurrent runs.

5. Watchdog timeouts:
- checklist supports `CodexTimeoutSeconds`, `CodexIdleTimeoutSeconds`, `GateTimeoutSeconds`, `GateIdleTimeoutSeconds`
- gate-level override supported in task json with `timeout_seconds` and `idle_timeout_seconds`
- refactor mode supports `IterationTimeoutSeconds` and `IdleTimeoutSeconds` through unified wrapper forwarding

5. Structured summary always:
- each run emits `.codex/logs/checklist-loop/run-*.summary.json`
- includes failed task, failed gate, error class, rollback checkpoint, and log paths.

## Recommended operating model
1. `DryRun` first.
2. Run one canary task (start from a single task id).
3. Run full unattended loop.
4. If interrupted, resume from failed task id.
