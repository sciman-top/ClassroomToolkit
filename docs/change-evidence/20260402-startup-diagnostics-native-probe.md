# 2026-04-02 Startup Diagnostics Native Probe

- Rule IDs: `R1`, `R2`, `R3`, `R6`, `R8`
- Risk: medium
- Boundary:
  - current landing: `src/ClassroomToolkit.Services/Compatibility/StartupCompatibilityProbe.cs`, `src/ClassroomToolkit.App/MainWindow.xaml`, `src/ClassroomToolkit.App/MainWindow.Launcher.cs`
  - target destination: startup compatibility probe + launcher diagnostics entry
  - batch: `2026-04-02-startup-diagnostics-native-probe`

## Basis

- User reported repeated startup warning for missing `e_sqlite3.dll` while the published package was complete.
- Local inspection showed `Debug` output contained `runtimes/win-x64/native/e_sqlite3.dll`, but startup probe only checked `[e_sqlite3.dll](E:\CODE\ClassroomToolkit\src\ClassroomToolkit.App\bin\Debug\net10.0-windows\e_sqlite3.dll)` at root.
- User also requested a visible place to reopen compatibility detection after suppressing repeated warnings.

## Commands

- `codex status`
- `codex --version`
- `codex --help`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~StartupCompatibilityProbeTests|FullyQualifiedName~UiCopyContractTests"`
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## Evidence

- `platform_na`
  - reason: `codex status` failed in non-interactive shell with `stdin is not a terminal`
  - alternative_verification:
    - `codex --version` => `codex-cli 0.118.0`
    - `codex --help` returned CLI help successfully
  - evidence_link: this file
  - expires_at: `2026-04-09`
- Root cause evidence:
  - `Debug` output contained `[e_sqlite3.dll](E:\CODE\ClassroomToolkit\src\ClassroomToolkit.App\bin\Debug\net10.0-windows\runtimes\win-x64\native\e_sqlite3.dll)`
  - root-level `[e_sqlite3.dll](E:\CODE\ClassroomToolkit\src\ClassroomToolkit.App\bin\Debug\net10.0-windows\e_sqlite3.dll)` did not exist
- Test evidence:
  - focused tests passed: `15/15`
  - full tests passed: `3103/3103`
  - contract/invariant subset passed: `24/24`
  - hotspot passed

## Changes

- Added runtime-aware native dependency resolution in [StartupCompatibilityProbe.cs](E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Services\Compatibility\StartupCompatibilityProbe.cs)
  - root directory remains first choice
  - fallback probes standard `.NET` runtime native paths such as `runtimes/win-x64/native`
- Added explicit diagnostics entry to launcher bottom bar in [MainWindow.xaml](E:\CODE\ClassroomToolkit\src\ClassroomToolkit.App\MainWindow.xaml)
- Added direct diagnostics dialog handler in [MainWindow.Launcher.cs](E:\CODE\ClassroomToolkit\src\ClassroomToolkit.App\MainWindow.Launcher.cs)
- Added regression coverage:
  - [StartupCompatibilityProbeTests.cs](E:\CODE\ClassroomToolkit\tests\ClassroomToolkit.Tests\StartupCompatibilityProbeTests.cs)
  - [UiCopyContractTests.cs](E:\CODE\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\UiCopyContractTests.cs)

## Rollback

- Revert launcher diagnostics entry by removing `DiagnosticsButton` from [MainWindow.xaml](E:\CODE\ClassroomToolkit\src\ClassroomToolkit.App\MainWindow.xaml) and `OnDiagnosticsClick` from [MainWindow.Launcher.cs](E:\CODE\ClassroomToolkit\src\ClassroomToolkit.App\MainWindow.Launcher.cs)
- Revert runtime-aware probing by restoring root-only lookup in [StartupCompatibilityProbe.cs](E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Services\Compatibility\StartupCompatibilityProbe.cs)
- Remove corresponding regression tests if behavior is intentionally changed

# Backfill 2026-04-03
当前落点=BACKFILL-2026-04-03
风险等级=BACKFILL-2026-04-03
规则ID=BACKFILL-2026-04-03
回滚动作=BACKFILL-2026-04-03
目标归宿=BACKFILL-2026-04-03
迁移批次=BACKFILL-2026-04-03
验证证据=BACKFILL-2026-04-03
影响模块=BACKFILL-2026-04-03
执行命令=BACKFILL-2026-04-03
