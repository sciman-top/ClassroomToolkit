# 终态最佳架构进度映射（M1-M5）

最后更新：2026-03-15  
状态：active  
对应主方案：`docs/plans/2026-03-06-best-target-architecture-plan.md`

## 1. 总体进度（唯一口径）

- 全项目终态重构总进度：`100%`（代码与自动化范围）。
- 当前阶段：自动化冻结复检已通过，等待人工最终回归。
- 进度解释：方向已固定、主干已成形，自动化验证闭环完成；人工 live 回归被显式跳过，不再作为本次代码收口阻塞项。

说明：

- 总进度只作为总体体感，不作为冻结放行依据。
- 本次收口按“子系统退出条件 + 自动化验证”裁决；人工回归已按用户指令跳过并保留风险提示。

## 2. 里程碑状态

- M1 运行时协调统一：`100%`（代码与自动化范围）
- M2 高风险主链收口：`100%`（代码与自动化范围）
- M3 存储与边界完成：`100%`（代码与自动化范围）
- M4 Ink 抽象完成：`100%`（代码与自动化范围）
- M5 最终验收冻结：`100%`（代码与自动化范围）

## 3. 已锁定完成的主干

- `.NET 10` 已统一落地。
- 配置默认主路径已切换到 JSON，保留 INI 自动迁移与兜底。
- `RollCallSqliteStoreAdapter` 已从桥接占位升级为真实 SQLite 落库，并具备双表事务快照与 bridge 失败回读兜底。
- 已确定 RollCall 之后的下一 SQLite 业务域为学生名册（student workbook / class roster），并已完成“适配器落地 -> 运行时接线 -> 迁移与回退收尾”三步闭环。
- 学生名册 SQLite 运行时接线已落地：`RollCallWorkbookStoreResolver` 的 SQLite 分支已切到 `StudentWorkbookSqliteRollCallStoreAdapter`（包装 `StudentWorkbookSqliteStoreAdapter`），并补充启动期后端选择 guardrail 日志；`StudentWorkbookSqliteStoreAdapterTests.LoadOrCreate_ShouldFallbackToSqliteSnapshot_WhenBridgeThrows` 已覆盖 bridge 异常时的 SQLite 快照回读兜底。
- `PaintOverlayWindow -> PaintWindowOrchestrator -> MainWindow` 的部分前台演示链路已切到应用内枚举，缩小 Interop 类型暴露面。
- Z-Order、SessionTransition、CrossPage 请求准入已逐步升级为“决策对象 + 原因码 + 诊断日志”模式。
- Ink 抽象已形成 CPU 保底 + GPU 可选入口的基本骨架。
- Ink capability/fallback guardrail 已完成自动化闭环：`InkRendererFactoryResolver` 以 `CTOOLKIT_USE_GPU_INK_RENDERER + CTOOLKIT_ENABLE_EXPERIMENTAL_GPU_INK` 双门控解析后端，GPU 不可用时稳定回退 CPU；定向过滤 `FullyQualifiedName~Ink|FullyQualifiedName~Renderer|FullyQualifiedName~Brush` 已通过（`252/252`）。
- `RollCallWindow.Input.cs` 的 Interop 直连已收口到点名遥控 hook 协调边界（`RollCallRemoteHookCoordinator`）。
- `MainWindow.Launcher.cs` 新增三项可测策略收口：`LauncherWorkAreaClampPolicy`、`LauncherAutoExitTimerPlanPolicy`、`StartupDiagnosticsGatePolicy`。
- `MainWindow.xaml.cs` 新增三项可测策略收口：`MainWindowExitPlanPolicy`、`InkCleanupCandidateDirectoryPolicy`、`LauncherTopmostVisibilityTimestampPolicy`。
- `MainWindow.xaml.cs` 继续收口运行时分支：`LauncherWindowRuntimeSelectionLogPolicy` 替换内联 fallback 日志条件判断。
- `MainWindow.Paint.cs` 新增四项可测策略收口：`PaintWindowEnsureSkipPolicy`、`PaintWindowCreationPolicy`、`SessionTransitionViolationLogPolicy`、`PhotoCloseOwnerDetachmentPolicy`。
- `MainWindow.Paint.cs` 的 toolbar direct-repair 调度尾项已继续收口：`ToolbarInteractionDirectRepairExecutionCoordinator` 接管后台调度、rerun 与 failure-plan 协调，减少热点文件中的过程式编排。
- `MainWindow.Photo.cs` 的 image-manager state-change transition 尾项已继续收口：`ImageManagerStateChangeTransitionCoordinator` 接管 overlay normalize 调度/同步 fallback 与 surface apply 协调，减少热点文件中的窗口状态恢复流程。
- `MainWindow.Photo.cs` 的 foreground retouch 尾项已继续收口：`ForegroundSurfaceRetouchCoordinator` 接管 overlay-activation retouch 与 explicit-foreground retouch 的节流、状态写回与最终 apply 协调，减少热点文件中的前台补触流程。
- `MainWindow.Photo.cs` 的 image-manager owner-sync 与 open/close transition 尾项已继续收口：`ImageManagerVisibilityTransitionCoordinator` 接管 image-manager open/close transition 的 owner/show/close/surface 协调，`PhotoModeTransitionCoordinator` 接管 photo-mode change 下的 image-manager suppress、toolbar state、owner-sync 与 surface 协调。
- `MainWindow.Photo.cs` 新增六项可测策略收口：`PhotoShowInkOverlayChangePolicy`、`PhotoCursorModeFocusPolicy`、`PhotoModeOwnerSyncPolicy`、`PhotoUnifiedTransformChangePolicy`、`ImageManagerOpenSurfaceApplyPolicy`、`ImageManagerStateChangeSurfaceApplyPolicy`。
- `PaintOverlayWindow.Presentation.cs` 新增三项共享策略收口：`PresentationFocusMonitorActivationPolicy`、`PresentationNavigationAdmissionPolicy`、`PresentationKeyboardDispatchPolicy`，并合并 WPS/Office 导航准入重复分支。
- `PaintOverlayWindow.Presentation.cs` 继续新增三项主链策略收口：`PresentationChannelAvailabilityPolicy`、`PresentationFullscreenTypeResolutionPolicy`、`WpsHookEnableGatePolicy`，并替换焦点恢复/前台检测/WPS hook 启用门控的内联分支。
- `PaintOverlayWindow.Presentation.cs` 的 Overlay presentation tail 已通过 `PresentationFocusRestorePolicy` + `OverlayPresentationCommandRouter` 接线验证收口；现阶段按“已完成待后续上层场景联调”处理，不再作为独立热点尾任务保留。
- `PaintOverlayWindow.xaml.cs` 新增六项策略收口：`InkShowUpdateTransitionPolicy`、`OverlayTopmostApplyGatePolicy`、`OverlayFocusResolverGatePolicy`、`WpsRawFallbackTargetPolicy`、`InkCacheUpdateTransitionPolicy`、`InkSaveUpdateTransitionPolicy`，并拆分焦点阻塞目标解析子流程。
- `PaintOverlayWindow.xaml.cs` 继续新增三项策略收口：`PhotoTransformMemoryTogglePolicy`、`PhotoUnifiedTransformApplyPolicy`、`DispatcherInvokeAvailabilityPolicy`，并替换对应内联分支。
- `PaintOverlayWindow.xaml.cs` 的 cross-page display toggle 尾项已继续收口：`CrossPageDisplayToggleTransitionCoordinator` 接管跨页显示切换中的标志更新、统一变换恢复/保存、邻页清理、图片源刷新与 PDF ink cache reload 协调，减少 overlay shell 中的过程式运行时编排。
- `PaintOverlayWindow.xaml.cs` 的 ink-show transition 尾项已继续收口：`InkShowTransitionCoordinator` 接管显示笔迹开关中的设置变更、隐藏时清空/缓存清理、显示时当前页加载与跨页刷新请求协调，减少 overlay shell 中的过程式开关编排。
- `PaintOverlayWindow.Photo.cs` 的 ink page-load 尾项已继续收口：`InkPageLoadCoordinator` 接管当前页笔迹加载中的 cache-scope 判断、显示状态判断、cache-hit、sidecar-fallback 与 clear-state 分支协调，减少 photo shell 中的入口过程式分支。
- `PaintOverlayWindow.Photo.cs` 的 ink stroke-apply 尾项已继续收口：`InkStrokeApplyCoordinator` 接管当前页笔迹应用中的运行时集合更新、fast-path vs redraw 分支，以及 loaded-state/perf 记账协调，减少 photo shell 中的应用过程式分支。
- `PaintOverlayWindow.Photo.CrossPage.cs` 的 deferred refresh 尾项已继续收口：`CrossPageDeferredRefreshCoordinator` 接管 post-input delay gate、延迟分发、单次 pointer-up 去重与失败恢复协调，减少 cross-page shell 中的调度过程式分支。
- `PaintOverlayWindow.Photo.CrossPage.cs` 的 post-input refresh-slot 尾项已继续收口：`CrossPagePostInputRefreshSlotCoordinator` 接管 pointer-up sequence 的 CAS acquire 语义（unset/matched/success/retry），减少 cross-page shell 中的并发状态写回分支。
- `PaintOverlayWindow.Photo.CrossPage.cs` 的 missing-neighbor refresh 尾项已继续收口：`CrossPageMissingNeighborRefreshCoordinator` 接管缺失邻页的 policy 判定、延迟分发与失败恢复协调，减少 cross-page shell 中的邻页缺失刷新调度分支。
- `PaintOverlayWindow.Photo.CrossPage.cs` 的 replay dispatch 尾项已继续收口：`CrossPageReplayDispatchCoordinator` 接管 replay 分发的调度、inline fallback 与失败 requeue 协调，减少 cross-page shell 中的 replay dispatch 过程式分支。
- `PaintOverlayWindow.Photo.CrossPage.cs` 的 replay flush 尾项已继续收口：`CrossPageReplayFlushCoordinator` 接管 replay flush 的 gate 判定与 dispatch-target 选择协调，减少 cross-page shell 中的 flush 入口过程式分支。
- `PaintOverlayWindow.Photo.CrossPage.cs` 的 dispatch-failure 尾项已继续收口：`CrossPageDisplayUpdateDispatchFailureCoordinator` 接管分发失败下的 inline fallback、replay queue 与 replay flush 协调，减少 cross-page shell 中的 dispatch-failure 过程式分支。
- `PaintOverlayWindow.Input.cs` 新增两项输入路由策略收口：`OverlayInputRoutingPolicy`、`OverlayPointerSourceGatePolicy`，统一键盘/滚轮路由与鼠标来源门控。
- `PaintOverlayWindow.Input.cs` 继续新增 `CrossPagePointerUpExecutionPlanPolicy`，聚合 pointer-up 后置动作（追踪/刷新/回放/延迟刷新来源/ink context 刷新）执行计划。
- `PaintOverlayWindow.Input.cs` 继续完成触笔时间戳状态收口：新增 `StylusSampleTimestampState`、`StylusSampleTimestampPolicy`、`StylusSampleTimestampStateUpdater`，将批次跨度估算、单调递增、状态记忆写回统一为单一状态对象路径。
- `PaintOverlayWindow.Input.cs` 继续完成输入来源门控聚合：新增统一入口 `ShouldContinuePointerInput`，收口鼠标/右键/触笔 begin-pan 多处重复来源门控分支，降低后续行为漂移风险。
- `PaintOverlayWindow.Input.cs` 继续将来源门控执行语义外提为可测策略：新增 `OverlayPointerSourceHandlingPolicy` 与 `OverlayPointerSourceHandlingPlan`，统一 Continue/Consume/Ignore 的“继续/标记已处理/隐藏橡皮预览”动作决策。
- `PaintOverlayWindow.Input.cs` 继续完成事件执行计划收口：`StylusPhotoPanExecutionPolicy` 接管触笔 photo-pan 的执行动作判定，`PhotoManipulationEventHandlingPolicy` 接管 manipulation 事件的 Handle/Consume/Ignore 处置语义，进一步压缩事件处理器内联控制流。
- `PaintOverlayWindow.Input.cs` 继续完成 manipulation 准入流程聚合：新增 `TryAdmitPhotoManipulation` 统一 `OnManipulationStarting/OnManipulationDelta` 的准入判定与 `Handled` 标记路径。
- `PaintOverlayWindow.Input.cs` 继续完成跨页输入切页准入收口：新增 `CrossPageInputSwitchAdmissionPolicy`，将 gate/bitmap 可用性/指针滞回判定整合为统一准入决策，减少 `TrySwitchActiveImagePageForInput` 的内联前置分支。
- `PaintOverlayWindow.Input.cs` 继续完成鼠标 photo-pan 执行计划收口：新增 `PhotoPanMouseExecutionPolicy`，将 move/end 的 PassThrough/Update/End 执行动作统一为可测计划，减少处理器内联动作分支。
- `PaintOverlayWindow.Input.cs` 继续完成跨页输入目标页决策收口：新增 `CrossPageInputSwitchTargetPolicy`，将“仅允许邻页切换”的目标页钳制逻辑从窗口文件内联方法外提为可测策略。
- `PaintOverlayWindow.Input.cs` 继续完成 photo-pan 允许性共享收口：鼠标与触笔路径改为共用 `ResolveShouldPanPhoto`，避免同类准入条件在多处漂移。
- `PaintOverlayWindow.Input.cs` 继续完成 manipulation 组合准入收口：新增 `PhotoManipulationAdmissionPolicy`，将 `Routing + EventHandling` 的组合决策从事件处理器中上收。
- `PaintOverlayWindow.Input.cs` 继续清理失效私有入口：移除不再被调用的 manipulation 路由包装方法与参数空载的 photo-pan 活跃判断方法，降低主文件噪音。
- `PaintOverlayWindow.Input.cs` 继续完成右键链路策略化：新增 `PhotoRightClickPendingStateUpdater` 与 `PhotoRightButtonUpExecutionPolicy`，将 pending 状态变迁与 right-button-up 上下文菜单执行计划从事件处理器内联逻辑中上收。
- `PaintOverlayWindow.Input.cs` 继续完成右键 down 与 lost-capture 执行计划策略化：新增 `PhotoRightButtonDownExecutionPolicy`、`OverlayLostMouseCaptureExecutionPolicy`，并接线替换事件处理器中的内联动作分支。
- `PaintOverlayWindow.Input.cs` 继续统一右键事件链执行语义：`OnRightButtonDown` 改为按 down 执行计划驱动（arm pending + begin-pan 准入），`OnOverlayLostMouseCapture` 改为按 lost-capture 执行计划驱动（end-pan + pending clear）。
- `PaintOverlayWindow.Input.cs` 新增跨页输入切页请求策略 `CrossPageInputSwitchRequestPolicy` 与执行计划策略 `CrossPageInputSwitchExecutionPolicy`，将切页准入与执行模式分支从主链方法内联逻辑上收为可测策略。
- `PaintOverlayWindow.Input.cs` 新增输入工具分派策略 `PointerDownToolExecutionPolicy`、`PointerMoveToolExecutionPolicy`、`PointerUpToolExecutionPolicy`，统一 down/move/up 的工具动作分派与自适应渲染刷新计划，进一步压缩事件处理器内联 `switch` 分支。
- `PaintOverlayWindow.Input.cs` 新增跨页切页后续执行策略 `CrossPageInputResumePolicy`，将刷子续写/当前输入回放/橡皮恢复三类切页恢复动作改为可测执行计划。
- `PaintOverlayWindow.Input.cs` 新增手势增量执行策略 `PhotoManipulationDeltaExecutionPolicy`，将平移阈值、pan telemetry 与跨页刷新请求从 `OnManipulationDelta` 内联条件中上收。
- `PaintOverlayWindow.Input.cs` 新增可见页命中策略 `CrossPageCurrentPagePointerHitPolicy` 与 `CrossPageNeighborPageCandidatePolicy`，收口当前页/邻页命中判定分支，减少 `TryResolveVisibleImagePageFromPointer` 多重 `if/continue`。
- `PaintOverlayWindow.Input.cs` 新增触笔事件执行策略：`StylusDownExecutionPolicy`、`StylusMoveExecutionPolicy`、`StylusUpExecutionPolicy`，统一 down/move/up 三条路径在 photo-loading、photo-pan 处理、ink-operation、stylus-point 批次下的执行动作计划，进一步压缩事件处理器重复门控分支。
- `PaintOverlayWindow.Input.cs` 新增滚轮演示执行策略 `OverlayWheelPresentationExecutionPolicy`，收口 WPS hook block-only 绕行、hook 最近触发抑制、滚轮方向命令映射三类分支。
- `PaintOverlayWindow.Input.cs` 新增 pointer-up 后处理计划策略 `CrossPagePointerUpPostExecutionPolicy`，统一后置动作链（track / refresh / replay / trace-end / ink-context-refresh）的执行判定入口。
- `PaintOverlayWindow.Input.cs` 的 Overlay input tail 已通过输入/来源门控定向过滤测试与 `ArchitectureDependencyTests` 验证收口；现阶段按“已完成并可从执行图移交到 cross-page tail”处理，不再保留为独立未闭环热点。
- `MainWindow.xaml.cs` 继续完成主窗口生命周期小分支策略化：新增 `MainWindowVisibleChangedPolicy`、`MainWindowLoadedToggleActionPolicy`、`SettingsSaveFailureNotificationPolicy`，分别收口可见性确保、Loaded 首次 UI 动作选择、设置保存失败通知状态。
- `MainWindow.xaml.cs` 继续完成协调链路小分支策略化：新增 `MainWindowOverlayInteractionState` + `MainWindowOverlayInteractionStatePolicy`（替换匿名元组交互状态）、`FloatingDispatchQueueDecisionLogPolicy`（收口队列决策日志准入）、`LauncherWindowResolutionPolicy`（收口 Launcher bubble/main 回退判定）。
- `MainWindow.xaml.cs` 继续完成 Z-order 队列回调策略化：新增 `ZOrderQueueDispatchDecisionHandlingPolicy` 与 `ZOrderQueueDispatchFailureRollbackStatePolicy`，收口队列决策日志/失败标记/状态回滚选择三类内联逻辑。
- `MainWindow.xaml.cs` 继续完成 Z-order/对话框/Ink 清理小分支策略化：新增 `MainWindowZOrderDedupIntervalPolicy`（统一 surface/request 去重间隔决策）、`DialogShowResultStateUpdater`（对话框结果状态更新）、`InkStartupCleanupLogPolicy`（启动清理日志门控与消息格式）。
- `MainWindow.xaml.cs` 继续完成散点状态写回尾部收口：新增 `LauncherTopmostVisibilityStateUpdater`、`SettingsSaveFailureNotificationStateUpdater`、`ZOrderQueueDispatchFailureRollbackStateUpdater`，把 Launcher topmost 时间戳、设置保存失败通知标记、Z-order 队列失败回滚从热点主链中的直接赋值改为 updater 路径。
- `MainWindow.Paint.cs` 继续完成工具条直修调度分支收口：新增 `ToolbarInteractionDirectRepairDispatchFailurePlanPolicy`，统一 admission 拒绝/队列标记失败/调度失败三类分支下的 rerun 与状态清理动作。
- `MainWindow.xaml.cs` 继续完成关闭流程小分支策略化：新增 `MainWindowOnClosingPlanPolicy`，将 `OnClosing` 的取消关闭与触发退出决策上收为显式计划。

