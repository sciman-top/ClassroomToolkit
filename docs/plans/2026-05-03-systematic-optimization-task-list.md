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

## Task 3: 超长 App 文件拆分与职责收敛（已完成第五阶段）

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

**Follow-up:**
- [ ] 下一刀继续 `PaintOverlayWindow.*` 中最肥的 rendering 或 cross-page 子文件族。
- [ ] 再下一刀评估 `ImageManagerWindow` 或 `RollCallWindow` 的状态/窗口编排子文件族。
- [ ] `RollCallSettingsDialog` 若后续继续拆分，只再拆一层通用 tab-state/default-apply helper，不引入新抽象。

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
