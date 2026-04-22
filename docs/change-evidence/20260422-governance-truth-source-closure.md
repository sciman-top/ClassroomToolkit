# 20260422 Governance Truth Source Closure

## Goal

Close governance truth-source drift so that active docs, gate scripts, and CI entry semantics refer to the same executable paths.

## Scope

- `scripts/quality/check-governance-truth-source.ps1` (new)
- `scripts/quality/run-local-quality-gates.ps1`
- `tests/ClassroomToolkit.Tests/GovernanceTruthSourceContractTests.cs` (new)
- `docs/governance/truth-source.md` (new)
- `docs/governance/reports/README.md` (new)
- `docs/governance/merge-report.md`
- `docs/governance/metrics-auto.md`
- `docs/runbooks/governance-endstate-maintenance.md`
- `docs/handover.md`
- `docs/validation/evidence/2026-04-10-full-governance-v2/05-observability-security-release-dataevolution.md`

## Changes

1. Added a dedicated governance truth-source gate check:
   - validates required active paths exist:
     - `scripts/quality/run-local-quality-gates.ps1`
     - `scripts/quality/check-governance-truth-source.ps1`
     - `azure-pipelines.yml`
     - `.gitlab-ci.yml`
   - validates retired paths do not exist:
     - `scripts/governance`
     - `.github/workflows/quality-gate.yml`
     - `.github/workflows/quality-gates.yml`
   - blocks stale references in active docs (`README*`, `docs/handover.md`, governance runbook).
2. Wired truth-source check into local gate chain after hotspot.
3. Added contract tests to freeze truth-source semantics and prevent drift.
4. Rewrote governance docs to separate active truth source vs archived snapshots.
5. Updated `handover` CI gate reference from retired GitHub workflow file to current script/CI wrappers.
6. Marked old 2026-04-10 observability evidence note as historical snapshot (non-authoritative for current gate truth).

## Verification

1. Truth-source script:
   - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-governance-truth-source.ps1`
   - result: `PASS`
2. Contract tests (isolated output to avoid running-app file locks):
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~GovernanceTruthSourceContractTests|FullyQualifiedName~RunLocalQualityGatesProfilePropagationContractTests|FullyQualifiedName~RepositoryDocumentationAndBuildHardeningContractTests" -p:OutDir=D:\CODE\ClassroomToolkit\.agent-build\truth-source-test\`
   - result: `PASS` (`10/10`)
3. Hotspot check:
   - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
   - result: `PASS`
4. Dependency governance check:
   - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-dependency-upgrade-feasibility.ps1`
   - result: `PASS` (all stable outdated packages covered by active waivers)

## Risks

- Full local quality-gates command can still fail when `devenv` / app runtime locks output DLLs (`MSB3021/MSB3027`).
- This change closes governance path drift, but does not auto-resolve process-lock runtime conditions.

## Rollback

Revert this change set:

- `git restore --source=HEAD~1 -- scripts/quality/check-governance-truth-source.ps1 scripts/quality/run-local-quality-gates.ps1 tests/ClassroomToolkit.Tests/GovernanceTruthSourceContractTests.cs docs/governance/truth-source.md docs/governance/reports/README.md docs/governance/merge-report.md docs/governance/metrics-auto.md docs/runbooks/governance-endstate-maintenance.md docs/handover.md docs/validation/evidence/2026-04-10-full-governance-v2/05-observability-security-release-dataevolution.md docs/change-evidence/20260422-governance-truth-source-closure.md`
