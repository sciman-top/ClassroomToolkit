# ClassroomToolkit 系统化优化任务清单

## 目标

在不破坏课堂可用性、触屏优先交互、Interop 降级、`students.xlsx`、`student_photos/`、`settings.ini` 兼容性的前提下，按小批次提升健壮性、性能、可维护性和规范化程度。

## 当前基线

- `dotnet build ClassroomToolkit.sln -c Debug`: PASS，0 warning，0 error。
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`: PASS，3476 tests。
- contract/invariant filter: PASS，28 tests。
- `scripts/quality/check-hotspot-line-budgets.ps1`: PASS。
- `scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug`: PASS。
- `dotnet format whitespace ClassroomToolkit.sln --verify-no-changes --verbosity minimal`: PASS。
- `tests/.tmp` 当前目录数：11934（已由 repo 内测试自清理开始回收，审查时为 31846）。

## 执行原则

- 先修能被门禁证明的低风险改进，再进入大范围重构。
- 性能改动必须先有测量或明确 hot path，不做无证据微优化。
- 格式归一化、行为修复、性能优化、模块拆分分开提交。
- 每批完成后按 `build -> test -> contract/invariant -> hotspot` 收口。

## Task 1: App analyzer 纳入常规 build（已完成）

**Description:** 将 `ClassroomToolkit.App` 加入现有 `EnableNETAnalyzers=true` 的生产项目范围，避免 UI 层只依赖额外 analyzer backlog 脚本兜底。

**Acceptance criteria:**
- [x] 常规 build 对 App 生产项目启用 .NET analyzers。
- [x] Debug build 仍 0 warning / 0 error。
- [x] full tests、contract、hotspot、standard quality gate 均通过。

**Verification:**
- [x] `dotnet build ClassroomToolkit.sln -c Debug`
- [x] `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- [x] contract/invariant filter
- [x] `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug`

## Task 2: 格式与行尾归一化独立批次（已完成）

**Description:** 处理 `dotnet format --verify-no-changes` 暴露的大量 whitespace / end-of-line 漂移。该批次只做机械格式化，不混入逻辑重构。

**Acceptance criteria:**
- [x] `dotnet format whitespace ClassroomToolkit.sln --verify-no-changes --verbosity minimal` 通过。
- [x] diff 仅包含格式、缩进、行尾变化。
- [x] hard gate 与 standard quality gate 通过。

**Risk:** 中。行为风险低，但 diff 面大，必须单独批次便于回滚。