## 4. 当前硬指标

- App 层直接引用 `ClassroomToolkit.Interop` 的文件数：`6`
- 全量 Debug 测试：`2502/2502`（最新冻结复检，通过）
- 全量 Release 测试：`2502/2502`（最新冻结复检，通过）

### 4.1 当前结构现实

- `App` 仍直接依赖 `Application / Infra / Services / Interop`。
- `Services` 当前应视为“运行时能力实现层 / Application 端口实现外观层”，不再扩张为新的编排中心。
- `Application*Flow` 开关目前不作为可靠主链切换手段。

## 5. 剩余主要工作（按优先级）

### P0

1. 场景互切下窗口层级与激活抖动完全收敛  
覆盖图片 / PDF / 白板 / PPT-WPS 互切，以及启动器 / 工具条 / 点名窗口 / Overlay / ImageManager 的统一层级协调。

2. 高风险 UI 文件中的残余散点状态写入继续上收  
`MainWindow.Paint` 的 toolbar direct-repair 调度尾项与 `MainWindow.Photo` 的 image-manager state-change transition、foreground retouch、owner-sync/open-close transition 已闭环；`PaintOverlayWindow.xaml` 的 cross-page display toggle、ink-show transition，`PaintOverlayWindow.Photo` 的 ink page-load、ink stroke-apply，以及 `PaintOverlayWindow.Photo.CrossPage` 的 deferred refresh、post-input refresh-slot、missing-neighbor refresh、replay dispatch、replay flush、dispatch-failure 也已完成 coordinator 收口，当前主战场已从 `MainWindow.*` 切到 overlay shell 剩余尾项。

