# Governance Gate Maintenance Runbook

Last updated: 2026-08-03
Status: active

## 1. Scope

This runbook defines the current governance maintenance loop for this repository.

Authoritative references:

- `docs/governance/truth-source.md`
- `scripts/quality/run-local-quality-gates.ps1`
- `scripts/quality/check-analyzer-backlog-baseline.ps1`

## 2. Daily Operations

1. Run the local quality gate chain:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug
```

2. For normal delivery, run `-Profile standard`; for release validation, run:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/release/preflight-check.ps1 -Configuration Release -Profile full
```

3. Collect UI performance sampling report (recommended before release sign-off):

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/validation/collect-ui-performance-samples.ps1 -LogRoot logs -WindowHours 24
```

4. Collect settings-load performance sampling report (recommended before release sign-off):

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/validation/collect-settings-load-performance-samples.ps1 -Configuration Debug
```

## 3. Failure Triage

- `build/test/contract/hotspot` failure: treat as blocking, fix code or tests first.
- `dependency-upgrade-audit` failure in `full`: upgrade the package or follow the waiver process in `scripts/quality/dependency-outdated-waivers.json`.
- `dependency-vulnerability` failure: upgrade or pin vulnerable package before merge/release.
- Runtime logging diagnostics are operator-run; a failure describes the selected log window and does not invalidate unrelated source changes.
- `analyzer-backlog-baseline` failure: treat as backlog regression; reduce new CA diagnostics or update baseline only after explicit治理评审.
- `MSB3021/MSB3027` copy-lock failure: close running app instance / Visual Studio file-lock holders before rerun.

## 4. Retired Entrypoints

Legacy governance-script self-checks, placeholder Azure/GitLab wrappers, and legacy GitHub quality-gate workflow lanes are retired in this repository.

Historical files that mention old paths are archive evidence only.

## 5. Rollback

1. Revert the gate-profile changeset.
2. Re-run local quality gate chain.
3. Record rollback evidence under `docs/change-evidence/`.
