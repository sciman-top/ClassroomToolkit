# CI Refactor State Clean-Runner Repair

## Basis and root cause

- issue: `locked-restore` failed on fresh `main` and the governance PR before build/test because `.codex/refactor-state.json` was absent
- root cause: `.codex/` is intentionally gitignored, while the workflow invoked `check-doc-consistency.ps1` with that local-only file as a mandatory default
- second inherited clean-runner gap: after the state gate passed, the workflow reached `resolve-stable-profile.ps1`, which had been deleted by commit `da390253` while the workflow reference remained
- boundary: independent CI repair from `origin/main`; no agent-rule, product, dependency, auth, provider, secret, MCP, process, or data-format change

## Change

- keep the script fail-closed by default when the state file is absent
- add an explicit clean-runner fallback that derives the same task/status shape from tracked `docs/refactor/tasks.json` only when all task entries have `status_hint`
- make `locked-restore` opt into that fallback and expose `state_source` in JSON output
- restore the machine-required automated-freeze status line in `docs/handover.md`; the first fallback run correctly failed closed until this tracked drift was repaired
- restore `scripts/validation/resolve-stable-profile.ps1` byte-for-byte from its last tracked Git object (`7a5dffb6...`), preserving the original pull-request/push profile mapping

## Verification and rollback

- red: missing `.codex/refactor-state.json` without the switch exited 1
- first fallback run exited 2 on the missing handover status line, proving the fallback did not bypass the consistency gate
- green: after repairing the tracked handover drift, fallback returned `status=ok`, `state_source=task_status_hints:docs/refactor/tasks.json`, `issues_remaining=0`, and exited 0
- profile contract: `locked-restore` maps PR to `quick` and push to `standard`; `quality-gate` maps PR to `standard` and push to `full`
- build: passed with 0 warnings and 0 errors
- test: 3544 tests passed; contract/invariant filter passed 29 tests
- hotspot: line-budget gate passed; canonical standard quality profile passed, including governance truth, dependency governance, vulnerability scan, and analyzer backlog total 0
- five-axis review: correctness/readability/architecture/security/performance passed with no Critical or Required finding
- rollback: revert only this repair commit; the default missing-state behavior remains the safety baseline

## Completion boundary

- `locally_verified=true`
- `default_branch_effective=false`
- `hosted/manual accepted=false`
