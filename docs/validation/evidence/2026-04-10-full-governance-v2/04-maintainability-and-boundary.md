# 2026-04-10 Full Governance v2 - Phase 4 Maintainability And Boundary

## 1) Objective

- 通过“边界契约 + 重复逻辑收敛”提升可维护性，减少后续回归风险。

## 2) Completed In This Batch

### 2.1 Windowing retry internals refactor (low-risk maintainability)

- 已完成公共核心抽取与三策略收敛（见 Phase 2 证据）。
- 目的：降低重复修改成本，确保重试语义一致。

### 2.2 Contract hardening: forbid blocking sleep in production

- new test: `tests/ClassroomToolkit.Tests/BlockingSleepUsageContractTests.cs`
- contract:
  - 扫描 `src/**/*.cs`
  - 若出现 `Thread.Sleep(` 直接失败
- rationale:
  - 将“避免阻塞导致卡顿”前置为自动化门禁
  - 防止未来回归到同步阻塞实现

### 2.3 Infra storage adapter common utility extraction

- new file: `src/ClassroomToolkit.Infra/Storage/SqliteStorageUtilities.cs`
- modified files:
  - `src/ClassroomToolkit.Infra/Storage/RollCallSqliteStoreAdapter.cs`
  - `src/ClassroomToolkit.Infra/Storage/StudentWorkbookSqliteStoreAdapter.cs`
- extracted common logic:
  - SQLite connection open/create directory
  - schema column existence guard
  - UTC timestamp parse helper
- effect:
  - 减少双适配器重复实现，降低后续修复分叉风险
  - 外部行为与存储契约保持不变

### 2.4 Ink export large-file decomposition (safe helper extraction)

- new file:
  - `src/ClassroomToolkit.App/Ink/InkExportScaleUtilities.cs`
- modified file:
  - `src/ClassroomToolkit.App/Ink/InkExportService.cs`
- action:
  - 把位图 DIP 尺寸换算与比例计算纯函数外提到独立工具类
  - `InkExportService` 保留原有公开/私有核心流程与反射测试关注方法
- effect:
  - 降低 `InkExportService` 体积（hotspot 由 `1111` -> `1081` 行）
  - 不改变导出行为、坐标换算结果与现有测试契约

### 2.5 Paint overlay large-file decomposition (partial split)

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.BrushFlow.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs`
- action:
  - 将笔刷开始/移动/结束与预览节流相关方法搬迁到新 partial 文件
  - 纯搬迁，无逻辑改写
- effect:
  - `PaintOverlayWindow.Ink.cs` 体积由 `1103` -> `963` 行
  - 热点文件可读性提升，后续分治重构成本降低

### 2.6 Paint overlay decomposition batch 2 (eraser/region entry flow)

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.EraserAndRegion.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs`
- action:
  - 将橡皮擦与区域擦除入口流程方法搬迁到独立 partial 文件
  - 保持调用链与行为不变，仅调整代码组织
- effect:
  - `PaintOverlayWindow.Ink.cs` 体积进一步由 `963` -> `867` 行
  - 热点聚焦继续下降，便于后续按子域增量重构

### 2.7 Ink export decomposition batch 2 (fingerprint hashing helpers)

- new file:
  - `src/ClassroomToolkit.App/Ink/InkExportFingerprintUtilities.cs`
- modified file:
  - `src/ClassroomToolkit.App/Ink/InkExportService.cs`
- action:
  - 将指纹哈希拼接辅助方法外提（字段分隔、token 分隔、UTF-8 追加）
  - 保留 `BuildExportFingerprint` 方法签名与调用路径，确保反射测试契约不变
- effect:
  - `InkExportService.cs` 体积进一步由 `1081` -> `1062` 行
  - 导出指纹逻辑可读性提升，后续扩展字段更易维护

### 2.8 Paint overlay decomposition batch 3 (cross-page region erase flow)

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.CrossPageRegionErase.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs`
- action:
  - 将跨页区域擦除流程（页面命中、跨页导航、批次擦除）方法拆到独立 partial
  - 原方法签名与调用链保持不变
- effect:
  - `PaintOverlayWindow.Ink.cs` 体积进一步由 `867` -> `760` 行
  - 跨页逻辑与通用 Ink 流程解耦，后续问题定位更直接

### 2.9 Paint overlay decomposition batch 4 (shape flow)

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.ShapeFlow.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs`
- action:
  - 将形状绘制流程（普通形状 + 三角形分步交互 + 草稿取消）搬迁到独立 partial
  - 方法签名与调用链保持不变
- effect:
  - `PaintOverlayWindow.Ink.cs` 体积进一步由 `760` -> `559` 行
  - 形状交互子域与其他 Ink 流程彻底分离，维护与排障效率提升

### 2.10 Paint overlay decomposition batch 5 (erase core flow)

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.EraseCore.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs`
- action:
  - 将橡皮擦核心与区域擦除基础流程（`HideEraserPreview/ApplyEraserAt/EraseRect/ClearRegionSelection`）拆到独立 partial
  - 保持签名与调用链不变，仅做职责分组
- effect:
  - 擦除子域与记录/回放流程彻底分离，后续局部修复影响面更小
  - `PaintOverlayWindow.Ink.cs` 继续减负并提升可读性

### 2.11 Ink export decomposition batch 3 (manifest utilities)

- new file:
  - `src/ClassroomToolkit.App/Ink/InkExportManifestUtilities.cs`
- modified file:
  - `src/ClassroomToolkit.App/Ink/InkExportService.cs`
- action:
  - 将导出清单（manifest）路径/键生成、读取、并发安全写入逻辑下沉到独立 utility
  - `InkExportService` 保留同名薄封装方法，调用链保持不变
- effect:
  - `InkExportService` 继续减小职责面，导出主流程与清单 I/O 逻辑解耦
  - 便于后续单独优化 manifest 一致性与并发策略

### 2.12 Paint overlay decomposition batch 6 (stroke recording flow)

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.StrokeRecording.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs`
- action:
  - 将运行期笔迹记录与掩码种子计算逻辑拆到独立 partial
  - 记录入口、序列化字段与哈希算法保持不变
