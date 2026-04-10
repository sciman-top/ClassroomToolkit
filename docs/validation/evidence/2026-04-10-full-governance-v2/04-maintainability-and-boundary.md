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

## 4) Boundary Notes

- 本批次变更仅在：
  - `src/ClassroomToolkit.App/Windowing/*`
  - `tests/ClassroomToolkit.Tests/*ContractTests*`
  - `docs/validation/evidence/*`
- 未引入跨层依赖，也未修改 Domain/Infra 外部契约。

## 5) Next Phase Candidate

下一批建议进入：
1. `src/ClassroomToolkit.App/Ink/InkExportService.cs`（`1062` 行）分段拆分计划。
2. `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs`（`913` 行）职责切分计划。
3. 对应新增“行为不变契约测试”后再实施拆分。
