# 2026-04-22 INI settings null-section guard

## Scope
- issue_id: `ini-settings-null-section-guard`
- risk_level: `low`
- boundary:
  - 修改 `IniSettingsStore` 的写入边界处理，不改变配置键、文件路径、数据格式语义。
  - 新增单元测试覆盖 `null section` 输入场景。

## Basis
- 代码事实：`src/ClassroomToolkit.Infra/Settings/IniSettingsStore.cs` 的 `Save` 在遍历 `section.Value` 时未处理 `null`，存在 `NullReferenceException` 风险。
- 对照事实：`JsonSettingsDocumentStoreAdapter` 已对 `null` section 进行空字典兜底，INI 分支缺失同级防护。

## Changes
1. `src/ClassroomToolkit.Infra/Settings/IniSettingsStore.cs`
   - 在 `Save` 中将 `section.Value == null` 兜底为空字典后再遍历，避免 NRE。
2. `tests/ClassroomToolkit.Tests/IniSettingsStoreTests.cs`
   - 新增 `Save_ShouldTreatNullSectionDictionary_AsEmptySection` 回归测试。

## Commands and Evidence
- `codex --version`
  - exit_code: `0`
  - key_output: `codex-cli 0.122.0`
- `codex status`
  - exit_code: `1`
  - key_output: `Error: stdin is not a terminal`
- `dotnet build ClassroomToolkit.sln -c Debug`
  - exit_code: `0`
  - key_output: `0 errors, 0 warnings`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - exit_code: `0`
  - key_output: `Passed: 3402`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code: `0`
  - key_output: `Passed: 28`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
  - exit_code: `0`
  - key_output: `[hotspot] PASS - all .cs files within line budget (max=1200)`

## N/A Record
- type: `platform_na`
- reason: `codex status` 在当前非交互会话中不可用（stdin 非 TTY）。
- alternative_verification: 已执行 `codex --version` 与 `codex --help`（命令可用，CLI 正常工作）。
- evidence_link: `docs/change-evidence/20260422-ini-settings-null-section-guard.md`
- expires_at: `2026-06-30`

## Rollback
- `git checkout -- src/ClassroomToolkit.Infra/Settings/IniSettingsStore.cs tests/ClassroomToolkit.Tests/IniSettingsStoreTests.cs`