- effect:
  - `PaintOverlayWindow.Ink.cs` 进一步收敛为流程编排壳层
  - 笔迹记录子域更易单独维护与测试

### 2.10 Paint overlay decomposition batch 5 (erase core flow)

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.EraseCore.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs`
- action:
  - 将橡皮擦核心流程（点擦除、矩形擦除、区域选择清理）搬迁到独立 partial
  - 保持行为不变，仅调整代码归属
- effect:
  - `PaintOverlayWindow.Ink.cs` 体积进一步由 `559` -> `509` 行
  - 擦除逻辑与记录/渲染逻辑进一步解耦，后续演进边界更清晰

### 2.11 Paint overlay decomposition batch 6 (stroke recording flow)

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.StrokeRecording.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs`
- action:
  - 将运行时笔迹记录流程（刷子/形状记录、掩码种子计算）整体搬迁到独立 partial
  - 保持原方法签名与调用路径不变
- effect:
  - `PaintOverlayWindow.Ink.cs` 体积进一步由 `509` -> `313` 行
  - 记录逻辑与擦除/形状/跨页逻辑完成结构化解耦

### 2.12 Paint overlay decomposition batch 7 (context/lifecycle flow)

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.ContextLifecycle.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs`
- action:
  - 将上下文、缓存、脏标记、跨页视觉同步、输入监控节流相关方法迁移到独立 partial
  - 迁移过程中出现一次编译失败（因文件缺失导致方法引用丢失），已恢复并通过全门禁复验
- effect:
  - `PaintOverlayWindow.Ink.cs` 体积进一步由 `313` -> `134` 行
  - 主文件仅保留擦除核心逻辑，子域边界清晰

### 2.13 Ink export decomposition batch 3 (rendering flow)

- new file:
  - `src/ClassroomToolkit.App/Ink/InkExportService.Rendering.cs`
- modified file:
  - `src/ClassroomToolkit.App/Ink/InkExportService.cs`
- action:
  - 将渲染与加载相关方法迁移到独立 partial：
    - `CompositeImage`
    - `LoadBackground/LoadPdfPage/LoadImage`
    - `GetPdfPageCount`
    - `SaveImage`
  - `InkExportService` 主文件改为 `partial`，保留现有入口与契约方法
- effect:
  - `InkExportService.cs` 体积进一步由 `1011` -> `908` 行
  - 导出主流程与底层渲染细节分离，维护复杂度下降

### 2.14 MainWindow decomposition batch 1 (lifecycle/startup/exit flow)

- new file:
  - `src/ClassroomToolkit.App/MainWindow.Lifecycle.cs`
- modified files:
  - `src/ClassroomToolkit.App/MainWindow.xaml.cs`
  - `tests/ClassroomToolkit.Tests/App/MainWindowSettingsSaveNotificationContractTests.cs`
  - `tests/ClassroomToolkit.Tests/MainWindowStartupWarmupDispatchContractTests.cs`
  - `tests/ClassroomToolkit.Tests/App/RollCallWindowSettingsReloadContractTests.cs`
  - `tests/ClassroomToolkit.Tests/MainWindowExitLifecycleContractTests.cs`
- action:
  - 将 MainWindow 生命周期相关逻辑拆分到独立 partial（加载、预热、退出、设置保存、关闭清理）。
  - `MainWindow.xaml.cs` 保留字段/构造/点名与 Z-order 主编排，降低单文件认知负担。
  - 适配契约测试源码定位：由单文件路径迁移为生命周期 partial 路径，保持原契约语义不变。
- effect:
  - `MainWindow.xaml.cs` 行数由 `979` 降到 `589`。
  - 生命周期逻辑职责边界更清晰，后续可继续独立演进与测试。

### 2.15 MainWindow decomposition batch 2 (z-order coordination flow)

- new file:
  - `src/ClassroomToolkit.App/MainWindow.ZOrder.cs`
- modified files:
  - `src/ClassroomToolkit.App/MainWindow.xaml.cs`
  - `tests/ClassroomToolkit.Tests/MainWindowContractSourceReader.cs`
- action:
  - 将 MainWindow 的 Z-order 编排、调度、去重、窗口置顶协同逻辑迁移到独立 partial。
  - `MainWindow.xaml.cs` 收敛为字段/构造器/点名流程主入口，降低单文件复杂度。
  - 契约源码聚合器扩展为 `core + lifecycle + z-order` 三文件聚合，避免后续拆分触发路径耦合。
- effect:
  - `MainWindow.xaml.cs` 行数由 `589` 降到 `257`。
  - 主流程文件聚焦交互入口，Z-order 子域可独立维护与演进。

### 2.16 MainWindow decomposition batch 3 (roll-call flow)

- new file:
  - `src/ClassroomToolkit.App/MainWindow.RollCall.cs`
- modified files:
  - `src/ClassroomToolkit.App/MainWindow.xaml.cs`
- action:
  - 将点名窗口交互与设置应用流程迁移到独立 partial（点击切换、生命周期绑定、设置对话框应用）。
  - 主文件仅保留字段与构造器初始化，进一步降低核心文件阅读负担。
- effect:
  - `MainWindow.xaml.cs` 行数由 `257` 降到 `128`。
  - 点名流程与生命周期/Z-order 流程形成清晰子域分层，后续维护更可控。

### 2.17 CrossPage decomposition batch 1 (bounds and scroll convergence flow)

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Bounds.cs`
- modified files:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs`
  - `tests/ClassroomToolkit.Tests/MainWindowContractSourceReader.cs`
- action:
  - 将跨页显示中的边界与滚动收敛方法迁移到独立 partial：
    - `UpdateNeighborTransformsForPan`
    - `FinalizeCurrentPageFromScroll`
    - `SyncCurrentPageToViewportCenter`
    - `ClampSinglePageTranslateY`
    - `ApplyCrossPageBoundaryLimits`
    - `TryGetCrossPageBounds`
  - 保持调用路径与行为不变，仅做归属调整。
  - 同步将 MainWindow 契约聚合读取扩展到 `MainWindow.RollCall.cs`，降低后续拆分脆弱性。
- effect:
  - `PaintOverlayWindow.Photo.CrossPage.cs` 行数由 `913` 降到 `626`。
  - CrossPage 主流程文件更聚焦邻页渲染编排，边界/收敛逻辑独立维护。

### 2.18 CrossPage decomposition batch 2 (neighbor ink cache/render surface helpers)

- modified files:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.NeighborInk.cs`
- action:
  - 将邻页墨迹缓存与渲染面辅助方法从 `CrossPage.cs` 迁移到 `CrossPage.NeighborInk.cs`：
    - `TryGetNeighborInkCacheEntry`
    - `BuildNeighborInkCacheEntry`
    - `ResolveNeighborInkRenderSurfacePlan`
    - `TryResolveNeighborInkHorizontalBounds`
  - 仅调整代码归属，不改变行为与调用链。
