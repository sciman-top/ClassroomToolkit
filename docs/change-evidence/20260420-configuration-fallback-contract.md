# 2026-04-20 Configuration Fallback Contract Clarification

- 规则映射: R1/R2/R6/R8
- 风险等级: Low
- 当前落点: `src/ClassroomToolkit.App/Settings/ConfigurationService.cs`
- 目标归宿: 在不改变配置行为、格式和兼容性的前提下，补充 `appsettings.json` 不可读场景契约测试，并修正与实现不一致的注释。

## 变更依据

- `ConfigurationService.ResolveSettingsDocument()` 在 `appsettings.json` 损坏/不可读时实际回退到默认 `settings.json`，但注释写成了“fallback to INI”。
- 该歧义会误导后续维护并提高误改风险。
- 现有测试覆盖了 malformed JSON，但未覆盖 `IOException`（文件被占用/不可读）路径。

## 实施变更

1. `tests/ClassroomToolkit.Tests/ConfigurationServiceTests.cs`
   - 新增 `Constructor_ShouldFallbackToDefault_WhenAppSettingsIsUnreadable`。
   - 用独占锁构造 `IOException`，验证回退契约保持为：
     - `SettingsIniPath = <base>/settings.ini`
     - `SettingsDocumentFormat = Json`
     - `SettingsDocumentPath = <base>/settings.json`

2. `src/ClassroomToolkit.App/Settings/ConfigurationService.cs`
   - 修正 `ResolveSettingsDocument()` 异常分支注释，使其与实现一致：
     - `JsonException/IOException/UnauthorizedAccessException` 都回退到默认 JSON 文档路径。

## 验证命令与结果

1. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~Constructor_ShouldFallbackToDefault_WhenAppSettingsIsUnreadable"`
   - result: PASS
   - key_output: `Passed: 1, Failed: 0, Skipped: 0`

2. `dotnet build ClassroomToolkit.sln -c Debug`
   - result: PASS
   - key_output: `0 warnings, 0 errors`

3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - result: PASS
   - key_output: `Passed: 3344, Failed: 0, Skipped: 0`

4. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - result: PASS
   - key_output: `Passed: 28, Failed: 0, Skipped: 0`

5. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
   - result: PASS
   - key_output: `[hotspot] PASS - all .cs files within line budget (max=1200)`

## Hotspot 人工复核

- 复核对象: `ConfigurationService.cs`, `ConfigurationServiceTests.cs`
- 结论:
  - 仅新增异常路径契约测试与注释修正，无行为变更。
  - 未触碰 `settings.ini`/`settings.json` 数据格式、外部接口和启动编排逻辑。
  - 回滚边界清晰。

## 回滚

- 回滚文件:
  - `src/ClassroomToolkit.App/Settings/ConfigurationService.cs`
  - `tests/ClassroomToolkit.Tests/ConfigurationServiceTests.cs`
  - `docs/change-evidence/20260420-configuration-fallback-contract.md`
- 回滚动作: 还原上述文件到变更前版本后，重新执行同一套 `build -> test -> contract/invariant -> hotspot`。
