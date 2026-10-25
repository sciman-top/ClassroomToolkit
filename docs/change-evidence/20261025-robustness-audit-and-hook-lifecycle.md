# 2026-10-25 稳健性审查、生命周期修复与治理收敛

## 目标与范围

- Goal：审查课堂主链及相关异常、持久化、异步与资源生命周期，修复有证据的缺陷，并在同一阶段收敛已确认的治理负担；保留既有配置和学生数据兼容性。
- Baseline：`a8ddac057153decb077fa8f42fb8212b297a8be4`，开始时工作区干净。
- 审查入口：App 启动/退出、点名加载与预加载、图片目录加载和 PDF 会话、墨迹存储/WAL、INI/JSON 设置、学生工作簿/SQLite 降级、键盘/WPS hook、演示控制、语音与日志。
- Minimum proof：具体缺陷回归、聚焦契约测试、standard 门禁（build → 非性能测试及核心契约 → hotspot）。
- Stop：本次已确认缺陷修复、受控启动/PowerPoint 共存验收通过且仓库门禁通过；不据此声称完整现场验收或所有潜在缺陷已被排除。
- 后续授权范围：删除纯转发 Policy，收敛脆弱源码形状测试，修正证据漂移和项目规则复核版本；不做全量重写、依赖升级或新增治理面。
- 两个独立审查子代理分别遭遇 429/服务高负载失败，未产生代码变更或有效审查结论；主代理串行接管，`fallback=serial`。

## 缺陷、依据与修复

| 问题 | 依据 | 修复 |
| --- | --- | --- |
| 长按期间命令被更换/移除，旧计时器仍持有元素且可调用新命令 | 新增两个回归在旧实现失败，pending timer 未清理 | 命令变更时停止计时器并重置手势状态 |
| 图片目录加载等待旧取消回调时，新请求的 CTS 已被清理 | 新回归在旧实现抛出 `ObjectDisposedException`，位置为 `nextThumbnailCts.Token` | await 前取得 token，续接后检查取消、关闭与请求编号，再操作 UI |
| INI 读取与保存采用不同分隔符优先级 | `profile:theme=dark` 读取成功，保存后读回出现 `KeyNotFoundException` | 读取、解析和原行替换统一使用原有的等号优先语义 |
| 注销漏掉仍在等待 StartAsync 的 hook 注册 | 使用假句柄延迟启动，注销后旧实现仍返回注册成功 | 注销在锁内递增注册代数；启动后校验和最终所有权转移均核对代数；失效注册解除回调并释放，失败释放保留重试所有权 |
| UI 允许的超大保留天数导致日期运算溢出 | `CleanupOldRecords(int.MaxValue)` 在旧实现抛出 `ArgumentOutOfRangeException` | 截止日期下限饱和至 DateTime.MinValue，保留现有记录 |
| 安全目录枚举将惰性序列带出异常边界 | 源码检查：ListPages、sidecar 枚举与清理中的 MoveNext 可在 try 外失败 | 在保护范围内取得目录快照；清理枚举失败记录诊断并降级返回 |
| 日志入队检查与队列释放存在竞态 | 源码检查：IsAddingCompleted 的访问在捕获范围外，可能与 Dispose 交错 | 将状态检查纳入既有异常隔离；沿用并发写入/释放回归验证 |
| 置顶测试替身是进程全局状态，可污染后台 UI 调用 | standard 曾出现调用次数 1→2；独立并发上下文回归确定性证明替身被串用（outer 次数 0） | 测试替身改为 AsyncLocal 作用域，正常调用使用原 native 默认适配器 |
| 绘图枚举解析接受未定义数字值，形状工具会进入不可绘制状态 | `Enum.TryParse("999")` 成功；工具栏因此将形状判为已启用，但 `CreateShape` 与 `BuildShapeGeometry` 对未定义值返回空 | 设置解析统一校验 `Enum.IsDefined`；形状菜单也拒绝未定义标签；保留有效名称和合法数字枚举值 |
| 外观对话框可将未定义主题标签回写到设置 | `Enum.TryParse("999")` 成功；`ThemeManager` 会视觉回退默认主题，但保存回调仍收到并写入 `999` | 对话框只把已定义的主题枚举交给回调，否则返回默认主题；WPF STA 回归覆盖该路径 |

其中前五项及测试替身问题有新增的旧实现失败回归；绘图枚举问题有 `999` 输入回归覆盖。目录枚举和日志边界加固依据控制流审查及现有回归，未模拟真实网络盘断开或确定性复现日志释放竞态。

## 治理负担收敛

