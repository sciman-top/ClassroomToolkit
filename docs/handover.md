# ClassroomToolkit 重构交接说明（handover）

最后更新：2026-03-21  
当前主线：`终态最佳架构 + 全功能零回归`

## 1. 统一口径

- 主方案：`docs/plans/2026-03-06-best-target-architecture-plan.md`
- 主进度：`docs/validation/2026-03-06-target-architecture-progress.md`
- 首批编码切片：`docs/plans/2026-03-10-target-architecture-first-batch-implementation-plan.md`
- 终态边界图：`docs/architecture/2026-03-10-target-boundary-map.md`
- Interop 债务台账：`docs/architecture/2026-03-10-interop-direct-dependency-matrix.md`
- 自治执行入口：`docs/refactor/tasks.json` + `.codex/refactor-state.json` + `.codex/skills/autonomous-refactor-loop/SKILL.md`
- 本文档只保留“接手即能继续执行”的最小闭环信息。

## 2. 当前做到哪里

### 2026-03-21 演示翻页稳定性收口（新增）

- 背景：PPT/WPS 全屏放映在画笔/光标切换、滚轮/键盘翻页链路出现过多次回归。
- 已落地收口：
  - 统一导航意图与上下文模型：`PresentationNavigationIntent`、`PresentationNavigationContextSnapshot`、`PresentationNavigationDecision`。
  - 导航决策集中化：新增 `PresentationNavigationIntentParser` + `PresentationNavigationOrchestrator`，窗口层改为“事件入口 + 调用编排”。
  - 编排参数收口：`PresentationNavigationOrchestrator.ResolveHook` 改为单一 `PresentationNavigationHookContext`，避免多布尔参数顺序误用。
  - 去抖口径统一：窗口侧导航去抖改为 `PresentationNavigationDebounceMsPolicy` 统一解析，不再散落硬编码。
  - 键盘延迟优化：hook 键盘链路调度优先级改为 `DispatcherPriority.Input`，并新增策略契约测试。
  - 防双重去抖：hook 来源下发到 service 时禁用 service 侧重复去抖，避免窗口 + service 叠加延迟。
  - 新增源码契约门：`PaintOverlayPresentationNavigationContractTests` 锁定解析器/编排器入口、hook-source 选项与去抖策略调用点。
  - MainWindow.Photo 参数收口：`ApplyPhotoModeSurfaceTransition` 改为接收 `PhotoModeSurfaceTransitionContext`，移除多布尔参数调用形态，降低调用点顺序误用风险。
  - 新增 `MainWindowPhotoSurfaceTransitionContractTests` 锁定 photo-mode 与 presentation-fullscreen 两条切换链路都通过上下文构建进入策略层。
  - 新增 `MainWindowToolbarRetouchDispatchContractTests` 锁定工具栏直修链路必须经 `ToolbarInteractionDirectRepairExecutionCoordinator` 与后台调度失败分支处理。
  - 新增 `MainWindowPaintTransitionContractTests` 锁定画笔显隐切换入口必须走 `PaintVisibilityTransitionPolicy` 与 `FloatingZOrderApplyExecutor`，并保持 `EnsurePaintWindows` 的 skip/creation 双策略闸门。
- 自动化证据（本地最近一次）：
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PresentationNavigationRegressionMatrixTests|FullyQualifiedName~WpsHook|FullyQualifiedName~Presentation|FullyQualifiedName~Overlay"`
  - 结果：`635/635` 通过（2026-03-21）。
- 人工验证新增关注：
  - WPS/PPT 全屏下 `画笔 <-> 光标` 来回切换后，键盘/滚轮翻页均持续可用。
  - 光标模式键盘翻页无可感知额外延迟（相对滚轮与切换前基线）。

### 2026-03-18 全量审查进展（新增）

- 已落地全量审查执行手册与 CI 质量闸门：
  - `docs/validation/2026-03-18-full-code-audit-playbook.md`
  - `.github/workflows/quality-gate.yml`
- 已完成 Chunk 1 证据归档：
  - `docs/validation/evidence/2026-03-18-full-audit/01-risk-inventory.md`
  - `docs/validation/evidence/2026-03-18-full-audit/02-static-and-test-gates.md`