- effect:
  - `PaintOverlayWindow.Photo.CrossPage.cs` 行数由 `626` 降到 `547`。
  - 邻页墨迹生命周期与渲染缓存逻辑集中到同一子域文件，维护边界更清晰。

### 2.19 CrossPage decomposition batch 3 (display/render entry flow)

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Rendering.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs`
- action:
  - 将跨页显示主入口与邻页渲染主流程迁移到独立 partial：
    - `UpdateCrossPageDisplay`
    - `RenderNeighborPages`
  - 仅做代码归属迁移，不改变逻辑与调用路径。
- effect:
  - `PaintOverlayWindow.Photo.CrossPage.cs` 行数由 `547` 降到 `24`。
  - CrossPage 入口编排与子域实现进一步解耦，后续维护颗粒度更细。

### 2.20 Photo navigation decomposition batch 1 (UI command and request flow)

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.Commands.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.cs`
- action:
  - 将 Photo 导航的交互入口与请求分发方法迁移到独立 partial：
    - 标题栏拖拽与置顶协同
    - 右键菜单关闭操作
    - 上一页/下一页点击入口
    - 文档内导航判定与文件导航请求
  - 保持逻辑与调用路径不变，仅做归属迁移。
- effect:
  - `PaintOverlayWindow.Photo.Navigation.cs` 行数由 `839` 降到 `701`。
  - 导航编排主体与 UI 事件入口解耦，后续维护边界更明确。

### 2.21 Photo navigation decomposition batch 2 (page save-state flow)

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.SaveState.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.cs`
- action:
  - 将页面保存状态相关方法迁移到独立 partial：
    - `SaveCurrentPageIfNeeded`
    - `SaveCurrentPageOnNavigate`
  - 保持原有逻辑、签名与调用链不变，仅调整代码归属。
- effect:
  - `PaintOverlayWindow.Photo.Navigation.cs` 行数由 `701` 降到 `557`。
  - 导航流程与状态持久化职责分离，后续演进更清晰。

### 2.22 Photo navigation decomposition batch 3 (navigate pipeline flow)

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.Navigate.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.cs`
- action:
  - 将 `NavigateToPage(...)` 大方法迁移到独立 partial，集中承载页面切换主流程。
  - 保持逻辑路径、参数和行为不变，仅做代码归属迁移。
- effect:
  - `PaintOverlayWindow.Photo.Navigation.cs` 行数由 `557` 降到 `289`。
  - 导航入口、保存状态、切页主流程三层解耦，维护成本进一步下降。

