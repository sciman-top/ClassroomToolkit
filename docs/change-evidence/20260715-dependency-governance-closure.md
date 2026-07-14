# Dependency Governance Closure - 2026-07-15

- rules: `R1-R8`, `E3`, `E5`
- verified_at: `2026-07-15T02:06:40+08:00`
- scope: stable dependency refresh and time-bounded compatibility waivers
- risk: medium; runtime package versions and the test platform SDK are updated without changing public APIs or persisted data formats
- compatibility: `students.xlsx`, `student_photos/`, `settings.ini`, window entry points, and public DTO contracts remain unchanged

## Changes

- Updated the .NET 10 package family from `10.0.9` to `10.0.10`.
- Updated `Microsoft.NET.Test.Sdk` from `18.7.0` to `18.8.1`.
- Updated `SourceGear.sqlite3` from `3.50.4.5` to `3.53.3`.
- Kept `SixLabors.Fonts` at `2.1.3` because `3.0.0` is a major-version visual/export compatibility change.
- Kept the `Microsoft.Testing.Platform 1.9.1` transitive family because the current latest stable `xunit.v3 3.2.2` dependency chain pins that line.
- Renewed only those compatibility waivers through `2026-10-15`, with owner, active status, recovery plan, and evidence link.

## Verification

| command | exit | key result |
|---|---:|---|
| `dotnet restore ClassroomToolkit.sln --force-evaluate` | 0 | all seven projects restored and lock files refreshed |
| targeted SQLite/workbook/native-probe tests | 0 | 73 passed, 0 failed |
| `scripts/quality/check-dependency-upgrade-feasibility.ps1` | 0 | only `SixLabors.Fonts` and xUnit MTP transitive packages remain under active waiver |
| `scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug` | 0 | build passed with 0 warnings/errors; 3544 tests and 29 contracts passed; hotspot, governance, vulnerability, and analyzer gates passed |
| `scripts/validation/run-compatibility-preflight.ps1 -Configuration Debug` | 0 | build, 3544 tests, 29 contracts, and hotspot passed |

## Residual risk

- `SixLabors.Fonts 3.0.0` remains deferred because its major-version font metric changes require visual and workbook-export validation.
- The current latest stable `xunit.v3 3.2.2` chain still resolves `Microsoft.Testing.Platform 1.9.1`; the MTP and `Microsoft.ApplicationInsights` residuals therefore remain a test-platform migration item.
- All active waivers expire on `2026-10-15`; recovery conditions are encoded in `scripts/quality/dependency-outdated-waivers.json`.

## Rollback

Revert this evidence file, the four touched project files, their generated `packages.lock.json` files, and `scripts/quality/dependency-outdated-waivers.json`; restore the previous dependency graph, then rerun the standard gate. Reverting the waiver file restores the original expired-waiver blocker by design.
