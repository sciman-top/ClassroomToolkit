# Governance Truth Source

Last updated: 2026-08-15
Status: active

## 1. Canonical Entrypoints

- Local gate chain: `scripts/quality/run-local-quality-gates.ps1`
- Analyzer backlog guard: `scripts/quality/check-analyzer-backlog-baseline.ps1`
- Dependency vulnerability guard: `scripts/quality/check-dependency-vulnerabilities.ps1`
- Analyzer backlog baseline: `scripts/quality/analyzer-backlog-baseline.json`
- Active CI: `.github/workflows/locked-restore.yml` and `.github/workflows/release-package.yml`

## 2. Canonical Gate Order

The current quality chain remains:

1. `build`
2. `test` (the selected profile excludes the contract groups from step 3)
3. `contract/invariant`
4. `hotspot`

Profile additions after hotspot:

- `quick`: no network governance checks.
- `standard`: no network governance checks.
- `full`: `dependency-vulnerability`, dependency-upgrade audit, then `latest-all` analyzer audit.

The runtime log diagnostic is deliberately outside code gates because host-local logs are not a deterministic property of the worktree. Dependency updates are release-maintenance input, not routine correctness failures.

## 3. Retired Paths (Do Not Reuse)

The following paths are retired in this repository and should not be referenced as active gates:

- `scripts/governance/*`
- `.github/workflows/quality-gate.yml`
- `.github/workflows/quality-gates.yml`
- `azure-pipelines.yml`
- `.gitlab-ci.yml`
- `scripts/quality/check-governance-truth-source.ps1`
- `scripts/validation/validate-stable-test-config.ps1`

Retired governance snapshots remain available from Git history; they are not active runtime policy.

## 4. Verification Commands

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile full -Configuration Release
```
