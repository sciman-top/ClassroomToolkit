# 2026-04-20 Configuration Default INI Contract

- 规则映射: R1/R2/R6/R8
- 风险等级: Low
- 当前落点: `src/ClassroomToolkit.App/Settings/ConfigurationService.cs`
- 目标归宿: 明确并锁定“`appsettings.json` 存在但未配置设置键时，文档存储默认走 INI”这一兼容契约，避免后续无意变更。

## 变更依据

- `ResolveSettingsDocument()` 当前实现默认 `configuredFormat ?? SettingsDocumentFormat.Ini`。
- 该行为关系到旧配置兼容性，但此前缺少直接契约测试，且可读性依赖阅读实现细节。

## 实施变更

1. `tests/ClassroomToolkit.Tests/ConfigurationServiceTests.cs`
   - 新增 `Constructor_ShouldDefaultToIniDocument_WhenAppSettingsExistsWithoutSettingsKeys`。
   - 验证 `appsettings.json` 仅含无关配置（如 `Logging`）时：
     - `SettingsDocumentFormat = Ini`
     - `SettingsDocumentPath = <base>/settings.ini`

2. `src/ClassroomToolkit.App/Settings/ConfigurationService.cs`
   - 在 `configuredFormat ?? SettingsDocumentFormat.Ini` 处增加兼容性注释，显式声明该默认值是兼容约束而非偶然实现。

## 验证命令与结果

1. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~Constructor_ShouldDefaultToIniDocument_WhenAppSettingsExistsWithoutSettingsKeys"`
   - result: PASS
   - key_output: `Passed: 1, Failed: 0, Skipped: 0`

2. `dotnet build ClassroomToolkit.sln -c Debug`
   - result: PASS
   - key_output: `0 warnings, 0 errors`

3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - result: PASS
   - key_output: `Passed: 3345, Failed: 0, Skipped: 0`

4. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - result: PASS
   - key_output: `Passed: 28, Failed: 0, Skipped: 0`

5. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
   - result: PASS
   - key_output: `[hotspot] PASS - all .cs files within line budget (max=1200)`

## Hotspot 人工复核

- 复核对象: `ConfigurationService.cs`, `ConfigurationServiceTests.cs`
- 结论:
  - 仅新增契约测试与说明性注释，不改变运行行为。
  - 未触碰外部接口、数据格式、迁移流程与持久化结构。

## 回滚

- 回滚文件:
  - `src/ClassroomToolkit.App/Settings/ConfigurationService.cs`
  - `tests/ClassroomToolkit.Tests/ConfigurationServiceTests.cs`
  - `docs/change-evidence/20260420-configuration-default-ini-contract.md`
- 回滚动作: 还原上述文件后，按同序执行 `build -> test -> contract/invariant -> hotspot`。