### 2.23 Photo navigation decomposition batch 4 (mode/session lifecycle flow)

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.Mode.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.cs`
- action:
  - 将 photo 模式生命周期与会话收尾方法迁移到独立 partial：
    - `SetPhotoSequence`
    - `EnterPhotoMode` / `ExitPhotoMode`
    - `EnsurePhotoWindowedMode`
    - `SetPhotoInkCanvasUnbounded`
    - `EvictRuntimeInkCacheForClosedPhotoSession`
  - `Navigation.cs` 保留为空壳 partial，稳定文件路径与热点预算入口。
- effect:
  - `PaintOverlayWindow.Photo.Navigation.cs` 行数由 `289` 降到 `4`。
  - 导航命令、切页流程、保存状态、模式生命周期形成独立子域分层。

### 2.24 Ink export decomposition batch 4 (utility and manifest/fingerprint flow)

- new file:
  - `src/ClassroomToolkit.App/Ink/InkExportService.Utilities.cs`
- modified file:
  - `src/ClassroomToolkit.App/Ink/InkExportService.cs`
- action:
  - 将 `InkExportService` 末尾工具方法迁移到独立 partial：
    - `BuildExportFingerprint`
    - manifest 读写/键计算
    - `GetOrRenderPdfPage`
    - `AdaptStrokesForBackground` / `ScaleGeometryPath` / `CloneStroke`
  - 保持逻辑、签名与调用链不变，仅做归属迁移。
- effect:
  - `InkExportService.cs` 行数由 `1031` 降到 `715`。
  - 导出主流程与工具/指纹逻辑进一步解耦，维护可读性显著提升。

### 2.25 Ink export decomposition batch 5 (pdf/image export execution flow)

- new file:
  - `src/ClassroomToolkit.App/Ink/InkExportService.Exporting.cs`
- modified file:
  - `src/ClassroomToolkit.App/Ink/InkExportService.cs`
- action:
  - 将导出执行主方法迁移到独立 partial：
    - `ExportPdfFile`
    - `ExportImageFile`
  - 保持参数、调用链与行为一致，仅做代码归属迁移。
- effect:
  - `InkExportService.cs` 行数由 `715` 降到 `578`。
  - 主类文件进一步聚焦导出编排入口，执行细节独立维护。

### 2.26 Ink export decomposition batch 6 (cleanup and path utility flow)

- new file:
  - `src/ClassroomToolkit.App/Ink/InkExportService.Cleanup.cs`
- modified file:
  - `src/ClassroomToolkit.App/Ink/InkExportService.cs`
- action:
  - 将导出清理与路径辅助方法迁移到独立 partial：
    - `TryDeleteOutputFileSafe`
    - `CleanupCompositeOutputsForFilesWithoutInk`
    - `ListCompositeSourceFilesInDirectory`
    - `CleanupStaleCompositeOutputsForSource`
    - `PathsEqual` / `NormalizePathOrOriginal` / `GetFullPathOrOriginal`
    - `GetFilesSafely`
  - 保持逻辑与调用链不变，仅做归属迁移。
- effect:
  - `InkExportService.cs` 行数由 `578` 降到 `438`。
  - 导出主流程、执行流程、工具流程、清理流程分层完成度进一步提升。

### 2.27 Ink export decomposition batch 7 (composite indexing and output naming)

- new file:
  - `src/ClassroomToolkit.App/Ink/InkExportService.CompositeIndexing.cs`
- modified file:
  - `src/ClassroomToolkit.App/Ink/InkExportService.cs`
- action:
  - 将主文件中的“合成输出索引与命名解析”职责迁移到独立 partial：
    - `ListFilesWithCompositeExports`
    - `CleanupOrphanCompositeOutputsInDirectory`
    - `GetExistingOutputPaths`
    - `RemoveCompositeOutputsForPage`
    - `BuildOutputPath` / `TryResolveSourcePathFromCompositeName` / `TryGetPdfPageIndexFromCompositeName`
    - `IsPdf` / `ShouldSkipExport`
  - 保持行为与签名不变，仅做归属迁移与职责聚合。
- effect:
  - `InkExportService.cs` 行数由 `438` 降到 `143`（hotspot 实测）。
  - `InkExportService` 主文件聚焦“入口编排”，索引清理与命名规则独立维护。

### 2.28 Interop hook diagnostics de-duplication batch

- new file:
  - `src/ClassroomToolkit.Interop/Utilities/InteropHookDiagnostics.cs`
- modified file:
  - `src/ClassroomToolkit.Interop/Presentation/KeyboardHook.cs`
  - `src/ClassroomToolkit.Interop/Presentation/WpsSlideshowNavigationHook.cs`
- action:
  - 抽取重复的 hook 回调诊断逻辑（慢回调日志 + 异常节流日志）到共享工具。
  - 两个 hook 入口改为复用 `InteropHookDiagnostics`，保留原阈值（50ms / 30000ms）和原有事件行为。
- effect:
  - 删除重复诊断代码，降低双处维护风险。
  - 保持原有异常容忍与日志节流语义一致。

### 2.29 Resolver diagnostics split batch

- new file:
  - `src/ClassroomToolkit.Interop/Presentation/Win32PresentationResolver.Diagnostics.cs`
- modified file:
  - `src/ClassroomToolkit.Interop/Presentation/Win32PresentationResolver.cs`
- action:
  - 将 `Win32PresentationResolver` 的 DEBUG 诊断输出函数迁移到独立 partial：
    - `DebugPptWindowBeforeCheck`
    - `DebugOfficeCandidate`
    - `DebugFinalSelection`
  - 主文件仅保留解析与判定主流程，行为不变。
- effect:
  - `Win32PresentationResolver.cs` hotspot 行数从 `313` 降到 `292`。
  - 诊断代码与主业务判定分离，后续维护定位更清晰。

### 2.30 Interop hook P/Invoke split batch

- new file:
  - `src/ClassroomToolkit.Interop/Presentation/KeyboardHook.Interop.cs`
  - `src/ClassroomToolkit.Interop/Presentation/WpsSlideshowNavigationHook.Interop.cs`
- modified file:
  - `src/ClassroomToolkit.Interop/Presentation/KeyboardHook.cs`
  - `src/ClassroomToolkit.Interop/Presentation/WpsSlideshowNavigationHook.cs`
- action:
  - 将两个 Hook 类的 Win32 委托/结构体/PInvoke 声明迁移到独立 partial。
  - 主文件保留生命周期、事件分发和拦截逻辑，不变更外部行为。
- effect:
  - `KeyboardHook.cs` hotspot 行数从 `309` 降到 `289`。
  - `WpsSlideshowNavigationHook.cs` hotspot 行数从 `312` 降到 `279`。
  - Interop 边界更清晰，后续维护可在“行为逻辑/平台绑定”之间独立变更。

### 2.31 Resolver native helper split batch

- new file:
  - `src/ClassroomToolkit.Interop/Presentation/Win32PresentationResolver.Native.cs`
- modified file:
  - `src/ClassroomToolkit.Interop/Presentation/Win32PresentationResolver.cs`
- action:
  - 将 `Win32PresentationResolver` 的窗口类名/进程信息/全屏判定等原生辅助方法迁移到独立 partial：
    - `BuildClassNames` / `BuildWindowInfo`
    - `HasCaption` / `IsFullscreenWindow`
    - `AddClassName` / `GetProcessId` / `GetProcessName`
  - 主文件保留解析主流程与评分策略，不变更行为。
- effect:
  - `Win32PresentationResolver.cs` hotspot 行数从 `292` 降到 `199`。
  - 解析策略与原生探测细节解耦，后续优化边界更明确。

### 2.32 Brush geometry entry split batch

- new file:
  - `src/ClassroomToolkit.App/Paint/Brushes/VariableWidthBrushRenderer.Geometry.Entry.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/Brushes/VariableWidthBrushRenderer.Geometry.cs`
- action:
  - 将几何入口与预览缓存相关方法迁移到独立 partial：
    - `GetLastStrokeGeometry`
    - `GetLastRibbonGeometries`
    - `GetLastCoreGeometry`
    - `GetPreviewCoreGeometry`
    - `BuildPreviewGeometryForRange`
    - `CopyRangeToPreviewSliceBuffer`
    - `GetInkBloomGeometries`
    - `GetRibbonOpacity`
    - `GenerateGeometry`
    - `BuildRibbonGeometries`
  - 保持行为不变，仅做职责归属拆分。
- effect:
  - `VariableWidthBrushRenderer.Geometry.cs` hotspot 行数从 `1217` 降到 `1057`。
  - 大文件的“入口编排/核心几何算法”边界更清晰，后续可继续分批拆分。

### 2.33 Brush geometry caps/corners split batch

- new file:
  - `src/ClassroomToolkit.App/Paint/Brushes/VariableWidthBrushRenderer.Geometry.CapsAndCorners.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/Brushes/VariableWidthBrushRenderer.Geometry.cs`
- action:
  - 将路径闭合、笔锋端帽与转角修正相关方法迁移到独立 partial：
    - `BuildStrokePathV10`
    - `BuildCapData`
    - `ClampTipLength`
    - `ComputePressureDropRate`
    - `AddCapV13` / `AddRoundedCapArc`
    - `AddCornerReinforcement` / `AddCornerArc`
    - `GetNormalFromVector`
    - `ResolveEllipticalNibRadius`
  - 保持行为不变，仅按职责拆分。
- effect:
  - `VariableWidthBrushRenderer.Geometry.cs` hotspot 行数从 `1057` 降到 `835`。
  - 几何主文件已从初始 `1217` 连续降到 `835`，复杂几何入口与端帽/转角算法实现解耦。

### 2.34 Brush geometry sampling split batch

- new file:
  - `src/ClassroomToolkit.App/Paint/Brushes/VariableWidthBrushRenderer.Geometry.Sampling.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/Brushes/VariableWidthBrushRenderer.Geometry.cs`
- action:
  - 将中心线采样/重采样/端点 taper/速度宽度估算链路迁移到独立 partial：
    - `BuildCenterlineSamplesFinal`（两重载）
    - `ResampleByArcLength`
    - `InterpolateStrokePoint`
    - `ApplyEndpointTaper`
    - `ResolveTaperFactor`
    - `BuildCenterlineSamplesV10`
    - `ResolveUpsampleSteps`
    - `ResolveCurvatureFactor`
    - `ResolveCornerAngleDegrees`
    - `CalculateWidthV10`
    - `SmoothWidthsV10`
  - 主文件保留“ribbon 形态与边缘构建”核心流程，不改变行为。
- effect:
  - `VariableWidthBrushRenderer.Geometry.cs` hotspot 行数从 `835` 降到 `367`。
  - 几何模块已形成 `Entry / Sampling / CapsAndCorners / Core` 分层，后续维护与回归定位成本显著下降。

### 2.35 Photo transform persistence split batch

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.Persistence.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.cs`
- action:
  - 将窗口尺寸/窗口状态恢复、变换状态缓存、debounce 保存与统一广播逻辑迁移到独立 partial：
    - `OnWindowSizeChanged` / `OnWindowStateChanged`
    - `PhotoTransformState`
    - `GetCurrentPhotoTransformKey` / `TryApplyStoredPhotoTransform`
    - `SavePhotoTransformState`
    - `SchedulePhotoTransformSave` / `FlushPhotoTransformSave`
    - `SchedulePhotoUnifiedTransformSave`
    - `OnPhotoTransformSaveTimerTick` / `OnPhotoUnifiedTransformSaveTimerTick`
  - 主文件聚焦实时变换与几何映射流程。
