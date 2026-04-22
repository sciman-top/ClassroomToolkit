# 2026-04-22 Dependency Governance Gate Hardening

## Scope
- Boundary:
  - `scripts/quality/run-local-quality-gates.ps1`
  - `scripts/quality/check-dependency-upgrade-feasibility.ps1`
  - `scripts/quality/dependency-outdated-waivers.json`
- Current landing: residual dependency-upgrade false alarms and unstable gate semantics.
- Target landing: deterministic dependency governance gate that distinguishes stable-updatable vs prerelease-only updates and supports explicit waivers.

## Risk
- Level: Low-Medium
- Reason: quality-gate behavior changed (new dependency-governance step), but runtime/business code unchanged.

## Root Cause
1. Existing quality gates did not enforce dependency-upgrade feasibility directly.
2. `dotnet list --outdated --include-transitive` output can be misunderstood as immediate mandatory upgrades even when:
   - only prerelease tracks are newer, or
   - stable upgrades are pinned by upstream test platform constraints.
3. No structured waiver mechanism with expiry existed in gate flow.

## Changes
1. Added `scripts/quality/check-dependency-upgrade-feasibility.ps1`
- Scans `dotnet list <solution> package --outdated --include-transitive`.
- Separately scans `--include-prerelease` for clarity.
- Fails on unwaived stable outdated packages.
- Passes with explicit INFO when only prerelease tracks are newer.
- Supports active waivers with `expires_at`.

2. Added `scripts/quality/dependency-outdated-waivers.json`
- Introduced explicit, time-bounded waivers for current test-platform transitive residuals:
  - `Microsoft.ApplicationInsights`
  - `Microsoft.Testing.Extensions.Telemetry`
  - `Microsoft.Testing.Extensions.TrxReport.Abstractions`
  - `Microsoft.Testing.Platform`
  - `Microsoft.Testing.Platform.MSBuild`
- Expiry set to `2026-06-30`.

3. Updated `scripts/quality/run-local-quality-gates.ps1`
- Added new hard gate step: `dependency-governance` after `hotspot`.

## Commands and Key Outputs
1. `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-dependency-upgrade-feasibility.ps1`
- exit_code: 0
- key_output: stable outdated packages detected but all covered by active waivers; gate PASS.

2. `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick`
- exit_code: 0
- key_output: build pass; stable-tests(quick)=101 pass; contract=28 pass; hotspot pass; dependency-governance pass.

3. `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard`
- exit_code: 0
- key_output: build pass; stable-tests(standard)=3416 pass; contract=28 pass; hotspot pass; dependency-governance pass.

4. `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile full`
- exit_code: 0
- key_output: build pass; stable-tests(full)=3416 pass; contract=28 pass; hotspot pass; dependency-governance pass.

## Rollback
- `git restore --source=HEAD~1 -- scripts/quality/run-local-quality-gates.ps1 scripts/quality/check-dependency-upgrade-feasibility.ps1 scripts/quality/dependency-outdated-waivers.json`

## Follow-up
- Before `2026-06-30`, re-evaluate waiver list against latest stable `xunit.v3` and `Microsoft.Testing.Platform` compatibility.