| 问题 | 依据 | 修复 |
| --- | --- | --- |
| MainWindow 可见性、交互状态和去重间隔存在纯转发 Policy | `ShouldEnsureVisible(bool)` 原样返回入参；状态 Policy 只调用构造器；间隔 Policy 再转发 Windowing/共享公式 | 调用方直接判断/构造并使用共享公式；删除三个 Policy 和只测包装层的测试 |
| Interop 契约逐字断言命名空间、等价 enum overload 和私有实现 | 命名空间与 overload 由编译/运行语义约束；线程检查缺少可直接注入的故障边界，源码断言不提供更高层证明 | 只保留跨进程 hook DllImport 的 Unicode marshalling 契约 |
| Ink 不可访问枚举测试断言四段实现源码 | 测试没有证明实际降级，重构等价枚举也会误报 | 改为对 pages 目录设置当前用户 `ListDirectory` Deny，断言 `ListPages` 降级为空并在 finally 恢复 ACL |
| Release 契约大量断言脚本措辞和诊断变量 | 通道、数据隔离、完整性、锁定还原、版本戳等是稳定合同；变量名和日志措辞不是外部行为 | 收敛到安全、数据隔离和交付完整性不变量，删除实现形状断言 |
| 证据写集落后于累计工作区，且无依赖变更时重复扫描漏洞 | 工作区包含 FileSystem、组合根、图片管理协调器和架构测试等后续收敛；漏洞扫描不属于本切片最低证明 | 本证据改为覆盖累计工作区，Minimum proof 移除独立漏洞扫描；依赖变化时才跑 full |
| portable 根放在仓库 artifact 下时设置层被 solution root 抢占 | run01 真实进程启动后读取/保存 `%LOCALAPPDATA%\ClassroomToolkit\settings.json`，未使用受控 data 目录 | `ConfigurationService` 的 portable 判定改用真实 runtime base directory，新增 solution root 不覆盖 portable root 的回归 |

## Exact write set

生产源文件：

- `src/ClassroomToolkit.App/Behaviors/LongPressBehavior.cs`
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Loading.cs`
- `src/ClassroomToolkit.App/Ink/InkStorageService.cs`
- `src/ClassroomToolkit.App/Ink/InkPersistenceService.cs`
- `src/ClassroomToolkit.App/Windowing/WindowTopmostExecutor.cs`
- `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
- `src/ClassroomToolkit.App/Settings/AppSettingsService.Helpers.cs`
- `src/ClassroomToolkit.App/Dialogs/AppearanceDialog.xaml.cs`
- `src/ClassroomToolkit.Infra/Settings/IniSettingsStore.cs`
- `src/ClassroomToolkit.Infra/Logging/FileLoggerProvider.cs`
- `src/ClassroomToolkit.Services/Input/GlobalHookService.cs`

回归文件：

- `tests/ClassroomToolkit.Tests/App/LongPressBehaviorTests.cs`（新增）
- `tests/ClassroomToolkit.Tests/Photos/ImageManagerLoadLifecycleTests.cs`（新增）
- `tests/ClassroomToolkit.Tests/Infra/IniSettingsStoreTests.cs`
- `tests/ClassroomToolkit.Tests/Ink/InkStorageServiceTests.cs`
- `tests/ClassroomToolkit.Tests/Settings/AppSettingsServiceTests.cs`
- `tests/ClassroomToolkit.Tests/UI/ThemeContractTests.cs`
- `tests/ClassroomToolkit.Tests/GlobalHookServiceLifecycleContractTests.cs`
- `tests/ClassroomToolkit.Tests/Windowing/WindowTopmostExecutorTests.cs`

另新增本文档；未修改依赖、宿主配置、部署产物或真实课堂数据。

后续同一累计任务追加：

