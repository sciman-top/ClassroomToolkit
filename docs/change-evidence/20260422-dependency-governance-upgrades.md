# 2026-04-22 Dependency Governance Upgrades

## Summary

- scope: `tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`
- scope: `tests/ClassroomToolkit.Tests/InkReplayBaselineIntegrityTests.cs`
- scope: `src/ClassroomToolkit.App/ClassroomToolkit.App.csproj`
- scope: `src/ClassroomToolkit.Infra/ClassroomToolkit.Infra.csproj`
- scope: `src/ClassroomToolkit.Services/ClassroomToolkit.Services.csproj`
- scope: `*.packages.lock.json`
- scope: targeted test files updated for `xunit.v3` cancellation-token analyzer compliance
- target destination: upgrade low-risk test/runtime dependencies without changing business semantics, external contracts, or persisted data formats
- risk level: low to medium

## Basis

1. Test stack still used legacy `xunit 2.9.3`, which NuGet reported as deprecated.
2. Official xUnit.net v3 migration guidance allows migrating to `xunit.v3`, and official xUnit documentation recommends keeping `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk` during compatibility transition for existing `dotnet test` / VSTest environments.
3. Runtime packages in App/Infra/Services were still on `8.x` while the project targets `.NET 10`, and NuGet reported stable `10.0.7` updates for:
   - `Microsoft.Extensions.DependencyInjection`
   - `Microsoft.Extensions.Logging`
   - `Microsoft.Extensions.Logging.Console`
   - `Microsoft.Data.Sqlite`
   - `System.Speech`
4. `coverlet.collector` also had a low-risk stable update available.
5. `FluentAssertions 6.12.0` had a newer stable major version `8.9.0`; after test-stack migration it became the remaining top-level outdated package in the test project.

## Official Sources

- xUnit migration guide:
  - https://xunit.net/docs/getting-started/v3/migration
- xUnit Microsoft Testing Platform guidance:
  - https://xunit.net/docs/getting-started/v3/microsoft-testing-platform
- `xunit.v3` NuGet package:
  - https://www.nuget.org/packages/xunit.v3
- `xunit.runner.visualstudio` NuGet package:
  - https://www.nuget.org/packages/xunit.runner.visualstudio/
- `Microsoft.NET.Test.Sdk` NuGet package:
  - https://www.nuget.org/packages/Microsoft.NET.Test.Sdk

## Changes

### Test stack

- replaced `xunit 2.9.3` with `xunit.v3 3.2.2`
- upgraded `FluentAssertions` from `6.12.0` to `8.9.0`
- upgraded `Microsoft.NET.Test.Sdk` from `17.6.0` to `18.5.0`
- upgraded `coverlet.collector` from `6.0.0` to `10.0.0`
- kept `xunit.runner.visualstudio 3.1.5` to preserve existing `dotnet test` / VSTest compatibility
- updated affected tests to pass `TestContext.Current.CancellationToken` or linked cancellation tokens where v3 analyzers required it
- updated legacy numeric assertion API usage in `InkReplayBaselineIntegrityTests` to the FluentAssertions 8 naming (`BeLessThanOrEqualTo`)

### Runtime packages

- `ClassroomToolkit.App`
  - `Microsoft.Extensions.DependencyInjection` -> `10.0.7`
  - `Microsoft.Extensions.Logging` -> `10.0.7`
  - `Microsoft.Extensions.Logging.Console` -> `10.0.7`
  - `System.Speech` -> `10.0.7`
- `ClassroomToolkit.Infra`
  - `Microsoft.Data.Sqlite` -> `10.0.7`
  - `Microsoft.Extensions.Logging` -> `10.0.7`
- `ClassroomToolkit.Services`
  - `System.Speech` -> `10.0.7`
- refreshed lock files:
  - `src/ClassroomToolkit.App/packages.lock.json`
  - `src/ClassroomToolkit.Infra/packages.lock.json`
  - `src/ClassroomToolkit.Services/packages.lock.json`
  - `tests/ClassroomToolkit.Tests/packages.lock.json`

## Commands And Evidence

### Dependency discovery

- cmd: `dotnet list ClassroomToolkit.sln package --outdated --include-transitive`
  - exit_code: `0`
  - key_output: confirmed stable updates for test/runtime packages listed above
- cmd: `dotnet list ClassroomToolkit.sln package --deprecated`
  - exit_code: `0`
  - key_output before change: `xunit 2.9.3` deprecated as legacy
  - key_output after change: no deprecated packages

### Upgrade validation

- cmd: `dotnet build tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1`
  - exit_code: `0`
  - key_output: `xunit.v3` + `FluentAssertions 8` migration compiled successfully after analyzer-driven test fixes and assertion API rename
- cmd: `dotnet build ClassroomToolkit.sln -c Debug -m:1`
  - exit_code: `0`
  - key_output: full solution build succeeded with upgraded runtime packages

### Hard gates

- cmd: `dotnet build ClassroomToolkit.sln -c Debug -m:1`
  - exit_code: `0`
  - key_output: success
- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1`
  - exit_code: `0`
  - key_output: `3406` tests passed
- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code: `0`
  - key_output: `28` contract/invariant tests passed
- cmd: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
  - exit_code: `0`
  - key_output: hotspot pass
- cmd: `dotnet list ClassroomToolkit.sln package --vulnerable --include-transitive`
  - exit_code: `0`
  - key_output: no known vulnerable packages

## Residuals

- several transitive packages remain behind latest versions (`DocumentFormat.OpenXml`, `SixLabors.Fonts`, `SQLitePCLRaw.*`, etc.); these are secondary to the direct package alignments completed here.
- `dotnet list ... --outdated --include-transitive` still reports newer `Microsoft.Testing.Platform*`, `Microsoft.ApplicationInsights`, `System.IO.Packaging`, and related transitive updates in the test project. Current evidence shows they are neither deprecated nor vulnerable, so they remain a separate compatibility slice instead of being mixed into this round.

## Rollback

- rollback command:
  - `git restore --source=HEAD -- src/ClassroomToolkit.App/ClassroomToolkit.App.csproj src/ClassroomToolkit.App/packages.lock.json src/ClassroomToolkit.Infra/ClassroomToolkit.Infra.csproj src/ClassroomToolkit.Infra/packages.lock.json src/ClassroomToolkit.Services/ClassroomToolkit.Services.csproj src/ClassroomToolkit.Services/packages.lock.json tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj tests/ClassroomToolkit.Tests/packages.lock.json tests/ClassroomToolkit.Tests/App/WindowInteropRetryExecutorTests.cs tests/ClassroomToolkit.Tests/InkExportServiceTests.cs tests/ClassroomToolkit.Tests/InkReplayBaselineIntegrityTests.cs tests/ClassroomToolkit.Tests/InteropBackgroundDispatchExecutorTests.cs tests/ClassroomToolkit.Tests/RollCallViewModelPreloadConcurrencyTests.cs tests/ClassroomToolkit.Tests/SafeTaskRunnerTests.cs tests/ClassroomToolkit.Tests/StudentPhotoResolverTests.cs docs/change-evidence/20260422-dependency-governance-upgrades.md`
