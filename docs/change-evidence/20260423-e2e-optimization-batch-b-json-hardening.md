# 20260423-e2e-optimization-batch-b-json-hardening

## Meta
- issue_id: `e2e-opt-20260423-batch-b`
- clarification_mode: `direct_fix`
- risk_level: `low_to_medium`
- scope: `JSON settings load/save hardening + logger wait-handle resource cleanup + regression tests`

## R1-R8 Trace
- R1 (归宿): 保持 `settings.ini/settings.json` 语义、接口行为、数据兼容不变，仅增强异常与资源边界。
- R2 (小步闭环): 切片 A（JSON 防护）-> 定向测试 -> 切片 B（日志释放）-> 全链路复验。
- R3 (根因优先): 修复根因为“超大 JSON 资源耗尽风险 + 异步等待句柄未释放”。
- R4 (风险分级): JSON 防护为中低风险（异常文件会被拒绝覆盖）；日志释放为低风险（行为等价）。
- R6 (硬门禁): 按 `build -> test -> contract/invariant -> hotspot` 固定顺序执行并通过。
- R7 (兼容): 未改业务契约、未改外部接口、未改配置键值格式。
- R8 (可追溯): 记录命令、关键输出、风险与回滚。

## Baseline (Before Change)
1. `dotnet build ClassroomToolkit.sln -c Debug`
- exit: `0`
- duration: `29.22s`

2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- exit: `0`
- key_output: `Passed 3428/3428`
- duration: `19.88s`

3. `dotnet test ... --filter "...ArchitectureDependencyTests|...InteropHookLifecycleContractTests|...InteropHookEventDispatchContractTests|...GlobalHookServiceLifecycleContractTests|...CrossPageDisplayLifecycleContractTests"`
- exit: `0`
- key_output: `Passed 28/28`
- duration: `7.69s`

4. security scan
- `dotnet list ClassroomToolkit.sln package --vulnerable --include-transitive`
- key_output: `No vulnerable packages`

## Changes

### 1) JSON 设置存储链路防护（正确性/安全/性能）
- file: `src/ClassroomToolkit.Infra/Settings/JsonSettingsDocumentStoreAdapter.cs`
- 问题:
  - `Load` 采用 `ReadAllText + JsonDocument.Parse(string)`，对超大 JSON 缺少显式尺寸门控，存在内存/IO 放大风险。
  - `Save` 预检路径未对超限 JSON 做明确阻断策略。
- 修改:
  - 新增 `MaxSettingsFileBytes = 4MB`，在 `load/save-preflight/hash` 三条路径统一校验文件尺寸。
  - `Load` 改为 `File.OpenRead + JsonDocument.Parse(stream)`，减少一次大字符串分配。
  - 将 `InvalidDataException` 纳入 `ShouldBlockOverwriteAfterLoadFailure`，超限文件与损坏 JSON 一致地进入“拒绝覆盖”保护。
- 收益:
  - 安全性: 收敛配置文件资源耗尽面（恶意/污染超大文件）。
  - 稳定性: 超限快速失败，不进入重负载解析。
  - 性能: 正常加载路径去除 `ReadAllText` 大对象分配。
- 风险:
  - 对异常超大 `settings.json` 的行为从“尝试解析/覆盖”变为“拒绝覆盖”，需要人工清理后再写入。
- 回滚:
  - 回滚 `JsonSettingsDocumentStoreAdapter.cs` 到变更前版本。

### 2) FileLoggerProvider 资源释放收敛（稳定性）
- file: `src/ClassroomToolkit.Infra/Logging/FileLoggerProvider.cs`
- 问题:
  - `WaitTaskSafely` 使用 `Task.AsyncWaitHandle` 后未显式释放，存在句柄泄露风险。
- 修改:
  - `var waitHandle` 改为 `using var waitHandle`，确保等待句柄生命周期受控。
- 收益:
  - 长时间运行下减少句柄累计风险，提升关闭路径稳定性。
- 风险:
  - 低；仅资源释放语义增强，日志行为不变。
- 回滚:
  - 回滚 `FileLoggerProvider.cs` 对应一行改动。

### 3) 测试补强（回归/契约）
- file: `tests/ClassroomToolkit.Tests/JsonSettingsDocumentStoreAdapterTests.cs`
- 新增测试:
  - `Load_ShouldReturnEmpty_AndBlockOverwrite_WhenJsonExceedsSizeLimit`
  - `Save_ShouldThrow_WhenExistingJsonExceedsSizeLimit_WithoutPriorLoad`
- 收益:
  - 固化“超限 -> 读空 + 禁止覆盖”回归契约，防止后续重构回退。

## Verification (After Change)
1. 定向测试
- `dotnet test ... --filter "FullyQualifiedName~JsonSettingsDocumentStoreAdapterTests"`
- exit: `0`
- key_output: `Passed 14/14`

2. 定向回归（JSON + Logger）
- `dotnet test ... --filter "FullyQualifiedName~FileLoggerProviderTests|FullyQualifiedName~JsonSettingsDocumentStoreAdapterTests"`
- exit: `0`
- key_output: `Passed 33/33`

3. 硬门禁固定顺序
- `dotnet build ClassroomToolkit.sln -c Debug` -> `exit=0` (`2.42s`, warm)
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` -> `exit=0` (`3430/3430`, `11.34s`, warm)
- `dotnet test ...contract filter...` -> `exit=0` (`28/28`, `7.46s`)
- `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1` -> `exit=0`

4. 额外质量门
- `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug` -> `exit=0` (`ALL PASS`)
- `dotnet list ClassroomToolkit.sln package --vulnerable --include-transitive` -> `No vulnerable packages`

5. hotspot 人工复核
- reviewed files:
  - `src/ClassroomToolkit.Infra/Settings/JsonSettingsDocumentStoreAdapter.cs`
  - `src/ClassroomToolkit.Infra/Logging/FileLoggerProvider.cs`
  - `tests/ClassroomToolkit.Tests/JsonSettingsDocumentStoreAdapterTests.cs`
- conclusion: 未发现契约破坏、接口行为变化、数据格式不兼容。

## N/A Records
- type: `platform_na`
- reason: `codex status` 在非交互环境返回 `stdin is not a terminal`。
- alternative_verification: 使用 `codex --version`、`codex --help` 与全链路质量门结果替代。
- evidence_link: `docs/change-evidence/20260423-e2e-optimization-batch-b-json-hardening.md`
- expires_at: `2026-05-23`

## Release / Migration / Rollback
- migration:
  - 无数据结构迁移；运行时自动生效。
- rollback (file-level):
  - `src/ClassroomToolkit.Infra/Settings/JsonSettingsDocumentStoreAdapter.cs`
  - `src/ClassroomToolkit.Infra/Logging/FileLoggerProvider.cs`
  - `tests/ClassroomToolkit.Tests/JsonSettingsDocumentStoreAdapterTests.cs`
- rollback verify:
  - 重新执行 `build -> test -> contract/invariant -> hotspot`。
- risk tier:
  - `low`: logger wait-handle 释放。
  - `low_to_medium`: JSON 超限文件由容错读取改为硬保护（拒绝覆盖）。