- 架构收敛：`src/ClassroomToolkit.FileSystem/` 新项目；`ClassroomToolkit.sln`；`src/ClassroomToolkit.App/Startup/AppCompositionRoot.cs`；`src/ClassroomToolkit.App/Photos/ImageManagerWindowCoordinator.cs`；`MainWindow.Photo.ImageManager.cs`、`MainWindow.RollCall.cs`、`MainWindow.xaml.cs`、`InkPersistenceCore.cs`、`StudentPhotoResolver.cs`、`JsonSettingsDocumentStoreAdapter.cs`、`StudentWorkbookStore.cs`；移除 Domain 内 `AtomicFileReplaceUtility.cs` 和两个 `MainWindowToggle*Policy`。
- 架构回归：`tests/ClassroomToolkit.Tests/App/AppCompositionRootTests.cs`、`tests/ClassroomToolkit.Tests/Photos/ImageManagerWindowCoordinatorTests.cs`、`tests/ClassroomToolkit.Tests/IO/`、`ArchitectureDependencyTests.cs`、`MainViewModelTests.cs`、`StudentPhotoResolverTests.cs`、`RollCallViewModelTimerStateTests.cs`、`BlockingSleepUsageContractTests.cs`。
- 治理收敛：`MainWindow.Lifecycle.cs`、`MainWindow.ZOrder.cs`；删除 `MainWindowVisibleChangedPolicy.cs`、`MainWindowOverlayInteractionStatePolicy.cs`、`MainWindowZOrderDedupIntervalPolicy.cs` 及对应三个测试；删除 `InteropAnalyzerContractTests.cs` 并新增 `Interop/InteropMarshallingContractTests.cs`；更新 `InkStorageServiceTests.cs`、`ReleaseArtifactContractTests.cs`、`ReleasePrepareDistributionContractTests.cs`、`ConfigurationService.cs`、`ConfigurationServiceTests.cs`、`docs/architecture/README.md`、`AGENTS.md`。
- 工程引用与锁：`ClassroomToolkit.App.csproj`、`ClassroomToolkit.Infra.csproj` 及 App/Infra/Tests 三份 `packages.lock.json` 因新项目引用更新；无第三方包版本变化。

## 验证

| 命令/阶段 | 退出码与结果 |
| --- | --- |
| 基线 `dotnet build ClassroomToolkit.sln -c Debug` | 0；0 warning / 0 error |
| 基线 `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter 'Gate!=Performance'` | 0；3124 passed / 0 failed / 0 skipped |
| 绘图枚举回归 `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~Load_ShouldFallbackForUndefinedNumericPaintEnumValues"` | 0；1 passed / 0 failed / 0 skipped |
| 治理聚焦回归 `dotnet test ... --filter "FullyQualifiedName~InteropMarshallingContractTests|FullyQualifiedName~InkStorageServiceTests|FullyQualifiedName~ReleaseArtifactContractTests|FullyQualifiedName~ReleasePrepareDistributionContractTests"` | 0；34 passed / 0 failed / 0 skipped |
| 隔离聚焦回归 `dotnet test ... --filter "FullyQualifiedName~ConfigurationServiceTests|FullyQualifiedName~StudentResourceLocatorTests"` | 0；26 passed / 0 failed / 0 skipped |
| MainWindow 聚焦回归 `dotnet test ... --no-build --filter "FullyQualifiedName~MainWindow"` | 0；11 passed / 0 failed / 0 skipped |
| 最终 `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug` | 0；ALL PASS；build 0 warning / 0 error；3130 passed / 0 failed / 0 skipped；hotspot PASS（1200 行预算） |
| `git diff --check` | 0 |

最终测试收据：`artifacts/evidence/tests/current/stable-tests-summary.json`，`profile=standard`、`filter=Gate!=Performance`、`dry_run=false`、`exit_code=0`、`duration_ms=8079`；治理收敛后 standard 门禁共 3130 项通过。

## 受控验收

run01 使用 `artifacts/acceptance/controlled-20261025-run01/` 的一次性 portable 目录、样本工作簿和 4 张样本图片启动真实 WPF 进程。该轮发现设置隔离缺陷：portable marker 在仓库 artifact 下时，`ConfigurationService` 先向上找到 solution root，导致设置读取/保存到 `%LOCALAPPDATA%\ClassroomToolkit\settings.json`。进程已停止；真实仓库 `data/students.xlsx` 的 LastWriteTime 未变化，受控后本机设置快照保留在 `artifacts/acceptance/controlled-20261025-run01/post-run01-local-settings.json`，但该次运行对本机 settings.json 的 geometry/timer 状态更新无法逐字段精确回滚。后续修复在 run03 重新验证。

run03 使用同一修复后 Release publish、仓库内 portable 目录和一次性样本数据验证：

- 启动后 `%LOCALAPPDATA%\ClassroomToolkit\settings.json` LastWriteTime 未变化；`run03/data/settings.json` 生成且 `ink_photo_root_path` 指向 `run03/data`。
- 非法 `theme=999` 被自动修复为 `MidnightTeal`；`auto_check_enabled=False` 保持。
- `startup-compatibility-latest.json`：`hasBlockingIssues=false`、`hasWarnings=false`、`issues=[]`。
- UI Automation 确认主窗口 `PaintButton`、`RollCallButton`、`ExitButton` 可用。
- 一次性 PowerPoint 单页演示进入全屏放映（`ShowState=1`、`SlideIndex=1`、`IsFullScreen=true`）；随后 ClassroomToolkit Paint overlay 全屏显示，应用进程 `Responding=true`。验收后只关闭该受控 presentation，PowerPoint 无剩余演示文档。