- 已完成 Chunk 2 首轮热点闭环：
  - `ImageManagerWindow` 目录展开 `async void` 生命周期风险已修复并补回归测试。
  - `WindowInteropRetryExecutor` 已支持可取消重试等待并补测试。
  - `ComObjectManager` 已补契约测试 + 运行时集成测试。
  - 证据文档：`03-hotspot-findings.md`、`04-fixes-and-regression.md`。
- 自动化最新结果（本地）：
  - Debug 全量：`2789/2789` 通过（2026-03-18）
  - Release 全量：`2789/2789` 通过（2026-03-18）
- 人工最终回归门本轮按用户指令跳过（课堂场景、DPI、互切链路仍待后续人工验收）。

- 运行时主干已进入“状态更新器 + 策略函数 + 执行器”持续收口阶段。
- 跨页链路已完成自动化尾部收口：`CrossPage/Photo/Overlay/ArchitectureDependencyTests` 定向过滤 `903/903` 通过；剩余以冻结前人工场景回归为主。
- 配置 / 存储分治已进入实质落地阶段：JSON 默认路径已切换，RollCall SQLite 已真实落库。
- SQLite 下一业务域已定版并闭环为学生名册（student workbook / class roster）：`adapter -> wiring -> migration/rollback` 三步已完成。
- 学生名册 SQLite runtime wiring 已完成：`RollCallWorkbookStoreResolver` 在 SQLite 分支改用 `StudentWorkbookSqliteRollCallStoreAdapter`（包装 `StudentWorkbookSqliteStoreAdapter`），并输出启动期后端选择 guardrail 日志；bridge 读取失败时可回读 SQLite snapshot 的兜底路径已有定向测试覆盖。
- Ink 抽象已具备 CPU 保底与 GPU 可选入口，且 optional-release 策略已收口：`CTOOLKIT_USE_GPU_INK_RENDERER + CTOOLKIT_ENABLE_EXPERIMENTAL_GPU_INK` 双门控下，GPU 仅在能力满足时启用，探测失败或运行异常会稳定回退 CPU（`Ink|Renderer|Brush|Gpu` 定向过滤 `259/259` 通过，2026-03-13）；GPU 仍不是可发布主路径。
- `MainWindow.Launcher` 已完成一轮策略化收口（工作区钳制 / 自动退出计时 / 启动诊断开关）并补齐定向单测。
- `MainWindow.xaml` 已完成一轮策略化收口（退出计划 / Ink 清理候选目录 / Launcher topmost 时间戳）并补齐定向单测。
- `MainWindow.xaml` 的 Launcher 运行时 fallback 日志判断已由独立策略接管，减少主链内联分支。
- `MainWindow.Paint` 已完成一轮策略化收口（窗口创建/早退判定、Session 违例日志门控、PhotoClose owner 断链门控）并补齐定向单测。
- `MainWindow.Paint` 已继续完成 toolbar direct-repair 调度协调收口：`ToolbarInteractionDirectRepairExecutionCoordinator` 接管后台调度、rerun 与 failure-plan 协调，热点文件内不再保留整段过程式调度分支。
- `MainWindow.Photo` 已继续完成 image-manager state-change transition 收口：`ImageManagerStateChangeTransitionCoordinator` 接管 overlay normalize 调度/同步 fallback 与 surface-apply 协调，热点文件内不再保留该恢复链的过程式调度分支。
- `MainWindow.Photo` 已继续完成 foreground retouch 收口：`ForegroundSurfaceRetouchCoordinator` 接管 overlay-activation retouch 与 explicit-foreground retouch 的节流、状态写回与最终 apply 协调，热点文件内不再保留该前台补触链的过程式判定分支。
- `MainWindow.Photo` 已继续完成 image-manager owner-sync / open-close transition 收口：`ImageManagerVisibilityTransitionCoordinator` 接管 image-manager open/close transition 的 owner/show/close/surface 协调，`PhotoModeTransitionCoordinator` 接管 photo-mode change 下的 image-manager suppress、toolbar state、owner-sync 与 surface 协调。
- `MainWindow.Photo` 已完成一轮策略化收口（Ink 覆盖开关门控、焦点门控、owner 同步门控、统一变换变更判定、ImageManager surface 触发门控）并补齐定向单测。
- `PaintOverlayWindow.Presentation` 已完成一轮共享准入策略收口（焦点监控激活、导航发送准入、键盘派发门控），减少 WPS/Office 导航链重复分支。
- `PaintOverlayWindow.Presentation` 已继续完成一轮核心门控策略收口（通道可用性、全屏类型决策、WPS hook 启用门控），并接线替换到主路径。
- `PaintOverlayWindow.Presentation` 的剩余 tail 已按既有实现完成验证闭环：`PresentationFocusRestorePolicy`、`OverlayPresentationCommandRouter` 与相关 Presentation 定向过滤测试、`ArchitectureDependencyTests` 已通过，可从执行图中视为完成。
- `PaintOverlayWindow.xaml` 已完成一轮运行时门控策略收口（Ink 显示切换、Topmost 应用门控、焦点解析门控、WPS Raw 兜底目标判定、Ink 缓存/保存更新计划）并补齐定向单测。
- `PaintOverlayWindow.xaml` 已继续完成一轮运行时状态策略收口（Photo 变换记忆门控、统一变换运行时应用门控、Dispatcher 调度可用性门控）并补齐定向单测。
- `PaintOverlayWindow.xaml` 已继续完成 cross-page display toggle 收口：`CrossPageDisplayToggleTransitionCoordinator` 接管跨页显示切换中的标志更新、统一变换恢复/保存、邻页清理、图片源刷新与 PDF ink cache reload 协调，窗口壳内不再保留整段过程式切换分支。
- `PaintOverlayWindow.xaml` 已继续完成 ink-show transition 收口：`InkShowTransitionCoordinator` 接管显示笔迹开关中的设置变更、隐藏时清空/缓存清理、显示时当前页加载与跨页刷新请求协调，窗口壳内不再保留整段过程式开关分支。
- `PaintOverlayWindow.Photo` 已继续完成 ink page-load 收口：`InkPageLoadCoordinator` 接管当前页笔迹加载中的 cache-scope 判断、显示状态判断、cache-hit、sidecar-fallback 与 clear-state 分支协调，热点文件内不再保留整段入口决策分支。
- `PaintOverlayWindow.Photo` 已继续完成 ink stroke-apply 收口：`InkStrokeApplyCoordinator` 接管当前页笔迹应用中的运行时集合更新、fast-path vs redraw 分支，以及 loaded-state/perf 记账协调，热点文件内不再保留整段应用分支。
- `PaintOverlayWindow.Photo.CrossPage` 已继续完成 deferred refresh 收口：`CrossPageDeferredRefreshCoordinator` 接管 post-input delay gate、延迟分发、单次 pointer-up 去重与失败恢复协调，跨页窗口文件内不再保留整段延迟刷新调度分支。
- `PaintOverlayWindow.Photo.CrossPage` 已继续完成 post-input refresh-slot 收口：`CrossPagePostInputRefreshSlotCoordinator` 接管 pointer-up sequence 的 CAS acquire 语义（unset/matched/success/retry），跨页窗口文件内不再保留内联 CAS 循环。
- `PaintOverlayWindow.Photo.CrossPage` 已继续完成 missing-neighbor refresh 收口：`CrossPageMissingNeighborRefreshCoordinator` 接管缺失邻页的 policy 判定、延迟分发与失败恢复协调，跨页窗口文件内不再保留整段邻页缺失刷新调度分支。
- `PaintOverlayWindow.Photo.CrossPage` 已继续完成 replay dispatch 收口：`CrossPageReplayDispatchCoordinator` 接管 replay 分发的调度、inline fallback 与失败 requeue 协调，跨页窗口文件内不再保留整段 replay dispatch 分支。
- `PaintOverlayWindow.Photo.CrossPage` 已继续完成 replay flush 收口：`CrossPageReplayFlushCoordinator` 接管 replay flush 的 gate 判定与 dispatch-target 选择协调，跨页窗口文件内不再保留整段 flush 入口分支。
- `PaintOverlayWindow.Photo.CrossPage` 已继续完成 dispatch-failure 收口：`CrossPageDisplayUpdateDispatchFailureCoordinator` 接管分发失败下的 inline fallback、replay queue 与 replay flush 协调，跨页窗口文件内不再保留整段 dispatch-failure 分支。
- `PaintOverlayWindow.Input` 已完成一轮输入路由策略收口（滚轮/键盘路由决策统一、鼠标来源门控统一）并补齐定向单测。
- `PaintOverlayWindow.Input` 已继续完成 pointer-up 后置动作执行计划收口（追踪/刷新/回放/刷新来源聚合），并将右键/触笔来源门控并入统一路径。
- `PaintOverlayWindow.Input` 已继续完成触笔时间戳状态收口（`StylusSampleTimestampState` + `StylusSampleTimestampPolicy` + `StylusSampleTimestampStateUpdater`），批次跨度估算/单调递增/状态写回已统一到单一状态路径并补齐定向单测。
- `PaintOverlayWindow.Input` 已继续完成输入来源门控重复分支聚合（统一入口 `ShouldContinuePointerInput`），鼠标/右键/触笔 begin-pan 路径不再重复内联来源判定。
- `PaintOverlayWindow.Input` 已继续完成来源门控动作决策外提（`OverlayPointerSourceHandlingPolicy` + `OverlayPointerSourceHandlingPlan`），Continue/Consume/Ignore 的执行语义已可独立测试。
- `PaintOverlayWindow.Input` 已继续完成事件执行计划收口（`StylusPhotoPanExecutionPolicy` + `PhotoManipulationEventHandlingPolicy`），触笔 photo-pan 与 manipulation 的事件处置分支已从处理器内联逻辑上收为可测策略。
- `PaintOverlayWindow.Input` 已继续完成 manipulation 准入聚合（`TryAdmitPhotoManipulation`），`Starting/Delta` 两条事件路径共享同一准入与 `Handled` 语义。
- `PaintOverlayWindow.Input` 已继续完成跨页输入切页准入聚合（`CrossPageInputSwitchAdmissionPolicy`），gate/bitmap 可用性/指针滞回判定已统一到单一准入决策路径。
- `PaintOverlayWindow.Input` 已继续完成鼠标 photo-pan 执行计划聚合（`PhotoPanMouseExecutionPolicy`），move/end 路径的动作判定已转为可测计划语义。
- `PaintOverlayWindow.Input` 已继续完成跨页输入目标页策略化（`CrossPageInputSwitchTargetPolicy`）与 manipulation 组合准入策略化（`PhotoManipulationAdmissionPolicy`），并统一 photo-pan 允许性判定入口（`ResolveShouldPanPhoto`）。
- `PaintOverlayWindow.Input` 已继续清理失效私有入口（未被调用的 manipulation 路由包装与空载 photo-pan 活跃判断），降低后续改造认知负担。
- `PaintOverlayWindow.Input` 已继续完成右键链路策略化（`PhotoRightClickPendingStateUpdater` + `PhotoRightButtonUpExecutionPolicy`），pending 状态变迁与 up 执行计划均已可独立测试。
- `PaintOverlayWindow.Input` 已继续完成右键 down / overlay lost-capture 执行计划策略化（`PhotoRightButtonDownExecutionPolicy` + `OverlayLostMouseCaptureExecutionPolicy`），事件处理器内联动作分支进一步减少。
- `PaintOverlayWindow.Input` 已继续统一右键事件链执行语义：right-button-down 与 lost-mouse-capture 两条路径都改为执行计划驱动，不再直接内联动作判定。
- `PaintOverlayWindow.Input` 已新增跨页输入切页请求与执行策略（`CrossPageInputSwitchRequestPolicy` + `CrossPageInputSwitchExecutionPolicy`），切页准入与执行模式分支进一步从主链方法内联逻辑中上收。
- `PaintOverlayWindow.Input` 已新增输入工具分派策略（`PointerDownToolExecutionPolicy` + `PointerMoveToolExecutionPolicy` + `PointerUpToolExecutionPolicy`），down/move/up 三段工具动作与刷子收尾刷新计划已统一到可测策略。
- `PaintOverlayWindow.Input` 已新增跨页切页恢复策略（`CrossPageInputResumePolicy`），切页后的 brush continuation / 当前输入回放 / eraser 恢复已由统一执行计划驱动。
- `PaintOverlayWindow.Input` 已新增手势增量执行策略（`PhotoManipulationDeltaExecutionPolicy`），`OnManipulationDelta` 的平移阈值、pan telemetry、跨页刷新请求已从内联分支上收。
- `PaintOverlayWindow.Input` 已新增可见页命中策略（`CrossPageCurrentPagePointerHitPolicy` + `CrossPageNeighborPageCandidatePolicy`），当前页/邻页命中判定路径进一步可测化。
- `PaintOverlayWindow.Input` 已新增触笔事件执行策略（`StylusDownExecutionPolicy` + `StylusMoveExecutionPolicy` + `StylusUpExecutionPolicy`），触笔 down/move/up 在加载态、photo-pan、ink 操作态、批处理路径下统一为执行计划驱动。
- `PaintOverlayWindow.Input` 已新增滚轮演示执行策略（`OverlayWheelPresentationExecutionPolicy`），WPS hook 绕行/最近触发抑制/滚轮方向命令映射改为统一执行判定。
- `PaintOverlayWindow.Input` 已新增 pointer-up 后处理计划策略（`CrossPagePointerUpPostExecutionPolicy`），后置动作链由统一 post-plan 驱动并减少主方法重复条件。
- `PaintOverlayWindow.Input` 的剩余 Overlay input tail 已按既有实现完成验证闭环：`OverlayInputRoutingPolicy`、`OverlayPointerSourceHandlingPolicy`、`PhotoManipulationAdmissionPolicy`、`PhotoRightButtonDownExecutionPolicy`、`OverlayLostMouseCaptureExecutionPolicy` 与 `ArchitectureDependencyTests` 已通过，可从执行图中视为完成。
- `MainWindow.xaml` 已继续完成一轮小分支策略化收口（`MainWindowVisibleChangedPolicy`、`MainWindowLoadedToggleActionPolicy`、`SettingsSaveFailureNotificationPolicy`），减少生命周期与异常分支的内联状态判断。
- `MainWindow.xaml` 已继续完成一轮协调链路策略化收口（`MainWindowOverlayInteractionStatePolicy`、`FloatingDispatchQueueDecisionLogPolicy`、`LauncherWindowResolutionPolicy`），减少 Z-order 与 launcher 解析中的内联判定分支。
- `MainWindow.xaml` 已继续完成 Z-order 队列回调策略化收口（`ZOrderQueueDispatchDecisionHandlingPolicy`、`ZOrderQueueDispatchFailureRollbackStatePolicy`），队列回调中的日志/失败标记/回滚状态选择已从内联逻辑上收为可测策略。
- `MainWindow.xaml` 已继续完成去重间隔/对话框结果/Ink 启动清理日志三项策略化收口（`MainWindowZOrderDedupIntervalPolicy`、`DialogShowResultStateUpdater`、`InkStartupCleanupLogPolicy`），进一步压缩内联分支与格式化逻辑。
- `MainWindow.xaml` 已继续完成剩余运行时写回尾部收口：`LauncherTopmostVisibilityStateUpdater`、`SettingsSaveFailureNotificationStateUpdater`、`ZOrderQueueDispatchFailureRollbackStateUpdater` 已接管 Launcher topmost 时间戳、设置保存失败通知标记、Z-order 队列失败回滚的内联赋值；`mainwindow-scattered-state-consolidation-tail` 可视为完成。
- `MainWindow.Paint` 已继续完成工具条直修调度失败分支收口（`ToolbarInteractionDirectRepairDispatchFailurePlanPolicy`）并抽出直接修复执行方法；`MainWindow.xaml` 关闭流程已接入 `MainWindowOnClosingPlanPolicy`。
- 当前全项目终态重构总进度按唯一口径为 `100%`（代码与自动化范围）。
- 仓库内已补一套 repo-local 自治重构 loop 骨架，用于按 `tasks/state` 续跑，不再依赖单次会话记忆。
- 自动化冻结复检任务 `automated-freeze-recheck-after-gap-closure` 已完成：`ArchitectureDependencyTests`=`5/5`，全量 Debug=`2227/2227`，全量 Release=`2227/2227`（2026-03-13）；当前自动化门已闭合，下一步仅剩人工最终回归。

