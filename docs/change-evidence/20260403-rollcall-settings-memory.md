# 20260403-rollcall-settings-memory

- Rule IDs: R1, R2, R6, R8
- Risk Level: Medium (settings persistence behavior change in roll-call window construction)
- Scope: `src/ClassroomToolkit.App/RollCallWindow.xaml.cs`, `tests/ClassroomToolkit.Tests/App/RollCallWindowSettingsReloadContractTests.cs`

## Evidence
- cmd: `codex status`
  - exit_code: `1`
  - key_output: `Error: stdin is not a terminal`
  - timestamp: `2026-04-03`
  - classification: `platform_na`
  - reason: non-interactive terminal cannot run status subcommand
  - alternative_verification: `codex --version`, `codex --help`
  - evidence_link: this file
  - expires_at: `2026-04-10`

- cmd: `codex --version`
  - exit_code: `0`
  - key_output: `codex-cli 0.118.0`

- cmd: `codex --help`
  - exit_code: `0`
  - key_output: help text displayed

- cmd: `dotnet build ClassroomToolkit.sln -c Debug`
  - exit_code: `0`
  - key_output: `0 Warning(s), 0 Error(s)`

- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - exit_code: `0`
  - key_output: `Passed: 3169, Failed: 0`

- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code: `0`
  - key_output: `Passed: 25, Failed: 0`

- cmd: `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
  - exit_code: `0`
  - key_output: `[hotspot] status=PASS`

## Rollback
- Revert the two changed files:
  - `git checkout -- src/ClassroomToolkit.App/RollCallWindow.xaml.cs`
  - `git checkout -- tests/ClassroomToolkit.Tests/App/RollCallWindowSettingsReloadContractTests.cs`