受控验收只推进到本次明确执行的场景，不等同完整现场清单。

中间门禁曾因数组返回类型触发 CA1859，修正具体返回类型后解决；另一次失败暴露上表中的测试隔离缺陷，修复后最终整套通过，未放宽断言或关闭并行门禁。

## 验收边界与回滚

- `repo_verified`：上述构建、聚焦契约、测试、核心契约和 hotspot 已通过；测试总数减少反映删除包装层/实现措辞断言，未删除行为保护。
- `controlled_accepted`：本机真实进程启动、portable 设置隔离、主 UI 冒烟、一次性样本名册加载和 PowerPoint 全屏共存已通过；不包含 WPS、实际 hook 翻页、触控/触笔、多屏投影、PDF 长链路、网络盘/杀软锁和长时间稳定性。
- `filesystem_projected / host_loaded`：本次无宿主投影或安装动作，`platform_na`；替代证据为本地构建输出和 WPF STA 回归。
- `live_accepted`：未完成真实教室中的触屏、投影、多屏、Office/WPS、音频设备或网络盘故障验收；假 hook 不证明真实系统 hook 安装及消息泵行为。
- WPS 为 `environment_na`：本机未发现 WPS COM 注册；替代验证为 PowerPoint 受控共存，不声明 WPS 行为。
- `gate_na`：本次无依赖变更和发布，未运行 release/full、依赖升级、漏洞审计或墙钟性能微基准；替代验证为 standard。
- 回滚仅撤销上列十一个生产源文件与六个已有测试文件的本任务 diff，并移除两份新增测试及本文档；若后续叠加其他工作，按 hunk 选择撤销，禁止整仓 reset。无需数据逆向迁移。
- 治理收敛回滚：恢复 `MainWindow.Lifecycle.cs`/`MainWindow.ZOrder.cs` 的直接调用为原包装调用，恢复三个 Policy 与三个包装测试；恢复 `InteropAnalyzerContractTests.cs` 与 Release/Ink 契约测试原文，并将 `AGENTS.md` 版本行回退。架构收敛按上列文件 hunk 回滚，禁止整仓 reset。无需数据逆向迁移。

## 追加：过度设计剪枝（同日后续）

在既有稳健性切片之上，按“删冗余、保行为”原则对纯转发层做了一轮收敛。动机来自结构审查：`XxxPolicy → XxxDecision(record) → XxxPlan/Executor → 薄包装` 的五段式在多数节点只有 1–2 个分支、且 Reason 枚举仅被测试断言读取。

| 收敛点 | 依据 | 处理 |
| --- | --- | --- |
| `OverlayNavigationFocus` 的 `Plan/PlanDecision/ActivateDecision/KeyboardFocusDecision` 与两个 Reason 枚举 | `src` 内除定义文件外零引用；生产只用 `ResolvePlanDecision` 一处 | 直接返回两个布尔判断；删 `SnapshotPolicy` 转发类 |
| `FloatingOwnerBindingReason` / `Decision` | Reason 与 Action 一一对应，纯冗余；`ResolveDecision` 只被 `.Action` 或测试读取 | 保留 `Resolve` 三值结果与两个 `Should*` |
| `SurfaceZOrderDecisionDedupIntervalPolicy` | 原样转发 `WindowingDedupPolicies.ResolveMs`，生产零调用 | 删除；间隔用例并入 `WindowingDedupPoliciesTests` |
| `RetouchThrottleDecision` 与 `ForegroundExplicitRetouchThrottleDecision` 两套同构结构 | 后者实现即调用前者再翻译枚举，两套 Reason 均只被测试读取 | 保留单一 `ResolveRetouchThrottle`；执行结果 Reason 改为诊断字符串 |
| `PhotoRightButtonDown/UpExecutionPlan`、`PhotoTitleBarDragZOrderPlan` | 前者是入参原样装箱；后者的四个布尔中两个恒为 false | 改 `bool` / 单值判定，调用点直接使用 |
| `PaintVisibilityTransitionPlan`（9 布尔） | 三个工厂方法实为写死常量组合，9 字段对调用方不可读 | 收为 `PaintVisibilityTransition` 五值枚举 + `ShouldNormalizeToolbarWindowState` |
| `LauncherVisibilityTransitionPlan`（7 布尔） | `RequestZOrderApply`/`ForceEnforceZOrder` 恒等于其余布尔之或 | 降为 5 个语义字段 + 两个派生属性 + 两个工厂方法 |
| `RollCall` 的 Transparency/StyleApply/HoverTimer 三组 Decision+Reason | 三组 Reason 在 `src` 内无读取方（无日志消费），仅测试断言 | 合并为三个布尔判定；`ShouldApplyStyle` 顺带修正为 `last != current`（null 视为未知需应用） |
| `FloatingTopmost` 的 Drift/ForceEnforce/Retouch/Apply 四组 Decision+Reason | 四个 Reason 在 `src` 内无读取方 | 收敛为 `HasDrift` / `ShouldForceEnforce` / `ShouldEnsureFloatingOnTransition` / `ShouldEnforceZOrder` |
| `LauncherBubble` 的 `VisibilityDecision` 与 `ResolveVisibleChangedApply` | 前者两字段同值；后者与 `ResolveZOrderApplyGate` 三层重复 | 合并为单一跳过原因函数；`ShouldApplyZOrder` 与 gate 共用 |
| 画笔预览常量 | `PreviewTailPointWindow=56`、`PreviewBaseRefreshStride=14` 在两个渲染器各定义一次 | 提取 `BrushPreviewDefaults` 作为单一源 |
| 邻页位图缓存裁剪 | 同一段 8 行裁剪逻辑在 `Photo.CrossPage.Helpers.cs` 重复两次 | 提取 `TrimNeighborImageCache` |