## 3. 当前硬指标

- 目标框架：`.NET 10`
- App 层直接引用 `ClassroomToolkit.Interop` 的文件数：`6`
- 全量 Debug：`2502/2502`（最新冻结复检，通过）
- 全量 Release：`2502/2502`（最新冻结复检，通过）

## 4. 当前结构现实

- `App` 仍直接引用 `Application / Infra / Services / Interop`，但这不意味着这些耦合都是终态许可面。
- `Services` 当前口径是“运行时能力实现层 / Application 端口实现外观层”，不要把它继续做成新的业务中心。
- `Application*Flow` 相关开关目前不再是可靠的运行时回滚总闸，只能视为历史遗留定义。

## 5. 接手后优先做什么

1. 继续收口高风险场景互切  
图片 / PDF / 白板 / PPT-WPS 互切时，优先处理窗口层级、激活、焦点、输入链路。

2. 继续压缩残余 Interop 直连面  
优先关注：
  - `src/ClassroomToolkit.App/MainWindow.*`
  - `src/ClassroomToolkit.App/Windowing/*InteropAdapter.cs`

3. 继续收口散点状态写入  
把高风险 UI / 场景文件中的直接赋值迁移到 Session / Updater / Policy / Executor。

4. 继续深化业务 SQLite  
学生名册（student workbook / class roster）SQLite 路径已完成；下一历史向业务域 Ink 历史快照（ink stroke history snapshot，含按文档/页索引的笔迹历史）也已完成 `adapter -> wiring -> migration/rollback` 闭环并同步到回滚手册。