- effect:
  - `PaintOverlayWindow.Photo.Transform.cs` hotspot 行数从 `454` 降到 `244`。
  - 交互变换与持久化/恢复分层明确，后续定位与改动成本降低。

### 2.36 Overlay state-field split batch

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.State.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs`
- action:
  - 将 `PaintOverlayWindow` 的常量、运行时状态字段、事件与会话只读属性迁移到独立 partial。
  - 主文件保留构造、生命周期与行为方法，不改动外部行为。
- effect:
  - `PaintOverlayWindow.xaml.cs` hotspot 行数从 `798` 降到 `569`。
  - 状态承载与行为逻辑分离，类文件可读性明显提升。

### 2.37 Paint settings dialog state split batch

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.State.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs`
- action:
  - 将选项常量表、预设提示、记录结构、公开设置属性及内部状态字段迁移到独立 partial。
  - 主文件保留构造与事件处理流程，不改变对话框行为。
- effect:
  - `PaintSettingsDialog.xaml.cs` hotspot 行数从 `696` 降到 `479`。
  - 配置承载与交互逻辑解耦，后续维护更聚焦。

### 2.38 Image manager state split batch

- new file:
  - `src/ClassroomToolkit.App/Photos/ImageManagerWindow.State.cs`
- modified file:
  - `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs`
- action:
  - 将 `ImageManagerWindow` 的常量、运行时字段、公开事件与 `ViewModel` 属性迁移到独立 partial。
  - 主文件保留构造与业务流程方法，不改变行为。
- effect:
  - `ImageManagerWindow.xaml.cs` hotspot 行数从 `407` 降到 `373`。
  - 状态承载与流程代码分离，窗口行为文件更易读、更易定位问题。