**Verification:**
- [x] `dotnet format whitespace ClassroomToolkit.sln --verbosity minimal`
- [x] `dotnet build ClassroomToolkit.sln -c Debug`
- [x] `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- [x] contract/invariant filter
- [x] `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug`

## Task 3: 超长 App 文件拆分与职责收敛（已完成第六阶段）

**Description:** 优先审查 `PaintToolbarWindow.xaml.cs`、`VariableWidthBrushRenderer.cs`、`RollCallSettingsDialog.xaml.cs`、`PaintOverlayWindow.*` 和 `ImageManagerWindow.Navigation.cs`。只拆已有职责边界，不引入新框架或猜测式抽象。

**Acceptance criteria:**
- [x] 每次只处理一个文件族。
- [x] 拆分后公开行为不变，相关定向测试通过。
- [x] hotspot 复核记录拆分原因、调用链和回滚点。

**Verification:**
- [x] `dotnet build ClassroomToolkit.sln -c Debug`
- [x] `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- [x] contract/invariant filter
- [x] `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
- [x] `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug`

**Implemented scope:**
- 首批仅处理 `RollCallSettingsDialog` 文件族。
- 将语音页相关逻辑拆分到 `src/ClassroomToolkit.App/RollCallSettingsDialog.Speech.cs`。
- `src/ClassroomToolkit.App/RollCallSettingsDialog.xaml.cs` 从 977 行降到 716 行。
- 删除主文件中已无调用链的私有死代码，避免未来维护时在同一文件中混杂“现役逻辑”和“历史残留 helper”。
- 第二批处理 `PaintToolbarWindow` 文件族。
- 将窗口拖拽、触控拖拽、区域截图恢复、辅助输入转发拆分到 `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.Pointer.cs`。
- `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs` 从 1163 行降到 874 行。
- 同步把 `PaintToolbarDragModeContractTests` 调整为按文件族聚合校验，保持 managed capture contract 不变。
- 第三批处理 `VariableWidthBrushRenderer` 文件族。
- 将 move telemetry snapshot、环境开关与 telemetry ring-buffer 聚合逻辑拆分到 `src/ClassroomToolkit.App/Paint/Brushes/VariableWidthBrushRenderer.Telemetry.cs`。
- `src/ClassroomToolkit.App/Paint/Brushes/VariableWidthBrushRenderer.cs` 从 1080 行降到 859 行。
- 保持几何、采样与笔刷物理计算逻辑不动，只收敛诊断/遥测职责。
- 第四批处理 `PaintOverlayWindow.Photo.Transform` 文件族。
- 将照片平移惯性的速度采样、release velocity、`CompositionTarget.Rendering` loop 与停止清理逻辑拆分到 `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.Inertia.cs`。
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.PanInertia.cs` 当前为 266 行，新增 `PaintOverlayWindow.Photo.Transform.Inertia.cs` 200 行。
- 保持 pan begin/update/end、bounds clamp、跨页刷新、ink redraw 与惯性策略参数不动，只收敛照片平移惯性职责边界。
- 第五批处理 `PaintOverlayWindow.Presentation` 文件族。
- 将 WPS hook 请求、WPS navigation fallback、hook runtime state、不可用通知和 WPS debounce 状态拆分到 `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Presentation.WpsHook.cs`。
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Presentation.cs` 当前为 657 行，新增 `PaintOverlayWindow.Presentation.WpsHook.cs` 342 行。
- 同步把 WPS hook 与 foreground ownership 的源码 contract 调整为按 `PaintOverlayWindow.Presentation*.cs` 文件族聚合校验，保持契约意图不变。
- 第六批处理 `PaintOverlayWindow.Ink.Rendering` 文件族。
- 将 redraw 调度执行路径收敛到 `RunPendingInkRedraw()`，避免 throttled/direct 分支重复维护 pending stamp、version check、redraw、completion sync 和 diagnostics callback。
- 将 stored stroke、ribbon、bloom 的 photo-transform geometry 决策收敛到 `ResolveStoredInkRenderGeometry()`。
- 将 draw command、pen cache key、brush/pen cache helper、opacity packing 和 layer-step helper 拆分到 `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Rendering.Cache.cs`。
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Rendering.cs` 从 874 行降到 759 行，新增 cache partial 124 行。
- 同步扩展 `PaintOverlayInkRedrawTelemetryContractTests`，锁定共享 redraw 执行路径和 rendering cache partial 边界。

**Follow-up:**
- [x] 下一刀继续 `PaintOverlayWindow.*` 中最肥的 rendering 或 cross-page 子文件族。
- [x] 再下一刀评估 `ImageManagerWindow` 或 `RollCallWindow` 的状态/窗口编排子文件族。
- [ ] `RollCallSettingsDialog` 若后续继续拆分，只再拆一层通用 tab-state/default-apply helper，不引入新抽象。

### Task 3: 第七批 ImageManagerWindow 导航/选择职责拆分 `[Done 2026-06-14]`

**Description:** 继续处理 `ImageManagerWindow` 文件族，将 `ImageManagerWindow.Navigation.cs` 中混合的目录/收藏导航与选择/多选/预览逻辑拆分到独立 partial，降低热点文件冲突面与认知负担。

**Acceptance criteria:**
- [x] `ImageManagerWindow.Navigation.cs` 不再同时承载收藏/最近与多选/预览两类职责
- [x] 收藏/最近回调、单击打开、多选删除等现有行为与合同语义保持不变
- [x] 相关合同测试改为按 `ImageManagerWindow*.cs` 文件族聚合校验，避免后续继续拆分时反复改测试入口

**Verification:**
- [x] `dotnet build ClassroomToolkit.sln -c Debug -p:UseSharedCompilation=false`
- [x] `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseSharedCompilation=false`
- [x] contract/invariant filter
- [x] `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`

