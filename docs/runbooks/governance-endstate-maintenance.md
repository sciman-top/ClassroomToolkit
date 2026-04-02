# Governance Endstate Maintenance Runbook

Last updated: 2026-04-03  
Status: active

## 1. Scope

- This runbook covers governance maintenance after enabling `evidence(all)` and endstate gates.
- It focuses on sustainable enforcement, avoiding over-design, and avoiding speculative optimization.

## 2. Endstate Definition

The repo is considered in endstate when all of the following remain stable:

- hard gates pass in fixed order: `build -> test -> contract/invariant -> hotspot`
- governance checks pass: waiver health, evidence completeness (`Mode=all`)
- doctor report is green (`endstate_score=100`)

## 3. Daily Operations

1. Run one-command loop:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/governance/run-endstate-loop.ps1 -Profile quick -Configuration Debug -EvidenceMode all
```
2. Review generated report:
- `docs/governance/reports/endstate-*.md`
3. If evidence gate fails on historical files, run:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/governance/backfill-evidence-template-fields.ps1
```

## 4. Anti-Overdesign Guardrails

- Do not add new governance dimensions without measurable failure evidence.
- Prefer existing script extension over introducing a new framework/toolchain.
- Keep default mode deterministic (`all` for enforcement); reserve optional modes only for migration.
- New checks must map to an existing blocker class: compile, test, contract, hotspot, waiver, evidence.

## 5. Anti-Overoptimization Guardrails

- Do not optimize build/test sequence that changes hard-gate semantics.
- Do not optimize by hiding failures (for example, converting blocker to warning) unless waiver exists with expiry.
- Any automation shortcut must preserve traceability fields:
  - `rule_id`
  - `commands`
  - `evidence`
  - `rollback`

## 6. Failure Triage

- build/test/contract/hotspot failure: block and fix code/test first.
- waiver/evidence failure: block and fix documentation/governance debt.
- doctor failure with script mismatch: re-run `install` from governance source, then rerun loop.

## 7. Rollback

1. Revert governance script/workflow batch.
2. Re-run hard gates.
3. Re-run doctor and capture evidence in `docs/change-evidence/`.