### 2.39 Overlay session/tools split batch

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.SessionAndTools.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs`
- action:
  - 将会话调度、会话映射、工具参数入口、清空流程与运行态空页守卫迁移到独立 partial：
    - `DispatchSessionEvent`
    - `ApplySessionOverlayTopmost` / `EnsureOverlayTopmost`
    - `ApplySessionNavigationMode` / `ApplySessionInkVisibility` / `ApplySessionWidgetVisibility`
    - `MapSessionToolMode` / `MapPresentationSource` / `MapPresentationForegroundSource` / `MapPhotoSource`
    - `LogSessionTransition`
    - `UpdateCursor` / `SetBrush` / `SetEraserSize` / `SetShapeType`
    - `ClearAll` / `ClearPhotoInkStateAfterClearAll`
    - `TryEnforceRuntimeEmptyGuardForCurrentPage`
    - 公开只读属性：`CurrentBrushColor`、`CurrentBrushOpacity`、`CurrentDocumentName`、`CurrentDocumentPath`、`CurrentPhotoFileType`、`CurrentCourseDate`、`CurrentPageIndex`
  - 主文件保留构造与窗口生命周期主流程，不改变行为。
- effect:
  - `PaintOverlayWindow.xaml.cs` hotspot 行数从 `569` 降到 `347`。
  - 主文件结构进一步聚焦，Session/Tool 逻辑独立维护。

### 2.40 Image manager loading-flow split batch

- new file:
  - `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Loading.cs`
- modified file:
  - `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs`
- action:
  - 将图片加载与缩略图调度链路迁移到独立 partial：
    - `OnThumbnailSizeChanged` / `OnThumbnailRefreshDebounceTick`
    - `StartLoadImages` / `LoadImagesAsync` / `ShowEmptyState`
    - `QueueThumbnailLoad` / `TryDispatchThumbnailUpdateAsync`
  - 主文件保留窗口构造、目录树初始化与导航接口。
- effect:
  - `ImageManagerWindow.xaml.cs` hotspot 行数从 `373` 降到 `164`。
  - 主流程与异步加载链路解耦，阅读与定位更直接。

### 2.41 Image manager tree/navigation split batch

- new file:
  - `src/ClassroomToolkit.App/Photos/ImageManagerWindow.TreeNavigation.cs`
- modified file:
  - `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs`
- action:
  - 将目录树初始化与导航缓存逻辑迁移到独立 partial：
    - `InitializeTreeAsync`
    - `TryNavigate`
    - `GetNavigableItems` / `GetNavigablePaths`
    - `CreateFolderPathSnapshot`
  - 主文件收敛为构造与绑定入口。
- effect:
  - `ImageManagerWindow.xaml.cs` hotspot 行数从 `164` 进一步降到 `50`。
  - `ImageManagerWindow` 分层形成：`State / Loading / TreeNavigation / Main`。

### 2.42 Paint settings interactions split batch

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.Interactions.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs`
- action:
  - 将对话框加载/关闭、确认/取消、恢复默认值等交互方法迁移到独立 partial。
  - 保持调用链和交互行为不变，仅做代码归属重整。
- effect:
  - `PaintSettingsDialog.xaml.cs` hotspot 行数从 `479` 降到 `232`。
  - `PaintSettingsDialog` 形成 `State / Interactions / Main` 分层，后续维护成本下降。

### 2.43 Contract tests source aggregation hardening batch

- modified files:
  - `tests/ClassroomToolkit.Tests/PaintSettingsDialogSizeToContentContractTests.cs`
  - `tests/ClassroomToolkit.Tests/InteropHookEventDispatchContractTests.cs`
  - `tests/ClassroomToolkit.Tests/InteropHookLifecycleContractTests.cs`
- action:
  - 将契约测试从“单文件硬编码读取”升级为“按 `*.cs` partial 聚合读取”。
  - 保留原契约语义（关键代码片段必须存在），同时消除对文件名/落点耦合。
- effect:
  - 支持后续模块化拆分而不引入伪回归。
  - 契约测试更贴近“行为约束”，更符合长期重构目标。

### 2.44 Keyboard hook lifecycle/callback split batch

- new files:
  - `src/ClassroomToolkit.Interop/Presentation/KeyboardHook.Lifecycle.cs`
  - `src/ClassroomToolkit.Interop/Presentation/KeyboardHook.Callback.cs`
- modified file:
  - `src/ClassroomToolkit.Interop/Presentation/KeyboardHook.cs`
- action:
  - 将 `KeyboardHook` 生命周期（Start/Stop/Dispose/SetHook）与回调/修饰键逻辑迁移到独立 partial。
  - 主文件收敛为字段与对外契约入口；行为与 Win32 交互语义不变。
- effect:
  - `KeyboardHook.cs` hotspot 行数从 `289` 降到 `30`。
  - Interop 高风险逻辑按职责分层，定位与维护效率显著提升。

### 2.45 WPS navigation hook lifecycle/callback split batch

- new files:
  - `src/ClassroomToolkit.Interop/Presentation/WpsSlideshowNavigationHook.Lifecycle.cs`
  - `src/ClassroomToolkit.Interop/Presentation/WpsSlideshowNavigationHook.Callbacks.cs`
- modified files:
  - `src/ClassroomToolkit.Interop/Presentation/WpsSlideshowNavigationHook.cs`
  - `tests/ClassroomToolkit.Tests/InteropHookLifecycleContractTests.cs`
- action:
  - 将 `WpsSlideshowNavigationHook` 生命周期（Start/Stop/Dispose）与回调分发（Keyboard/Mouse/Queue）迁移到独立 partial。
  - 契约测试升级为聚合读取 `WpsSlideshowNavigationHook*.cs`，保留原顺序与安全约束检查。
- effect:
  - `WpsSlideshowNavigationHook.cs` hotspot 行数从 `279` 降到 `47`。
  - Interop 导航钩子形成 `core + lifecycle + callbacks + interop` 分层，后续演进更稳定。

### 2.46 Overlay navigation focus policy branch de-dup batch

- modified file:
  - `src/ClassroomToolkit.App/Windowing/OverlayNavigationFocusPolicy.cs`
- action:
  - 收敛 `ResolveActivateDecision/ResolveKeyboardFocusDecision` 中重复的 guard-reason 映射分支。
  - 新增 `MapBlockedActivateReason` 与 `MapBlockedKeyboardFocusReason` 两个私有映射函数，保持策略语义不变。
- effect:
  - 降低重复分支维护风险，后续新增 guard reason 时只需单点调整映射逻辑。
  - 行为与现有测试断言保持一致。