3. 跨页并发链路自动化尾部已闭环  
`CrossPage/Photo/Overlay/ArchitectureDependencyTests` 定向过滤已通过（`903/903`）；后续以冻结前人工场景回归核验为主。

### P1

1. Interop 直连面继续压缩到边界层  
当前 `6` 个 App 层直连文件中，Windowing Adapter 边界可保留，其余应继续收口。

2. 业务 SQLite 深化  
学生名册（student workbook / class roster）域已完成迁移/兼容回读/回滚口径闭环；下一历史向业务域 Ink 历史快照（ink stroke history snapshot，含按文档/页索引的笔迹历史）已完成 `adapter -> wiring -> migration/rollback` 三步闭环，并沿用同等迁移与回退要求。

3. Ink 抽象可选发布策略收口已完成（2026-03-13）  
CPU 主路径保持稳定可发布；GPU 路径维持双门控可选启用，探测失败或运行异常均回退 CPU，并已通过 `FullyQualifiedName~Ink|FullyQualifiedName~Renderer|FullyQualifiedName~Brush|FullyQualifiedName~Gpu` 定向验证（`259/259`）。

### P2

1. 文档、守卫、人工回归矩阵收尾  
用于支撑最终冻结验收与后续长期维护。

2. 历史 phase 文档与当前口径的引用关系继续去歧义  
保留历史记录，但避免后续接手时误用历史阶段文档。

