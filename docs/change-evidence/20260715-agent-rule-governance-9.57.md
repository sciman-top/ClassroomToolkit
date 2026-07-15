# Agent Rule Governance 9.57

## Scope and boundary

- repository: `ClassroomToolkit`
- frozen baseline: `d364d547002fffd2fa7f64199d9209feaf7ebb98`
- task branch: `codex/agent-rule-governance-9.57`
- write-set: `AGENTS.md` and this evidence file; `CLAUDE.md` remains the verified import-only wrapper
- release review: `rule_release=9.57 / project_contract_version=2.0 / coordination_schema=2.3`
- semantic basis: Claude Code's current official memory documentation permits imports up to five hops; the project WHERE/HOW contract itself is unchanged
- exclusions: no product/runtime/schema/data/dependency/auth/provider/secret/MCP/account/process/hosted-UI change
- worktree setup generated an untracked `.githooks/` directory; it is excluded from the write-set and will not be staged

## Verification ledger

- wrapper: `CLAUDE.md` verified as the import-only `@AGENTS.md` wrapper, no BOM; control-repo `--require-all` target audit passed for all 9 isolated targets
- build: passed with 0 warnings and 0 errors
- test: standard suite passed, 3544 tests
- contract/invariant: contract suite passed, 29 tests; governance truth, dependency governance, vulnerability and analyzer-backlog checks passed (active waivers remained explicit)
- hotspot/full: line budgets and the repository full quality gate passed; analyzer backlog total was 0
- diff hygiene: `git diff --check` passed; setup-created untracked `.githooks/` remained excluded and unstaged
- five-axis review: correctness/readability/architecture/security/performance passed with no Critical or Required finding
- Git publication: not yet executed at this capture point

## Compatibility and rollback

- compatibility: content-release review marker only; repository commands, invariants, external behavior, data formats, and wrapper loading shape remain unchanged
- rollback: revert only `AGENTS.md` and this evidence file from the task commit; do not reset, clean, or include unrelated local history

## Completion boundary at capture

- `repo-side completed=true`
- `published branch=false`
- `default-branch effective=false`
- `hosted/manual accepted=false`
- `fully completed=false`