### 2.47 Overlay window lifecycle split batch

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Lifecycle.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs`
- action:
  - 将窗口生命周期与监控回调方法迁移到独立 partial：
    - `OnOverlayLoaded`
    - `OnPresentationFocusMonitorTick`
    - `OnInkMonitorTick`
    - `OnInkSidecarAutoSaveTimerTick`
    - `OnOverlayVisibleChanged`
    - `OnOverlaySourceInitialized`
    - `OnOverlayDeactivated`
    - `OnOverlayClosed`
  - 主文件保留构造与 `SetMode` 主入口，行为不变。
- effect:
  - `PaintOverlayWindow.xaml.cs` hotspot 行数从 `347` 降到 `241`。
  - `PaintOverlayWindow` 形成 `State / SessionAndTools / Lifecycle / Main` 分层。

### 2.48 Resolver scoring split batch

- new file:
  - `src/ClassroomToolkit.Interop/Presentation/Win32PresentationResolver.Scoring.cs`
- modified file:
  - `src/ClassroomToolkit.Interop/Presentation/Win32PresentationResolver.cs`
- action:
  - 将 `BuildWindowCheck`（候选判定与评分）迁移到独立 partial。
  - 主文件收敛为 resolver 入口与窗口枚举编排，不改变评分语义。
- effect:
  - `Win32PresentationResolver.cs` hotspot 行数从 `199` 降到 `148`。
  - 候选评分与枚举流程解耦，后续调参与回归定位更清晰。

### 2.49 Overlay input cross-page continuation split batch

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.CrossPage.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.cs`
- action:
  - 将跨页输入续写与补点插值逻辑迁移到独立 partial：
    - `ResumeCrossPageInputOperationAfterSwitch`
    - `AppendCrossPageContinuationSamples`
  - 主输入文件保留输入采样与捕获释放入口，行为不变。
- effect:
  - `PaintOverlayWindow.Input.cs` hotspot 行数从 `219` 降到 `129`。
  - 输入主流程与跨页续写算法分层，后续维护和定位更清晰。

### 2.50 Photo transform geometry/matrix split batch

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.Geometry.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.cs`
- action:
  - 将照片坐标变换与几何映射方法迁移到独立 partial：
    - `ToPhotoSpace`
    - `ToPhotoGeometry`
    - `ToScreenGeometry`
    - `GetPhotoMatrix`
    - `GetPhotoInverseMatrix`
  - 主文件保留缩放与交互刷新编排，行为不变。
- effect:
  - `PaintOverlayWindow.Photo.Transform.cs` hotspot 行数从 `244` 降到 `169`。
  - 变换编排与几何换算分层，后续定位和优化更直接。

### 2.51 Overlay mode-switch flow split batch

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Mode.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs`
- action:
  - 将 `SetMode` 工具模式切换主流程迁移到独立 partial。
  - 主文件收敛为构造与基础入口，模式切换子域独立维护。
- effect:
  - `PaintOverlayWindow.xaml.cs` hotspot 行数从 `241` 降到 `159`。
  - 结构清晰度提升，后续模式策略优化更易局部变更。

### 2.52 Overlay navigation focus contracts split batch

- new file:
  - `src/ClassroomToolkit.App/Windowing/OverlayNavigationFocusPolicy.Contracts.cs`
- modified file:
  - `src/ClassroomToolkit.App/Windowing/OverlayNavigationFocusPolicy.cs`
- action:
  - 将 `OverlayNavigationFocusPolicy` 中的 `record/enum` 契约定义迁移到独立 contracts 文件。
  - 策略实现文件仅保留决策逻辑与映射函数，行为不变。
- effect:
  - `OverlayNavigationFocusPolicy.cs` hotspot 行数从 `192` 降到 `160`。
  - 契约定义与策略实现分离，后续扩展/维护冲突面更小。

### 2.53 Photo transform zoom-flow split batch

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.Zoom.cs`
- modified file:
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.cs`
- action:
  - 将缩放输入、缩放归一化、惯性档位更新、缩放应用流程迁移到独立 partial：
    - `ZoomPhoto`
    - `ZoomPhotoByFactor`
    - `ApplyPhotoZoomInput`
    - `UpdatePhotoZoomTuning`
    - `UpdatePhotoInertiaProfile`
    - `ApplyPhotoScale`
  - 主文件保留视口步进与墨迹补偿同步逻辑，行为不变。
- effect:
  - `PaintOverlayWindow.Photo.Transform.cs` hotspot 行数从 `169` 降到 `96`。
  - 变换模块形成 `Persistence / Geometry / Zoom / Core` 分层。

### 2.54 Resolver resolution-flow split batch

- new file:
  - `src/ClassroomToolkit.Interop/Presentation/Win32PresentationResolver.Resolution.cs`
- modified file:
  - `src/ClassroomToolkit.Interop/Presentation/Win32PresentationResolver.cs`
- action:
  - 将解析主流程迁移到独立 partial：
    - `ResolvePresentationTarget`
    - `ResolveFullscreenPresentationTarget`
  - 主文件保留 `UpdateScoringOptions/ResolveForeground/CheckWindow` 等轻量入口。
- effect:
  - `Win32PresentationResolver.cs` hotspot 行数从 `148` 降到 `33`。
  - 解析编排、评分策略、原生探测三层边界进一步清晰。

### 2.55 Paint settings constructor initialization split batch

- new file:
  - `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.Initialization.cs`
- modified files:
  - `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs`
  - `tests/ClassroomToolkit.Tests/PaintSettingsDialogPresentationControlContractTests.cs`
- action:
  - 将构造函数中的大段设置装配逻辑迁移到独立 partial `InitializeFromSettings`。
  - 构造函数保留窗口初始化、边框修复与只读字段 `_presetRecommendation` 赋值。
  - 同步把展示控制契约测试升级为 `PaintSettingsDialog*.cs` 聚合读取，消除路径耦合。
- effect:
  - `PaintSettingsDialog.xaml.cs` hotspot 行数从 `232` 降到 `25`。
  - `PaintSettingsDialog` 形成 `State / Initialization / Interactions / Main` 分层。

### 2.56 Overlay navigation focus mapping split batch

- new file:
  - `src/ClassroomToolkit.App/Windowing/OverlayNavigationFocusPolicy.Mapping.cs`
- modified file:
  - `src/ClassroomToolkit.App/Windowing/OverlayNavigationFocusPolicy.cs`
- action:
  - 将 blocked-reason 映射函数迁移到独立 mapping partial，策略主文件仅保留决策流程。
  - 行为保持不变。
- effect:
  - `OverlayNavigationFocusPolicy.cs` hotspot 行数从 `160` 降到 `136`。
  - 策略流程与映射细节彻底分层。

