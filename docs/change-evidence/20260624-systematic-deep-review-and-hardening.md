规则ID=R1,R2,R4,R6,R8,E4,E5
影响模块=src/ClassroomToolkit.App/App.xaml.cs; src/ClassroomToolkit.App/Photos/ImageManagerWindow.Lifecycle.cs; src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs; src/ClassroomToolkit.Services/Speech/SpeechService.cs; src/ClassroomToolkit.App/*.csproj; src/ClassroomToolkit.Infra/*.csproj; src/ClassroomToolkit.Services/*.csproj; tests/ClassroomToolkit.Tests; scripts/quality/analyzer-backlog-baseline.json; docs/tech-debt-backlog.md
当前落点=D:\CODE\ClassroomToolkit
目标归宿=在严格兼容前提下完成第二层深审第一批代码侧收口：dependency-governance 转绿、analysis baseline 收紧到当前事实、关键关闭/日志恢复路径补强并完成非人工门验证；人工课堂回归另行执行。
迁移批次=20260624-systematic-deep-review-and-hardening
风险等级=低到中。依赖变更仅限 patch 级稳定版本；代码变更集中在 dispatcher shutdown、初始化调度、日志保留失败恢复与语音通知回调隔离，不改用户可见语义与数据格式。

执行命令=
- preflight: `codex --version`
- preflight: `codex --help`
- baseline: `dotnet build ClassroomToolkit.sln -c Debug`
- baseline: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- baseline: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- baseline: `dotnet list ClassroomToolkit.sln package --vulnerable --include-transitive`
- baseline: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile full -Configuration Debug`
- targeted after changes: `dotnet build ClassroomToolkit.sln -c Debug`
- targeted after changes: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~SpeechServiceTests|FullyQualifiedName~SpeechServiceLifecycleContractTests|FullyQualifiedName~SpeechServiceNotificationTests|FullyQualifiedName~PhotoOverlayLoadFailureBranchContractTests|FullyQualifiedName~PhotoOverlayAsyncLoadDispatchContractTests|FullyQualifiedName~PhotoOverlayLoadedBitmapDispatchContractTests|FullyQualifiedName~PhotoOverlayEventCallbackSafetyContractTests|FullyQualifiedName~PhotoOverlayWindowLifecycleContractTests|FullyQualifiedName~ImageManagerWindowLifecycleContractTests|FullyQualifiedName~AppGlobalExceptionDialogDispatchContractTests|FullyQualifiedName~AppLogRetentionLifecycleContractTests|FullyQualifiedName~SettingsDocumentBootstrapMigrationExecutorTests"`
- targeted governance: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-dependency-upgrade-feasibility.ps1`
- targeted governance: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug`
- final verification: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validation/run-compatibility-preflight.ps1 -Configuration Debug`
- final verification: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile full -Configuration Debug`
- final verification: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validation/collect-settings-load-performance-samples.ps1`

发现与修复=
- 真实失败项只有一个：`run-local-quality-gates.ps1 -Profile full -Configuration Debug` 中的 `dependency-governance` 因多个未豁免 patch 级过期包失败。
- 升级了以下 patch 级稳定依赖并让对应 lockfile 同步：
  - `Microsoft.Extensions.DependencyInjection` `10.0.8 -> 10.0.9`
  - `Microsoft.Extensions.Logging` `10.0.8 -> 10.0.9`
  - `Microsoft.Extensions.Logging.Console` `10.0.8 -> 10.0.9`
  - `System.Speech` `10.0.8 -> 10.0.9`
  - `Microsoft.Data.Sqlite` `10.0.8 -> 10.0.9`
  - `System.IO.Packaging` `10.0.8 -> 10.0.9`
  - `Microsoft.Bcl.AsyncInterfaces` `10.0.8 -> 10.0.9`
  - `Microsoft.NET.Test.Sdk` `18.5.1 -> 18.7.0`
  - `coverlet.collector` `10.0.0 -> 10.0.1`
- `App.TryApplyErrorLogRetention` 原实现一旦第一次失败，会因为 `_errorLogRetentionApplied` 一直置位而不再重试。本轮改为：
  - 用 `_errorLogRetentionSucceeded` 代表“已成功完成并可跳过后续执行”
  - `finally` 中释放 `_errorLogRetentionApplied`，让一次性 I/O 或锁争用失败不会永久锁死日志保留清理
- `ImageManagerWindow.OnWindowLoaded` 原先直接 fire-and-forget `InitializeTreeAsync`。本轮改为通过 `SafeTaskRunner` 调度，统一异常隔离与取消语义。
- `PhotoOverlayWindow` 增加了三处关闭态保护：
  - `CloseOverlay()` 在 dispatcher 已关闭时直接 no-op
  - deferred z-order retouch callback 内再次判断 dispatcher shutdown，避免窗口已销毁后继续 retouch
  - deferred mask hide 在 dispatcher 已关闭时不再走 inline fallback，避免关闭尾声继续触碰 UI
- `SpeechService` 抽取 `NotifySpeechUnavailable()`，保留原行为，同时新增可直接验证的测试入口，证明“recoverable callback 不阻断其他订阅者，fatal callback 继续抛出”。
- `scripts/quality/analyzer-backlog-baseline.json` 从历史旧基线 `84 / CA1515` 收紧到当前真实扫描结果 `0`，与现有 analyzer report 和历史 `20260503` 之后的事实对齐，避免门禁过宽。
- 对 `compatibility-preflight` 中曾出现的一次性 `InkPersistenceServiceTests.SaveEmptyPage_ShouldOnlyRemoveThatPage_AndKeepOtherPages` / `File.Replace` 异常做了根因复核：
  - 单独重放该用例：通过。
  - `InkPersistenceServiceTests` 子集重跑：通过，22/22。
  - 串行 `run-compatibility-preflight.ps1 -Configuration Debug` 复跑：通过。
  - 结论：当前没有足够证据把 `IOException` 一概纳入 `AtomicReplaceFallbackPolicy`；为避免投机式放宽回退条件，本轮不修改原子替换策略，只把该信号登记为待再次复现时继续取证的残余风险。

验证证据=
- `dotnet build ClassroomToolkit.sln -c Debug`：PASS，0 warning / 0 error。
- 定向测试：PASS，26/26。
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`：PASS，3544/3544。
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`：PASS，29/29。
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validation/run-compatibility-preflight.ps1 -Configuration Debug`：PASS。
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile full -Configuration Debug`：PASS。
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validation/collect-settings-load-performance-samples.ps1`：PASS，产物位于 `artifacts/validation/settings-load-performance-*.json|md`。
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-dependency-upgrade-feasibility.ps1`：PASS，仅剩 active waiver 覆盖项：
  - `SixLabors.Fonts 2.1.3 -> 3.0.0`
  - `Microsoft.ApplicationInsights 2.23.0 -> 3.1.2`
  - `Microsoft.Testing.Extensions.Telemetry 1.9.1 -> 2.2.3`
  - `Microsoft.Testing.Extensions.TrxReport.Abstractions 1.9.1 -> 2.2.3`
  - `Microsoft.Testing.Platform 1.9.1 -> 2.2.3`
  - `Microsoft.Testing.Platform.MSBuild 1.9.1 -> 2.2.3`
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug`：PASS，`diagnostics_total=0`。
- `artifacts/quality/analyzer-backlog-report.json` 当前内容与 baseline 对齐：`project_counts=[]`、`rule_counts=[]`、`diagnostics=[]`。

残余风险=
- 本轮没有继续做大规模 UI code-behind 拆分或 Interop 重组，因为当前未发现新的、可被现有证据稳定证明的高价值缺陷；继续扩改容易放大验证面。
- `SixLabors.Fonts` 和 MTP 相关 transitive major 更新仍在 waiver 下，后续若要处理必须单独做视觉/测试平台兼容批次。
- 原子替换链曾出现过一次未稳定复现的 `File.Replace` 删除目标文件失败信号；当前序列化复测均通过，因此不对回退策略做猜测式放宽。若后续再次出现，下一轮必须补充 `HResult / Message / 句柄占用工况` 证据后再决定是否扩大 fallback。
- `docs/validation/manual-final-regression-checklist.md` 与 fullscreen 切换矩阵所要求的人工课堂场景验证，本轮未在真实双屏 / 投影 / Office-WPS 环境执行；因此“代码与自动化门禁已收口”不等同于“人工课堂门已通过”。

回滚动作=
- 依赖回滚：还原以下文件即可回到变更前版本集合：
  - `src/ClassroomToolkit.App/ClassroomToolkit.App.csproj`
  - `src/ClassroomToolkit.Infra/ClassroomToolkit.Infra.csproj`
  - `src/ClassroomToolkit.Services/ClassroomToolkit.Services.csproj`
  - `tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`
  - 对应 `packages.lock.json`
- 生命周期/日志回滚：
  - `src/ClassroomToolkit.App/App.xaml.cs`
  - `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Lifecycle.cs`
  - `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs`
  - `src/ClassroomToolkit.Services/Speech/SpeechService.cs`
  - 新增测试文件 4 个
- 治理基线回滚：
  - `scripts/quality/analyzer-backlog-baseline.json`
  - `docs/tech-debt-backlog.md`
  - 本证据文件
- 回滚后必须重新执行：
  - `dotnet build ClassroomToolkit.sln -c Debug`
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-dependency-upgrade-feasibility.ps1`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug`