5. Ink 继续按“CPU 稳定优先，GPU 可选启用”推进  
不要为了 GPU 路径打断高风险主链收口。

## 6. 当前热点文件

- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs`
- `src/ClassroomToolkit.App/MainWindow.xaml.cs`
- `src/ClassroomToolkit.App/MainWindow.Paint.cs`

说明：

- 这些文件仍是高风险编排与历史债集中区。
- 接手时优先结合 Session / Windowing / Application UseCases 分拆，不要继续向这些文件堆新逻辑。

## 7. 最近已落地且不要回退的原则

- JSON 是配置默认主路径，INI 只作兼容与迁移兜底。
- 业务 SQLite 已不是占位概念，后续应继续深化，而不是退回 Excel / INI 运行时主存储。
- `Application` / `Session` / `Policy` / `Updater` / `Executor` 是当前收口主方向，禁止回到散点 if/else 修补。
- Interop 失败必须降级，不得把异常直接抛到 UI 主链。
- 架构守卫只允许收紧，不允许为了“先过测试”而放大白名单。

## 8. 当前不要做的事

- 不要无验证地重写整块高风险链路。
- 不要把历史 `phase-*` 文档当成当前执行口径。
- 不要为了样式或 UI 新鲜感切换到 WinUI。
- 不要把 GPU Ink、视觉优化或非关键重构排到高风险主链之前。
- 不要再把 `CTOOLKIT_USE_APPLICATION_*` 写进新的回滚或冻结方案。

## 9. 必跑验证

- 定向测试：按本批改动的场景、策略、状态更新器精确过滤。
- 全量 Debug：
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- 高风险批次补跑全量 Release：
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release`

## 10. 必做人工回归点（进入冻结前）

- PPT / WPS 全屏放映
- 图片 / PDF 全屏跨页
- 白板
- 三者互切
- 启动器 / 工具条 / 点名窗口的置顶与激活关系

## 11. 文档同步要求

只要以下任一项变化，必须同步更新主方案 / 主进度 / handover / 边界图 / Interop 台账：

- 总进度
- App -> Interop 文件数
- `Services` 定位
- 有效 feature flag
- 最终验收入口

## 12. 注意事项

- 仓库当前是长期脏工作区，接手前先看 `git status`，但不要回滚不属于当前任务的改动。
- 默认连续执行，不做阶段性汇报；仅在真实阻塞时暂停。
- 若主方案 / 主进度 / handover / ADR 发生冲突，按优先级裁决，不得先回退到旧方案。