**Implemented scope:**
- 新增 `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Favorites.cs`，承接收藏/最近、目录收藏对话框与安全回调逻辑。
- 新增 `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Selection.cs`，承接列表选择、长按多选、删除、多选状态与预览打开逻辑。
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Navigation.cs` 收口为默认目录、树选择、路径输入、前进/后退/上级与目录解析。
- `tests/ClassroomToolkit.Tests/ImageManagerTouchFlowContractTests.cs` 改为按 `ImageManagerWindow*.cs` 文件族聚合读取源码，保持合同意图不变。
- `ImageManagerWindow.Navigation.cs` 从 861 行降到 289 行；最大热点转移到 `ImageManagerWindow.Selection.cs` 406 行，仍在预算内。

### Task 3: 第八批 MainWindow.Photo image-manager 协调拆分 `[Done 2026-06-14]`

**Description:** 将 `MainWindow.Photo.cs` 中的 image-manager 打开、关闭、状态同步和收藏/最近回调逻辑拆到独立 partial，保留照片进入、导航、焦点和前台 retouch 主线在原文件中。

**Acceptance criteria:**
- [x] `MainWindow.Photo.cs` 不再同时承载 image-manager 协调和 photo navigation/focus 两类职责
- [x] `OnImageSelected`、`ApplyPhotoOverlayEntry`、`OnPhotoNavigateRequested`、`FocusOverlayForPhotoNavigation` 的行为与源码合同语义保持不变
- [x] 依赖 `MainWindow.Photo.cs` 的源码合同测试改为按 `MainWindow.Photo*.cs` 文件族聚合校验

**Verification:**
- [x] `dotnet build ClassroomToolkit.sln -c Debug -p:UseSharedCompilation=false`
- [x] `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseSharedCompilation=false`
- [x] contract/invariant filter
- [x] `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`

**Implemented scope:**
- 新增 `src/ClassroomToolkit.App/MainWindow.Photo.ImageManager.cs`，承接 image-manager 创建、打开、关闭、state-change、收藏/最近、show-ink 同步和布局回调。
- `src/ClassroomToolkit.App/MainWindow.Photo.cs` 收口为 photo overlay entry、photo navigation、photo mode change、overlay focus 与 foreground retouch。
- `tests/ClassroomToolkit.Tests/MainWindowPhotoFocusDispatchContractTests.cs` 改为按 `MainWindow.Photo*.cs` 文件族聚合校验。
- `tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs` 改为按 `MainWindow.Photo*.cs` 文件族聚合校验 photo-source 片段。
- `MainWindow.Photo.cs` 从 573 行降到 438 行；新增 `MainWindow.Photo.ImageManager.cs` 146 行，热点仍显著低于预算。

### Task 3: 第九批 PaintSettingsDialog section-state/defaults 拆分 `[Done 2026-06-14]`

**Description:** 继续压缩 `PaintSettingsDialog` 复杂度，将 `PaintSettingsDialog.SectionState.cs` 中混合的脏状态跟踪与“全部恢复默认”逻辑拆到独立 partial，保留 section snapshot/apply 主线在原文件。

**Acceptance criteria:**
- [x] `PaintSettingsDialog.SectionState.cs` 不再同时承载 section snapshot/apply、dirty tracking 和 defaults reset 三类职责
- [x] `PaintSettingsDialog` 的源码合同仍按 `PaintSettingsDialog*.cs` 文件族聚合成立，无需新增单文件脆弱依赖
- [x] 对话框构造、默认值恢复和 section dirty tracking 行为保持不变

**Verification:**
- [x] `dotnet build ClassroomToolkit.sln -c Debug -p:UseSharedCompilation=false`
- [x] `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseSharedCompilation=false`
- [x] contract/invariant filter
- [x] `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`

**Implemented scope:**
- 新增 `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.SectionDirtyTracking.cs`，承接控件 dirty tracking 注册/注销与回调。
- 新增 `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.Defaults.cs`，承接 `ApplyDefaultSettings()` 的全局默认恢复逻辑。
- `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.SectionState.cs` 收口为 section state record、capture/apply 主线。
- `PaintSettingsDialog.SectionState.cs` 从 390 行降到 191 行；dirty tracking 和全局默认恢复分别下沉到 117 行与 92 行的独立 partial。

### Task 3: 第十批 PaintSettingsDialog interactions/lifecycle/restore 拆分 `[Done 2026-06-14]`

**Description:** 继续压缩 `PaintSettingsDialog` 设置页复杂度，将 `PaintSettingsDialog.Interactions.cs` 中混合的窗口生命周期和“重置本页/全部重置”流程拆到独立 partial，保留确认保存与画笔即时联动主线在原文件。

**Acceptance criteria:**
- [x] `PaintSettingsDialog.Interactions.cs` 不再同时承载 lifecycle、restore defaults 和 confirm/change handlers 三类职责
- [x] `OnDialogLoaded` 的延迟 `SizeToContent` 提交流程和关闭解绑逻辑保持不变
- [x] “重置本页/全部重置”提示文案、按页默认恢复和 classifier rollback 状态刷新保持不变

**Verification:**
- [x] `dotnet build ClassroomToolkit.sln -c Debug -p:UseSharedCompilation=false`
- [x] `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseSharedCompilation=false`
- [x] contract/invariant filter
- [x] `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`

**Implemented scope:**
- 新增 `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.Lifecycle.cs`，承接 `OnDialogLoaded`、`OnDialogClosed` 与窗口生命周期清理。
- 新增 `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.Restore.cs`，承接 `OnRestoreDefaultsClick`、`OnRestoreAllDefaultsClick` 与 `ApplyDefaultSettingsForCurrentTab()`。
- `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.Interactions.cs` 收口为确认保存、取消、画笔 slider/change handler 和 active brush size 解析。
- `PaintSettingsDialog.Interactions.cs` 从 289 行降到 108 行；新增 lifecycle 与 restore partial 分别为 52 行和 135 行，最大 `PaintSettingsDialog*.cs` 文件降到 277 行。

### Task 3: 第十一批 PaintSettingsDialog selection/combo helper 拆分 `[Done 2026-06-14]`

**Description:** 继续压缩 `PaintSettingsDialog` 设置页复杂度，将 `PaintSettingsDialog.Selection.cs` 中通用 ComboBox 标签选择、数值 ComboBox 回填/解析和百分比换算 helper 拆到独立 partial，保留画笔/形状/导出范围等业务选项选择器在原文件。

**Acceptance criteria:**
- [x] `PaintSettingsDialog.Selection.cs` 不再同时承载业务选项选择器与通用 ComboBox helper
- [x] `Clamp`、`ToPercent`、`ToByte`、`SelectComboByTag`、`ResolveIntCombo`、`ResolveDoubleCombo` 等调用点保持原签名与行为
- [x] 初始化、确认保存、按页恢复默认、section state capture/apply 继续通过同一 helper 路径

**Verification:**
- [x] `dotnet build ClassroomToolkit.sln -c Debug -p:UseSharedCompilation=false`
- [x] `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseSharedCompilation=false`
- [x] contract/invariant filter
- [x] `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`

**Implemented scope:**
- 新增 `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.ComboSelection.cs`，承接通用 ComboBox、int/double ComboBox 与基础数值转换 helper。
- `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.Selection.cs` 收口为 `Shape`、`BrushStyle`、`WhiteboardPreset`、`CalligraphyPreset`、`ClassroomWritingMode`、`InkExportScope` 等业务选项选择/解析。
- `PaintSettingsDialog.Selection.cs` 从 277 行降到 135 行；新增 `ComboSelection.cs` 148 行，最大 `PaintSettingsDialog*.cs` 文件降到 `PaintSettingsDialog.PresetScheme.cs` 258 行。

### Task 3: 第十二批 PaintSettingsDialog preset scheme 拆分 `[Done 2026-06-14]`

**Description:** 继续压缩 `PaintSettingsDialog` 设置页复杂度，将 `PaintSettingsDialog.PresetScheme.cs` 中托管控件事件/降级逻辑和托管参数快照/应用逻辑拆到独立 partial，保留预设选择、应用和提示刷新主线在原文件。

**Acceptance criteria:**
- [x] `PaintSettingsDialog.PresetScheme.cs` 不再同时承载 preset selection、managed control wiring 和 managed parameter snapshot 三类职责
- [x] 预设切换、自定义降级、托管控件 enable/tooltip 和 custom snapshot 行为保持不变
- [x] 现有 preset policy/initialization 测试与 `PaintSettingsDialog*.cs` 源码合同继续成立

**Verification:**
- [x] `dotnet build ClassroomToolkit.sln -c Debug -p:UseSharedCompilation=false`
- [x] `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseSharedCompilation=false`
- [x] contract/invariant filter
- [x] `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`

**Implemented scope:**
- 新增 `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.PresetManagedControls.cs`，承接托管控件事件注册/注销、手动覆盖降级和控件视觉状态。
- 新增 `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.PresetManagedParameters.cs`，承接 custom snapshot、托管参数 capture/apply 和 debug format。
- `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.PresetScheme.cs` 收口为 preset scheme change、convert-to-custom、apply scheme、initial scheme 和 hint update 主线。
- `PaintSettingsDialog.PresetScheme.cs` 从 258 行降到 87 行；最大 `PaintSettingsDialog*.cs` 文件降到 240 行。

## Task 4: 存储原子写与临时文件策略复核

**Description:** 对 `settings / ink / workbook / wal` 的 `temp + replace/copy + cleanup` 模式做一致性审查。只有调用点语义一致时才收敛 helper。

**Acceptance criteria:**
- [ ] 不改变编码、异常语义、覆盖语义和兼容格式。
- [ ] 临时文件残留和回退语义有测试保护。
- [ ] 更新或补充相关存储测试。

**Verification:** `JsonSettingsDocumentStoreAdapterTests|InkPersistenceServiceTests|InkStorageServiceTests|StudentWorkbookStoreTests` + full gate。

## Task 5: 性能测量优先的 hot path 批次

**Description:** 优先跑现有 settings/UI performance sampling，再决定是否优化图片缩略图、画笔渲染、照片切换、设置加载和日志批量写入。

**Acceptance criteria:**
- [ ] 每个性能改动有 before/after 数字。
- [ ] 不牺牲触屏响应、关闭路径和异常降级。
- [ ] 采样结果写入 `artifacts/validation` 或 release evidence。

**Verification:** performance sampling scripts + targeted tests + full gate。

## Task 6: 依赖 waiver 定期复核

**Description:** 当前漏洞扫描为 PASS，但 `Microsoft.Testing.Platform*` 与 `Microsoft.ApplicationInsights` 存在 active waiver 覆盖的 stable outdated packages。后续只在 waiver 到期或兼容性验证充分时升级。

**Acceptance criteria:**
- [ ] `check-dependency-upgrade-feasibility.ps1` 仍能区分 active waiver 与真实回归。
- [ ] 升级前记录兼容性、回滚和 test platform 边界。
- [ ] 不做无证据盲升。

**Verification:** dependency governance + vulnerability scan + full gate。

## Task 7: 本地测试临时目录治理（已完成第一阶段）

**Description:** 本机 `tests/.tmp` 已积累大量忽略文件，影响人工递归扫描体验。优先评估测试清理策略或安全 cleanup 脚本，而不是手工删除当作修复。

**Acceptance criteria:**
- [x] 清理目标必须限制在 repo 内忽略目录。
- [x] 不删除用户数据、fixtures 或版本管理文件。
- [x] 测试生成目录具备可复跑清理路径。

**Verification:**
- [x] `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~TestPathHelperTests"`
- [x] full hard gate
- [x] `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug`

**Implemented scope:**
- 在 `TestPathHelper` 的 repo-local `tests/.tmp` 根目录增加单次进程级 best-effort maintenance。
- 回收规则为“删除超过 7 天的目录；若仍超出 2048 个，则按最旧优先继续裁剪到软上限”。
- 仅作用于测试临时目录，不触碰版本管理内容、fixtures 或用户数据。

**Follow-up:**
- [ ] 如需进一步降低目录数，可在独立批次补一个 dry-run/report 命令，但不应把人工删除当作长期方案。

## 结论

规划和任务清单已经建立，并已自动推进完成 Task 1、Task 2、Task 3 第五阶段与 Task 7 第一阶段。当前全仓没有被自动门禁证明的 P0/P1 失败；下一步应继续处理下一个超长文件族，并保持“单个文件族拆分”和“串行硬门禁收口”。
