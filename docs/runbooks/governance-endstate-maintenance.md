# Governance Gate Maintenance Runbook

Last updated: 2026-04-23  
Status: active

## 1. Scope

This runbook defines the current governance maintenance loop for this repository.

Authoritative references:

- `docs/governance/truth-source.md`
- `scripts/quality/run-local-quality-gates.ps1`
- `scripts/quality/check-governance-truth-source.ps1`
- `scripts/quality/check-analyzer-backlog-baseline.ps1`

## 2. Daily Operations

1. Run the local quality gate chain:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug
```

2. Run truth-source drift check directly when needed:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-governance-truth-source.ps1
```

3. If release validation is required, run:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/release/preflight-check.ps1 -Configuration Debug
```

4. Collect UI performance sampling report (recommended before release sign-off):

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/validation/collect-ui-performance-samples.ps1 -LogRoot logs -WindowHours 24
```

## 3. Failure Triage

- `build/test/contract/hotspot` failure: treat as blocking, fix code or tests first.
- `governance-truth-source` failure: fix stale docs or missing/retired path drift first.
- `dependency-governance` failure: follow waiver process in `scripts/quality/dependency-outdated-waivers.json`.
- `dependency-vulnerability` failure: upgrade or pin vulnerable package before merge/release.
- `logging-alert-threshold` failure: inspect runtime log drop pressure and reduce dropped-log count below threshold.
- `analyzer-backlog-baseline` failure: treat as backlog regression; reduce new CA diagnostics or update baseline only after explicit治理评审.
- `MSB3021/MSB3027` copy-lock failure: close running app instance / Visual Studio file-lock holders before rerun.

## 4. Retired Entrypoints

Legacy governance-script lane and legacy GitHub quality-gate workflow lane are retired in this repository.

Historical files that mention old paths are archive evidence only.

## 5. Rollback

1. Revert the governance-truth-source changeset.
2. Re-run local quality gate chain.
3. Record rollback evidence under `docs/change-evidence/`.
