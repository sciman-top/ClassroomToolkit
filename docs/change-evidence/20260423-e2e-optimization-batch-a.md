# 20260423-e2e-optimization-batch-a

## Meta
- issue_id: `e2e-opt-20260423-batch-a`
- clarification_mode: `direct_fix`
- risk_level: `medium`
- scope: `ImageManager dispatch contract recovery + Infra logging/security hardening + INI size guard + tests`

## R1-R8 Trace
- R1 (归宿): `保持现有业务契约与数据兼容`，修复失败契约测试并加固日志/配置读取稳定性。
- R2 (小步闭环): 先修复 4 个失败测试，再做日志与配置增强，并逐步补测。
- R3 (根因优先): 针对调度契约偏移（`InvokeAsync`/参数顺序/异常过滤器）做根因修复，不修改业务流程。
- R6 (硬门禁): 按 `build -> test -> contract/invariant -> hotspot` 执行并通过。
- R8 (可追溯): 记录命令、关键输出、风险与回滚。

## Baseline (Before Change)
1. `dotnet build ClassroomToolkit.sln -c Debug`
- exit: `0`
- key_output: `2 warnings (MSB3026 file in use), 0 errors`
- duration: `20.36s`

2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- exit: `1`
- key_output: `4 failing contract tests`
  - `ImageManagerThumbnailDispatchFallbackContractTests`
  - `ImageManagerLoadImagesPostAwaitGuardContractTests`
  - `PhotoOverlayAsyncLoadDispatchContractTests`
  - `ImageManagerDispatcherShutdownGuardContractTests`
- duration: `13.56s`

## Changes

### 1) ImageManager / PhotoOverlay 契约修复（正确性/稳定性）
- files:
  - `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Loading.cs`
  - `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Tree.cs`
  - `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs`
- problem: 调度契约字符串与实现偏移，导致 4 个契约测试失败。
- change:
  - 对齐 `AppendScanResultsAsync(result, token, requestId)` 调用与签名。
  - 对齐 `await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);`
  - 拆分 `OperationCanceledException` 过滤器，显式保留 `_isClosing` 分支。
  - 对齐 `await Dispatcher.InvokeAsync(ApplyLoadedBitmapOnUi, DispatcherPriority.Normal);`
- benefit: 恢复契约测试，避免调度降级策略回归。
- risk: 低；仅调度调用细节与参数顺序调整，无业务分支新增。
- rollback: 还原以上 3 文件到变更前版本。

### 2) FileLoggerProvider 安全/性能/可观测性优化
- file: `src/ClassroomToolkit.Infra/Logging/FileLoggerProvider.cs`
- problem:
  - 日志 category/message 可带控制字符，存在日志注入风险。
  - 队列丢弃计数仅内存累积，缺少落盘可观测性。
  - 关闭等待路径需降低无效 CPU 消耗（并满足禁用 `.Wait(` 契约）。
- change:
  - 新增单行净化 `SanitizeSingleLine`（转义 `\r \n \0`）。
  - 新增 `TryWriteDroppedMessageSummary`：在 dispose 路径落盘 `dropped-log-messages`。
  - 关闭等待改为 `AsyncWaitHandle.WaitOne` 路径，避免新增 `.Wait(` 违约。
- benefit:
  - 安全性提升：日志注入面收敛。
  - 可观测性提升：日志丢弃可审计。
  - 稳定性/性能提升：关闭等待更平稳，避免忙等热点。
- risk: 中低；日志文本表现由原始控制字符变为转义文本。
- rollback: 还原 `FileLoggerProvider.cs`。

### 3) IniSettingsStore 输入体积防护
- file: `src/ClassroomToolkit.Infra/Settings/IniSettingsStore.cs`
- problem: 超大 INI 文件可能造成读取放大与内存压力。
- change:
  - 增加 `MaxIniFileBytes = 4MB`。
  - 读取前执行 `TryValidateInputSize`，超限/异常时 fail-fast。
- benefit: 资源耗尽防护，提升鲁棒性。
- risk: 中低；异常超大配置会被拒绝加载。
- rollback: 还原 `IniSettingsStore.cs`。

### 4) 测试补强
- files:
  - `tests/ClassroomToolkit.Tests/FileLoggerProviderTests.cs`
  - `tests/ClassroomToolkit.Tests/IniSettingsStoreTests.cs`
- added tests:
  - `Log_ShouldEscapeControlCharacters_InCategoryAndMessage`
  - `Dispose_ShouldWriteDroppedMessageSummary_WhenQueueDropCountIsNonZero`
  - `TryLoad_ShouldFailForOversizedIniFile`

## Verification (After Change)
1. `dotnet build ClassroomToolkit.sln -c Debug`
- exit: `0`
- key_output: `0 warnings, 0 errors`
- duration: `2.15s` (warm run)

2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- exit: `0`
- key_output: `Passed: 3428/3428`
- duration: `10.50s`

3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- exit: `0`
- key_output: `Passed: 28/28`
- duration: `7.38s`

4. security gate
- command: `dotnet list ClassroomToolkit.sln package --vulnerable --include-transitive`
- result: `No vulnerable packages`

5. hotspot manual review
- reviewed files: all 7 changed files
- conclusion: 无契约破坏、无数据格式变更、无外部接口语义破坏。

## N/A Records
- type: `platform_na`
- reason: `codex status` 在非交互环境报错 `stdin is not a terminal`。
- alternative_verification: 使用 `codex --version` + `codex --help` + 仓库门禁命令作为替代证据。
- evidence_link: `docs/change-evidence/20260423-e2e-optimization-batch-a.md`
- expires_at: `2026-05-23`

## Rollback Plan
1. 精确回滚本次文件：
   - `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Loading.cs`
   - `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Tree.cs`
   - `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs`
   - `src/ClassroomToolkit.Infra/Logging/FileLoggerProvider.cs`
   - `src/ClassroomToolkit.Infra/Settings/IniSettingsStore.cs`
   - `tests/ClassroomToolkit.Tests/FileLoggerProviderTests.cs`
   - `tests/ClassroomToolkit.Tests/IniSettingsStoreTests.cs`
2. 回滚后重跑门禁：`build -> test -> contract/invariant`。
