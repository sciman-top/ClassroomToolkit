# Agent Rule Governance 9.56

- verified_at: `2026-07-15T00:30:00+08:00`
- scope: `AGENTS.md` global review marker only; application, data formats, and dependencies were not changed.
- risk: low for the rule marker; release verification is blocked by dependency governance.
- compatibility: project contract remains `2.0`; `CLAUDE.md` remains the one-line `@AGENTS.md` wrapper.

## Ordered gates

| stage | command | exit | key result |
|---|---|---:|---|
| build | `dotnet build ClassroomToolkit.sln -c Debug` | 0 | 0 warnings, 0 errors |
| test | `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` | 0 | 3544 passed |
| contract/invariant | filtered architecture/Interop lifecycle tests | 0 | 29 passed |
| hotspot | `scripts/quality/check-hotspot-line-budgets.ps1` | 0 | all C# files within 1200-line budget |
| canonical full | `scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug` | 1 | blocked at `dependency-governance`; preceding build/test/contract/hotspot passed |
| rule contract | control-repo `verify-target-project-rules.py --require-all` | 0 | project rule/wrapper/workflow passed |

## Blocker

`scripts/quality/dependency-outdated-waivers.json` expired on `2026-06-30`. The full gate reported unwaived stable drift including `SixLabors.Fonts`, `SourceGear.sqlite3`, `Microsoft.ApplicationInsights`, and test-platform packages. Renewing risk acceptance or performing major/test-platform upgrades requires an owner decision and a dedicated compatibility slice; this rule-only task does neither.

Rollback is limited to this evidence file and the `AGENTS.md` 9.56 marker. This repository is audited and changed but not fully verified for release.
