# Governance Truth Source

Last updated: 2026-04-22  
Status: active

## 1. Canonical Entrypoints

- Local gate chain: `scripts/quality/run-local-quality-gates.ps1`
- Local truth-source guard: `scripts/quality/check-governance-truth-source.ps1`
- Analyzer backlog guard: `scripts/quality/check-analyzer-backlog-baseline.ps1`
- Analyzer backlog baseline: `scripts/quality/analyzer-backlog-baseline.json`
- CI wrappers:
  - `azure-pipelines.yml`
  - `.gitlab-ci.yml`

## 2. Canonical Gate Order

The current quality chain remains:

1. `build`
2. `test` (stable profile or full fallback)
3. `contract/invariant`
4. `hotspot`

Additional governance checks run after hotspot:

5. `governance-truth-source`
6. `dependency-governance`
7. `analyzer-backlog-baseline` (latest-all backlog must not regress)

## 3. Retired Paths (Do Not Reuse)

The following paths are retired in this repository and should not be referenced as active gates:

- `scripts/governance/*`
- `.github/workflows/quality-gate.yml`
- `.github/workflows/quality-gates.yml`

Historical snapshots under `docs/governance/reports/` and `docs/governance/*.md` may still contain old paths; treat them as archived evidence only, not as active runtime policy.

## 4. Verification Commands

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-governance-truth-source.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug
```