### 2.57 Contract source aggregation helper batch

- new file:
  - `tests/ClassroomToolkit.Tests/ContractSourceAggregationHelper.cs`
- modified files:
  - `tests/ClassroomToolkit.Tests/PaintSettingsDialogSizeToContentContractTests.cs`
  - `tests/ClassroomToolkit.Tests/PaintSettingsDialogPresentationControlContractTests.cs`
  - `tests/ClassroomToolkit.Tests/InteropHookEventDispatchContractTests.cs`
  - `tests/ClassroomToolkit.Tests/InteropHookLifecycleContractTests.cs`
- action:
  - 抽取契约测试中重复的 partial 源文件聚合读取逻辑到公共 helper：
    - `ReadSourcesInDirectory(directorySegments, pattern)`
  - 4 个契约测试统一改为复用 helper，去除重复目录拼接与读取代码。
- effect:
  - 降低契约测试基础设施重复实现，后续扩展 partial 聚合测试更稳定。
  - 不改变任何契约断言语义。

## 3) Verification

本批次执行后全门禁通过：

1. `dotnet build ClassroomToolkit.sln -c Debug` -> PASS
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` -> PASS (`3213/3213`)
3. 契约过滤测试 -> PASS (`25/25`)
4. hotspot 脚本 -> PASS

新增 Infra 抽取后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`）。

新增 Ink/Overlay 大文件拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 batch 2 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 InkExport 指纹辅助拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增跨页区域擦除拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增形状流程拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增擦除核心流程拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 manifest utilities 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 stroke recording 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增擦除核心拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增笔迹记录拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增上下文生命周期拆分并修复后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 InkExport 渲染流程拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 MainWindow 生命周期拆分并同步更新契约测试后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 MainWindow Z-order 拆分并更新聚合源码读取后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 MainWindow Roll-call 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 CrossPage bounds 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS；中途一次契约命令受进程锁影响，重跑通过）。

新增 CrossPage neighbor-ink helper 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS；并行阶段一次 build 受编译进程锁影响，串行重跑通过）。

新增 CrossPage rendering entry 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 Photo navigation commands 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS；并行阶段一次 full-test 受进程锁影响，串行重跑通过）。

新增 Photo navigation save-state 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS；并行阶段一次 full-test 出现临时 WPF 编译态异常，串行重跑通过）。

新增 Photo navigation navigate-flow 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS；并行阶段一次 full-test 出现临时 WPF 生成态异常，串行重跑通过）。

新增 Photo navigation mode/session 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS；并行阶段一次 build 受编译进程锁影响，串行重跑通过）。

新增 Ink export utility 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 Ink export execution-flow 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 Ink export cleanup-flow 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS；并行阶段一次 full-test 出现临时 WPF 生成态异常，串行重跑通过）。

新增 Ink export composite-indexing 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 Interop hook diagnostics 去重后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS；并行阶段一次 build 出现临时 WPF 生成态异常，串行重跑通过）。

新增 resolver diagnostics 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 hook P/Invoke 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS；并行阶段一次 build 出现临时 WPF 生成态异常，串行重跑通过）。

新增 resolver native helper 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 brush geometry entry 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 brush geometry caps/corners 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 brush geometry sampling 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 photo transform persistence 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS；拆分后一次 build 发现缺失 `SafeActionExecutionExecutor` 命名空间引用，补齐后通过）。

新增 overlay state-field 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS；拆分后一次 build 发现缺失 `LatestOnlyAsyncGate/PdfDocumentHost` 命名空间引用，补齐后通过）。

新增 paint settings dialog state 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 image manager state 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 overlay session/tools 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS；拆分后一次 build 发现缺失 `PresentationForegroundSource` 命名空间引用，补齐后通过）。

新增 image manager loading-flow 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 image manager tree/navigation 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS；拆分后一次 build 发现缺失 `SafeActionExecutionExecutor` 命名空间引用，补齐后通过）。

新增 paint settings interactions 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS；拆分后首次 full-test 因契约测试单文件耦合失败，改为 partial 聚合读取后通过）。

新增 interop 契约测试聚合硬化与 keyboard hook lifecycle/callback 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS；拆分后首次 build 缺少 `VirtualKey/KeyModifiers` using，补齐后通过）。

新增 WPS navigation hook lifecycle/callback 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 overlay navigation focus policy 分支去重复后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 overlay window lifecycle 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 resolver scoring 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 overlay input cross-page continuation 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS；拆分后一次 build 因 `BrushInputSample` using 误配失败，修正命名空间后通过）。

新增 photo transform geometry/matrix 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 overlay mode-switch flow 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS；拆分后一次 build 因命名空间与 `Cursors` 二义性失败，补齐 `Windowing` 引用并改完全限定名后通过）。

新增 overlay navigation focus contracts 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 photo transform zoom-flow 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 resolver resolution-flow 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 paint settings constructor initialization 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS；拆分后一次 build 命中只读字段赋值约束，调整 `_presetRecommendation` 回构造函数赋值后通过；一次 full-test 命中契约单文件路径耦合，升级聚合读取后通过）。

新增 overlay navigation focus mapping 拆分后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

新增 contract source aggregation helper 后再次执行全门禁，结果仍为 PASS（`3213/3213`，契约 `25/25`，hotspot PASS）。

## 4) Boundary Notes

- 本批次变更仅在：
  - `src/ClassroomToolkit.App/Windowing/*`
  - `tests/ClassroomToolkit.Tests/*ContractTests*`
  - `docs/validation/evidence/*`
- 未引入跨层依赖，也未修改 Domain/Infra 外部契约。

## 5) Next Phase Candidate

下一批建议进入：
1. `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.cs`（`244` 行）评估输入变换与显示同步进一步拆分。
2. `src/ClassroomToolkit.App/Windowing/OverlayNavigationFocusPolicy.cs`（`192` 行）继续收敛策略路径并保持行为契约。
3. `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.cs` 中持久化与实时变换边界继续细化并补契约测试。