保留项（审查后判定不属于冗余）：`OverlayActivationRetouchReason` 被 `FormatRetouchSkipMessage` 真实日志消费；`LauncherBubbleVisibleChangedDedupReason` / `LauncherBubbleZOrderApplyGateReason` 同理由日志使用；`SafeActionExecutionExecutor` 与 Win32 执行器承载异常隔离与降级，属课堂可用性不变量。

验证（同一工作区，未放宽断言）：

| 命令 | 结果 |
| --- | --- |
| `dotnet build ClassroomToolkit.sln -c Debug` | 0 warning / 0 error |
| `dotnet test ... --no-build`（全量） | 3109 passed / 0 failed |
| `pwsh ... run-local-quality-gates.ps1 -Profile standard -Configuration Debug` | ALL PASS：build、非性能测试 3100 passed、hotspot PASS |

计数变化说明：测试总数由 3130 降到 3100，减少的是 `*_ShouldMapResolveDecision` 这类只验证包装层转调的用例与被删除结构的直测；每处收敛都以真实调用点的行为测试替代，未删除任何课堂路径保护。`OverlayNavigationFocusSnapshotPolicyTests` 因 `SnapshotPolicy` 删除而移除，其构造语义由 `MainWindow.Photo.cs` 的直接 `new` 覆盖。

## 追加：测试包装收敛与死包装清理（第三轮）

在上一轮生产剪枝后，继续收敛测试与包装层：

1. 删除 35 个只证明“包装方法转调内部 Resolve 方法”的用例，覆盖 SessionTransition、OverlayActivation、WindowExecution、Dedup、Interop retry、ZOrder request、Toolbar interaction、Floating coordination、Launcher、Launcher bubble、Image manager、Paint visibility、Photo overlay transition 和 Roll call windowing。所有详细分支用例保留。
2. 删除失去全部消费者的 21 个生产包装方法；其中 Launcher bubble 的两个布尔包装、Interop retry 的三个 retry 包装、Floating activation guard 的两个重载均为零生产调用。
3. CrossPage 仅删除 `CrossPageDisplayUpdatePolicies.ShouldAdmit` 这个零生产调用转发；未改动跨页墨迹同步、replay 队列、延迟刷新或 PDF 缓存行为。
4. `AppSettingsService.Helpers.cs` 审查后标记为“无推荐”：当前 `GetString/GetDouble/GetBool` 等辅助承载解析、回退和兼容语义，不是纯转发；泛型化会降低设置兼容链路可读性。

当前收口证据（输入冻结后运行）：

| 命令 | 退出码与结果 |
| --- | --- |
| `dotnet build ClassroomToolkit.sln -c Debug` | 0；0 warning / 0 error |
| `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build` | 0；3074 passed / 0 failed |
| `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug` | 0；ALL PASS；build 0 warning / 0 error；3065 passed / 0 failed；hotspot PASS |

测试数由 3100 降到 3065，差额与本轮删除的 35 个包装转调用例一致。本证据仍只声明 `repo_verified`：未执行真实课堂、触控/触笔、WPS、多屏投影或长时间课堂稳定性验收。回滚按本轮列出的生产包装与测试用例 hunk 撤销；禁止整仓 reset。