## 6. 子系统退出条件

### 6.1 Windowing / Session

- `MainWindow.*`、`PaintOverlayWindow.*`、`RollCallWindow.*` 中不再新增散点状态写入。
- 关键窗口关系由 Session / Windowing 路径稳定接管。
- 置顶、焦点、激活抖动在人手回归下无新已知缺陷。

### 6.2 Presentation

- `PaintOverlayWindow` 不再继续加深对 `PresentationControlService` 的直接耦合。
- WPS / PPT 放映控制的主路径具备统一用例与降级口径。
- 相关定向测试与人工回归通过。

### 6.3 Storage

- JSON 默认主路径稳定。
- SQLite 不再停留在 RollCall 单点；学生名册（student workbook / class roster）域与下一历史域 Ink 历史快照（ink stroke history snapshot）均已完成适配器、运行时接线、兼容回读与回滚文档闭环。

### 6.4 Ink

- CPU 主路径稳定可发布。
- GPU 保持可选增强，不因能力探测失败影响主链。

### 6.5 Boundary Guard

- App -> Interop 基线不升高。
- `docs/architecture/2026-03-10-interop-direct-dependency-matrix.md` 与守卫白名单一致。

## 7. 当前最小执行切片

1. 继续清理 `PaintOverlayWindow.*`、`RollCallWindow.*` 中残余 Interop 直连与散点状态。
2. 继续把跨页刷新与输入链路的剩余散点上收到策略与状态更新器。
3. 在业务存储层推进 Ink 历史快照 SQLite 路径时，同步补齐迁移与回退测试。
4. 保持“小步收口 + 定向测试 + 全量测试”的节奏，不做无验证重写。
5. 清理已经失去执行意义的 `Application*Flow` 文档口径，避免把未接线开关写成回滚依赖。

## 8. 最近验证证据

- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests"`：通过（`5/5`，`automated-freeze-recheck-after-gap-closure`，2026-03-15）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`：通过（`2502/2502`，`automated-freeze-recheck-after-gap-closure`，2026-03-15）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release`：通过（`2502/2502`，`automated-freeze-recheck-after-gap-closure`，2026-03-15）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests"`：失败（`4/5`，`ArchitectureDependencyTests.AppLayer_ShouldAvoidInfraNamespace_OutsideCompositionRoot` 报告新增 App->Infra 直连：`InkHistoryPersistenceBridge.cs`、`PaintOverlayWindow.Export.cs`、`PaintWindowFactory.cs`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`：失败（`2226/2227`，同一 ArchitectureDependencyTests 守卫失败）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release`：失败（`2226/2227`，同一 ArchitectureDependencyTests 守卫失败）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`：通过（`2214/2214`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release`：通过（`2214/2214`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~Migration|FullyQualifiedName~SettingsDocument|FullyQualifiedName~Sqlite|FullyQualifiedName~Store"`：通过（`85/85`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests"`：通过（`5/5`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~LauncherWorkAreaClampPolicyTests"`：通过（`3/3`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~LauncherAutoExitTimerPlanPolicyTests"`：通过（`3/3`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~StartupDiagnosticsGatePolicyTests"`：通过（`3/3`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~MainWindowExitPlanPolicyTests|FullyQualifiedName~InkCleanupCandidateDirectoryPolicyTests|FullyQualifiedName~LauncherTopmostVisibilityTimestampPolicyTests"`：通过（`7/7`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~LauncherWindowRuntimeSelectionLogPolicyTests|FullyQualifiedName~MainWindowExitPlanPolicyTests|FullyQualifiedName~InkCleanupCandidateDirectoryPolicyTests|FullyQualifiedName~LauncherTopmostVisibilityTimestampPolicyTests"`：通过（`12/12`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PaintWindowEnsureSkipPolicyTests|FullyQualifiedName~PaintWindowCreationPolicyTests|FullyQualifiedName~SessionTransitionViolationLogPolicyTests|FullyQualifiedName~PhotoCloseOwnerDetachmentPolicyTests"`：通过（`12/12`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoShowInkOverlayChangePolicyTests|FullyQualifiedName~PhotoCursorModeFocusPolicyTests|FullyQualifiedName~PhotoModeOwnerSyncPolicyTests|FullyQualifiedName~PhotoUnifiedTransformChangePolicyTests|FullyQualifiedName~ImageManagerOpenSurfaceApplyPolicyTests|FullyQualifiedName~ImageManagerStateChangeSurfaceApplyPolicyTests"`：通过（`19/19`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PresentationFocusMonitorActivationPolicyTests|FullyQualifiedName~PresentationNavigationAdmissionPolicyTests|FullyQualifiedName~PresentationKeyboardDispatchPolicyTests|FullyQualifiedName~PresentationKeyCommandPolicyTests|FullyQualifiedName~OverlayPresentationCommandRouterTests"`：通过（`33/33`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PresentationChannelAvailabilityPolicyTests|FullyQualifiedName~PresentationFullscreenTypeResolutionPolicyTests|FullyQualifiedName~WpsHookEnableGatePolicyTests|FullyQualifiedName~PresentationFocusMonitorActivationPolicyTests|FullyQualifiedName~PresentationNavigationAdmissionPolicyTests|FullyQualifiedName~PresentationKeyboardDispatchPolicyTests|FullyQualifiedName~WpsHookInterceptPolicyTests|FullyQualifiedName~PresentationFocusRestorePolicyTests"`：通过（`41/41`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~Presentation|FullyQualifiedName~OverlayPresentationCommandRouterTests|FullyQualifiedName~ArchitectureDependencyTests"`：通过（`173/173`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~InkShowUpdateTransitionPolicyTests|FullyQualifiedName~OverlayTopmostApplyGatePolicyTests|FullyQualifiedName~OverlayFocusResolverGatePolicyTests|FullyQualifiedName~WpsRawFallbackTargetPolicyTests|FullyQualifiedName~InkCacheUpdateTransitionPolicyTests|FullyQualifiedName~InkSaveUpdateTransitionPolicyTests|FullyQualifiedName~OverlayFocusAcceptancePolicyTests|FullyQualifiedName~OverlayWindowStyleApplyPolicyTests"`：通过（`31/31`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~InkShowUpdateTransitionPolicyTests|FullyQualifiedName~OverlayTopmostApplyGatePolicyTests|FullyQualifiedName~OverlayFocusResolverGatePolicyTests|FullyQualifiedName~WpsRawFallbackTargetPolicyTests|FullyQualifiedName~InkCacheUpdateTransitionPolicyTests|FullyQualifiedName~InkSaveUpdateTransitionPolicyTests|FullyQualifiedName~PhotoTransformMemoryTogglePolicyTests|FullyQualifiedName~PhotoUnifiedTransformApplyPolicyTests|FullyQualifiedName~DispatcherInvokeAvailabilityPolicyTests|FullyQualifiedName~OverlayFocusAcceptancePolicyTests|FullyQualifiedName~OverlayWindowStyleApplyPolicyTests"`：通过（`41/41`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~OverlayInputRoutingPolicyTests|FullyQualifiedName~OverlayPointerSourceGatePolicyTests|FullyQualifiedName~PhotoManipulationRoutingPolicyTests|FullyQualifiedName~OverlayPresentationRoutingPolicyTests|FullyQualifiedName~WpsWheelRoutingPolicyTests"`：通过（`25/25`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPagePointerUpExecutionPlanPolicyTests|FullyQualifiedName~CrossPagePointerUpDecisionPolicyTests|FullyQualifiedName~CrossPagePointerUpDeferredStatePolicyTests|FullyQualifiedName~CrossPagePointerUpRefreshPolicyTests|FullyQualifiedName~OverlayInputRoutingPolicyTests|FullyQualifiedName~OverlayPointerSourceGatePolicyTests|FullyQualifiedName~PhotoManipulationRoutingPolicyTests|FullyQualifiedName~StylusPhotoPanRoutingPolicyTests"`：通过（`26/26`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~StylusSampleTimestampPolicyTests|FullyQualifiedName~StylusSampleTimestampStateUpdaterTests|FullyQualifiedName~StylusBatchTimingPolicyTests|FullyQualifiedName~StylusBatchDispatchPolicyTests"`：通过（`10/10`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~OverlayPointerSourceGatePolicyTests|FullyQualifiedName~OverlayInputRoutingPolicyTests|FullyQualifiedName~StylusPhotoPanRoutingPolicyTests|FullyQualifiedName~PhotoPanMouseMoveRoutingPolicyTests|FullyQualifiedName~PhotoPanTerminationPolicyTests|FullyQualifiedName~CrossPagePointerUpExecutionPlanPolicyTests"`：通过（`23/23`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~OverlayPointerSourceHandlingPolicyTests|FullyQualifiedName~OverlayPointerSourceGatePolicyTests|FullyQualifiedName~OverlayInputRoutingPolicyTests|FullyQualifiedName~StylusPhotoPanRoutingPolicyTests|FullyQualifiedName~PhotoPanMouseMoveRoutingPolicyTests|FullyQualifiedName~PhotoPanTerminationPolicyTests"`：通过（`24/24`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoInputAlignmentPolicyTests|FullyQualifiedName~OverlayPointerSourceHandlingPolicyTests|FullyQualifiedName~OverlayPointerSourceGatePolicyTests|FullyQualifiedName~StylusSampleTimestampPolicyTests|FullyQualifiedName~StylusSampleTimestampStateUpdaterTests|FullyQualifiedName~CrossPagePointerUpExecutionPlanPolicyTests"`：通过（`53/53`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPageInputSwitchAdmissionPolicyTests|FullyQualifiedName~CrossPageInputSwitchGatePolicyTests|FullyQualifiedName~CrossPageInputSwitchPolicyTests"`：通过（`12/12`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoPanMouseExecutionPolicyTests|FullyQualifiedName~PhotoPanMouseMoveRoutingPolicyTests|FullyQualifiedName~PhotoPanTerminationPolicyTests|FullyQualifiedName~PhotoInputAlignmentPolicyTests|FullyQualifiedName~CrossPageInputSwitchAdmissionPolicyTests"`：通过（`59/59`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPageInputSwitchTargetPolicyTests|FullyQualifiedName~CrossPageInputSwitchAdmissionPolicyTests|FullyQualifiedName~CrossPageInputSwitchGatePolicyTests|FullyQualifiedName~CrossPageInputSwitchPolicyTests|FullyQualifiedName~PhotoPanMouseExecutionPolicyTests|FullyQualifiedName~PhotoPanMouseMoveRoutingPolicyTests|FullyQualifiedName~PhotoPanTerminationPolicyTests|FullyQualifiedName~PhotoManipulationAdmissionPolicyTests|FullyQualifiedName~PhotoInputAlignmentPolicyTests|FullyQualifiedName~OverlayPointerSourceHandlingPolicyTests|FullyQualifiedName~StylusSampleTimestampPolicyTests|FullyQualifiedName~StylusSampleTimestampStateUpdaterTests|FullyQualifiedName~CrossPagePointerUpExecutionPlanPolicyTests"`：通过（`87/87`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests"`：通过（`5/5`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoManipulationAdmissionPolicyTests|FullyQualifiedName~PhotoInputAlignmentPolicyTests|FullyQualifiedName~PhotoPanMouseExecutionPolicyTests|FullyQualifiedName~CrossPageInputSwitchTargetPolicyTests|FullyQualifiedName~CrossPagePointerUpExecutionPlanPolicyTests"`：通过（`58/58`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoRightClickPendingStateUpdaterTests|FullyQualifiedName~PhotoRightButtonUpExecutionPolicyTests|FullyQualifiedName~PhotoRightClickContextMenuPolicyTests|FullyQualifiedName~PhotoPanMouseExecutionPolicyTests|FullyQualifiedName~PhotoPanMouseMoveRoutingPolicyTests|FullyQualifiedName~PhotoPanTerminationPolicyTests|FullyQualifiedName~PhotoManipulationAdmissionPolicyTests|FullyQualifiedName~PhotoInputAlignmentPolicyTests|FullyQualifiedName~CrossPageInputSwitchAdmissionPolicyTests|FullyQualifiedName~CrossPageInputSwitchTargetPolicyTests|FullyQualifiedName~OverlayPointerSourceHandlingPolicyTests|FullyQualifiedName~StylusSampleTimestampPolicyTests"`：通过（`84/84`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoRightButtonDownExecutionPolicyTests|FullyQualifiedName~OverlayLostMouseCaptureExecutionPolicyTests|FullyQualifiedName~PhotoRightClickPendingStateUpdaterTests|FullyQualifiedName~PhotoRightButtonUpExecutionPolicyTests|FullyQualifiedName~PhotoRightClickContextMenuPolicyTests|FullyQualifiedName~PhotoPanMouseExecutionPolicyTests|FullyQualifiedName~PhotoPanMouseMoveRoutingPolicyTests|FullyQualifiedName~PhotoPanTerminationPolicyTests|FullyQualifiedName~PhotoManipulationAdmissionPolicyTests|FullyQualifiedName~PhotoInputAlignmentPolicyTests|FullyQualifiedName~CrossPageInputSwitchAdmissionPolicyTests|FullyQualifiedName~CrossPageInputSwitchTargetPolicyTests|FullyQualifiedName~OverlayPointerSourceHandlingPolicyTests|FullyQualifiedName~StylusSampleTimestampPolicyTests|FullyQualifiedName~CrossPagePointerUpExecutionPlanPolicyTests"`：通过（`94/94`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoRightButtonDownExecutionPolicyTests|FullyQualifiedName~OverlayLostMouseCaptureExecutionPolicyTests|FullyQualifiedName~PhotoRightClickPendingStateUpdaterTests|FullyQualifiedName~PhotoRightButtonUpExecutionPolicyTests|FullyQualifiedName~PhotoRightClickContextMenuPolicyTests|FullyQualifiedName~PhotoPanMouseExecutionPolicyTests|FullyQualifiedName~PhotoInputAlignmentPolicyTests|FullyQualifiedName~CrossPageInputSwitchTargetPolicyTests|FullyQualifiedName~PhotoManipulationAdmissionPolicyTests"`：通过（`73/73`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~MainWindowVisibleChangedPolicyTests|FullyQualifiedName~MainWindowLoadedToggleActionPolicyTests|FullyQualifiedName~SettingsSaveFailureNotificationPolicyTests|FullyQualifiedName~MainWindowExitPlanPolicyTests|FullyQualifiedName~PhotoRightButtonDownExecutionPolicyTests|FullyQualifiedName~OverlayLostMouseCaptureExecutionPolicyTests|FullyQualifiedName~PhotoRightButtonUpExecutionPolicyTests|FullyQualifiedName~PhotoManipulationAdmissionPolicyTests|FullyQualifiedName~CrossPageInputSwitchTargetPolicyTests|FullyQualifiedName~StylusSampleTimestampPolicyTests"`：通过（`32/32`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~MainWindowOverlayInteractionStatePolicyTests|FullyQualifiedName~FloatingDispatchQueueDecisionLogPolicyTests|FullyQualifiedName~LauncherWindowResolutionPolicyTests|FullyQualifiedName~MainWindowVisibleChangedPolicyTests|FullyQualifiedName~MainWindowLoadedToggleActionPolicyTests|FullyQualifiedName~SettingsSaveFailureNotificationPolicyTests|FullyQualifiedName~MainWindowExitPlanPolicyTests|FullyQualifiedName~PhotoRightButtonDownExecutionPolicyTests|FullyQualifiedName~OverlayLostMouseCaptureExecutionPolicyTests|FullyQualifiedName~PhotoRightButtonUpExecutionPolicyTests|FullyQualifiedName~PhotoManipulationAdmissionPolicyTests|FullyQualifiedName~CrossPageInputSwitchTargetPolicyTests|FullyQualifiedName~StylusSampleTimestampPolicyTests"`：通过（`40/40`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~MainWindowOverlayInteractionStatePolicyTests|FullyQualifiedName~FloatingDispatchQueueDecisionLogPolicyTests|FullyQualifiedName~LauncherWindowResolutionPolicyTests|FullyQualifiedName~ZOrderQueueDispatchDecisionHandlingPolicyTests|FullyQualifiedName~ZOrderQueueDispatchFailureRollbackStatePolicyTests|FullyQualifiedName~MainWindowVisibleChangedPolicyTests|FullyQualifiedName~MainWindowLoadedToggleActionPolicyTests|FullyQualifiedName~SettingsSaveFailureNotificationPolicyTests|FullyQualifiedName~MainWindowExitPlanPolicyTests|FullyQualifiedName~PhotoRightButtonDownExecutionPolicyTests|FullyQualifiedName~OverlayLostMouseCaptureExecutionPolicyTests|FullyQualifiedName~PhotoRightButtonUpExecutionPolicyTests|FullyQualifiedName~PhotoManipulationAdmissionPolicyTests|FullyQualifiedName~CrossPageInputSwitchTargetPolicyTests|FullyQualifiedName~StylusSampleTimestampPolicyTests"`：通过（`46/46`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~MainWindowOverlayInteractionStatePolicyTests|FullyQualifiedName~MainWindowZOrderDedupIntervalPolicyTests|FullyQualifiedName~FloatingDispatchQueueDecisionLogPolicyTests|FullyQualifiedName~LauncherWindowResolutionPolicyTests|FullyQualifiedName~ZOrderQueueDispatchDecisionHandlingPolicyTests|FullyQualifiedName~ZOrderQueueDispatchFailureRollbackStatePolicyTests|FullyQualifiedName~MainWindowVisibleChangedPolicyTests|FullyQualifiedName~MainWindowLoadedToggleActionPolicyTests|FullyQualifiedName~SettingsSaveFailureNotificationPolicyTests|FullyQualifiedName~DialogShowResultStateUpdaterTests|FullyQualifiedName~InkStartupCleanupLogPolicyTests|FullyQualifiedName~MainWindowExitPlanPolicyTests|FullyQualifiedName~PhotoRightButtonDownExecutionPolicyTests|FullyQualifiedName~OverlayLostMouseCaptureExecutionPolicyTests|FullyQualifiedName~PhotoRightButtonUpExecutionPolicyTests|FullyQualifiedName~PhotoManipulationAdmissionPolicyTests|FullyQualifiedName~CrossPageInputSwitchTargetPolicyTests|FullyQualifiedName~StylusSampleTimestampPolicyTests"`：通过（`53/53`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ToolbarInteractionDirectRepairDispatchFailurePlanPolicyTests|FullyQualifiedName~MainWindowOnClosingPlanPolicyTests|FullyQualifiedName~MainWindowZOrderDedupIntervalPolicyTests|FullyQualifiedName~FloatingDispatchQueueDecisionLogPolicyTests|FullyQualifiedName~LauncherWindowResolutionPolicyTests|FullyQualifiedName~ZOrderQueueDispatchDecisionHandlingPolicyTests|FullyQualifiedName~ZOrderQueueDispatchFailureRollbackStatePolicyTests|FullyQualifiedName~MainWindowVisibleChangedPolicyTests|FullyQualifiedName~MainWindowLoadedToggleActionPolicyTests|FullyQualifiedName~SettingsSaveFailureNotificationPolicyTests|FullyQualifiedName~DialogShowResultStateUpdaterTests|FullyQualifiedName~InkStartupCleanupLogPolicyTests|FullyQualifiedName~MainWindowExitPlanPolicyTests"`：通过（`34/34`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ZOrderQueueDispatchFailureRollbackStateUpdaterTests|FullyQualifiedName~LauncherTopmostVisibilityStateUpdaterTests|FullyQualifiedName~SettingsSaveFailureNotificationStateUpdaterTests"`：通过（`6/6`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~MainWindow|FullyQualifiedName~FloatingDispatchQueue|FullyQualifiedName~Launcher|FullyQualifiedName~ZOrderQueue|FullyQualifiedName~ArchitectureDependencyTests"`：通过（`166/166`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ToolbarInteractionDirectRepairExecutionCoordinatorTests|FullyQualifiedName~ToolbarInteraction|FullyQualifiedName~FloatingTopmost|FullyQualifiedName~MainWindow|FullyQualifiedName~ArchitectureDependencyTests" -m:1 -p:UseSharedCompilation=false`：通过（`189/189`，2026-03-14）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ImageManagerStateChangeTransitionCoordinatorTests|FullyQualifiedName~ImageManagerStateChange|FullyQualifiedName~Foreground|FullyQualifiedName~FloatingWindow|FullyQualifiedName~MainWindow|FullyQualifiedName~ArchitectureDependencyTests" -m:1 -p:UseSharedCompilation=false`：通过（`121/121`，2026-03-14）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ForegroundSurfaceRetouchCoordinatorTests|FullyQualifiedName~Foreground|FullyQualifiedName~OverlayActivation|FullyQualifiedName~FloatingOwner|FullyQualifiedName~MainWindow|FullyQualifiedName~ArchitectureDependencyTests" -m:1 -p:UseSharedCompilation=false`：通过（`146/146`，2026-03-14）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ImageManagerVisibilityTransitionCoordinatorTests|FullyQualifiedName~PhotoModeTransitionCoordinatorTests|FullyQualifiedName~ImageManager|FullyQualifiedName~FloatingOwner|FullyQualifiedName~MainWindow|FullyQualifiedName~ArchitectureDependencyTests" -m:1 -p:UseSharedCompilation=false`：通过（`129/129`，2026-03-14）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPageDisplayToggleTransitionCoordinatorTests|FullyQualifiedName~CrossPageDisplayToggle|FullyQualifiedName~PaintOverlayWindow|FullyQualifiedName~ArchitectureDependencyTests" -m:1 -p:UseSharedCompilation=false`：通过（`17/17`，2026-03-14）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~InkShowTransitionCoordinatorTests|FullyQualifiedName~InkShowUpdateTransitionPolicyTests|FullyQualifiedName~PhotoShowInkOverlayChangePolicy|FullyQualifiedName~PaintOverlayWindow|FullyQualifiedName~ArchitectureDependencyTests" -m:1 -p:UseSharedCompilation=false`：通过（`18/18`，2026-03-14）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~InkPageLoadCoordinatorTests|FullyQualifiedName~InkPersistenceTogglePolicyTests|FullyQualifiedName~CrossPageInkFastPathSelectorTests|FullyQualifiedName~PaintOverlayWindow|FullyQualifiedName~ArchitectureDependencyTests" -m:1 -p:UseSharedCompilation=false`：通过（`28/28`，2026-03-14）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~InkStrokeApplyCoordinatorTests|FullyQualifiedName~CrossPageInkFastPathSelectorTests|FullyQualifiedName~InkPageLoadCoordinatorTests|FullyQualifiedName~PaintOverlayWindow|FullyQualifiedName~ArchitectureDependencyTests" -m:1 -p:UseSharedCompilation=false`：通过（`23/23`，2026-03-14）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPageDeferredRefreshCoordinatorTests|FullyQualifiedName~CrossPageDeferredRefreshGatePolicyTests|FullyQualifiedName~CrossPageDelayedDispatchFailureRecoveryPolicyTests|FullyQualifiedName~CrossPageDelayedDispatchFailureDiagnosticsPolicyTests|FullyQualifiedName~PaintOverlayWindow|FullyQualifiedName~ArchitectureDependencyTests" -m:1 -p:UseSharedCompilation=false`：通过（`20/20`，2026-03-15）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPagePostInputRefreshSlotCoordinatorTests|FullyQualifiedName~CrossPageDeferredRefreshCoordinatorTests|FullyQualifiedName~CrossPageDeferredRefreshGatePolicyTests|FullyQualifiedName~PaintOverlayWindow|FullyQualifiedName~ArchitectureDependencyTests" -m:1 -p:UseSharedCompilation=false`：通过（`18/18`，2026-03-15）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPageMissingNeighborRefreshCoordinatorTests|FullyQualifiedName~CrossPageMissingNeighborRefreshPolicyTests|FullyQualifiedName~CrossPageDeferredRefreshCoordinatorTests|FullyQualifiedName~CrossPageDelayedDispatchFailureRecoveryPolicyTests|FullyQualifiedName~PaintOverlayWindow|FullyQualifiedName~ArchitectureDependencyTests" -m:1 -p:UseSharedCompilation=false`：通过（`25/25`，2026-03-15）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPageReplayDispatchCoordinatorTests|FullyQualifiedName~CrossPageReplayDispatchPolicyTests|FullyQualifiedName~CrossPageReplayDispatchScheduleFallbackPolicy|FullyQualifiedName~CrossPageReplayPendingStateUpdaterTests|FullyQualifiedName~PaintOverlayWindow|FullyQualifiedName~ArchitectureDependencyTests" -m:1 -p:UseSharedCompilation=false`：通过（`31/31`，2026-03-15）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPageReplayFlushCoordinatorTests|FullyQualifiedName~CrossPageReplayDispatchCoordinatorTests|FullyQualifiedName~CrossPageReplayDispatchPolicyTests|FullyQualifiedName~CrossPageUpdateReplayPolicyTests|FullyQualifiedName~CrossPageReplayPendingStateUpdaterTests|FullyQualifiedName~PaintOverlayWindow|FullyQualifiedName~ArchitectureDependencyTests" -m:1 -p:UseSharedCompilation=false`：通过（`40/40`，2026-03-15）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPageDisplayUpdateDispatchFailureCoordinatorTests|FullyQualifiedName~CrossPageDisplayUpdateDispatchFailureFallbackPolicyTests|FullyQualifiedName~CrossPageReplayQueuePolicyTests|FullyQualifiedName~CrossPageReplayFlushCoordinatorTests|FullyQualifiedName~PaintOverlayWindow|FullyQualifiedName~ArchitectureDependencyTests" -m:1 -p:UseSharedCompilation=false`：通过（`21/21`，2026-03-15）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPageInputSwitchRequestPolicyTests|FullyQualifiedName~CrossPageInputSwitchExecutionPolicyTests|FullyQualifiedName~PointerDownToolExecutionPolicyTests|FullyQualifiedName~PointerMoveToolExecutionPolicyTests|FullyQualifiedName~PointerUpToolExecutionPolicyTests"`：通过（`24/24`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPageInputResumePolicyTests|FullyQualifiedName~PhotoManipulationDeltaExecutionPolicyTests|FullyQualifiedName~PhotoManipulationAdmissionPolicyTests|FullyQualifiedName~PointerDownToolExecutionPolicyTests|FullyQualifiedName~PointerMoveToolExecutionPolicyTests|FullyQualifiedName~PointerUpToolExecutionPolicyTests|FullyQualifiedName~CrossPageInputSwitchRequestPolicyTests|FullyQualifiedName~CrossPageInputSwitchExecutionPolicyTests"`：通过（`42/42`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPageCurrentPagePointerHitPolicyTests|FullyQualifiedName~CrossPageNeighborPageCandidatePolicyTests|FullyQualifiedName~CrossPageInputResumePolicyTests|FullyQualifiedName~PhotoManipulationDeltaExecutionPolicyTests|FullyQualifiedName~CrossPageInputSwitchRequestPolicyTests|FullyQualifiedName~CrossPageInputSwitchExecutionPolicyTests|FullyQualifiedName~PointerDownToolExecutionPolicyTests|FullyQualifiedName~PointerMoveToolExecutionPolicyTests|FullyQualifiedName~PointerUpToolExecutionPolicyTests"`：通过（`48/48`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~StylusDownExecutionPolicyTests|FullyQualifiedName~StylusMoveExecutionPolicyTests|FullyQualifiedName~StylusUpExecutionPolicyTests|FullyQualifiedName~StylusSampleTimestampPolicyTests|FullyQualifiedName~StylusSampleTimestampStateUpdaterTests|FullyQualifiedName~StylusBatchDispatchPolicyTests|FullyQualifiedName~StylusInterpolationPolicyTests|FullyQualifiedName~CrossPageInputResumePolicyTests|FullyQualifiedName~PhotoManipulationDeltaExecutionPolicyTests|FullyQualifiedName~PointerDownToolExecutionPolicyTests|FullyQualifiedName~PointerMoveToolExecutionPolicyTests|FullyQualifiedName~PointerUpToolExecutionPolicyTests"`：通过（`55/55`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~OverlayWheelPresentationExecutionPolicyTests|FullyQualifiedName~CrossPagePointerUpPostExecutionPolicyTests|FullyQualifiedName~StylusDownExecutionPolicyTests|FullyQualifiedName~StylusMoveExecutionPolicyTests|FullyQualifiedName~StylusUpExecutionPolicyTests|FullyQualifiedName~CrossPagePointerUpExecutionPlanPolicyTests|FullyQualifiedName~CrossPagePointerUpDecisionPolicyTests|FullyQualifiedName~CrossPageInputResumePolicyTests|FullyQualifiedName~PhotoManipulationDeltaExecutionPolicyTests"`：通过（`40/40`）
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~OverlayInputRoutingPolicyTests|FullyQualifiedName~OverlayPointerSourceHandlingPolicyTests|FullyQualifiedName~PhotoManipulationAdmissionPolicyTests|FullyQualifiedName~PhotoRightButtonDownExecutionPolicyTests|FullyQualifiedName~OverlayLostMouseCaptureExecutionPolicyTests|FullyQualifiedName~ArchitectureDependencyTests"`：通过（`32/32`）

## 9. 口径说明

- 本文档是当前唯一进度口径。
- 进度汇报默认只报“全项目终态重构总进度”，不混用局部口径。
- 若里程碑状态变化，必须同步更新：
  - `docs/plans/2026-03-06-best-target-architecture-plan.md`
  - `docs/handover.md`
  - 本文档
  - `docs/architecture/2026-03-10-target-boundary-map.md`
  - `docs/architecture/2026-03-10-interop-direct-dependency-matrix.md`


