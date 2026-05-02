规则ID=R1/R2/R6/R8/E4/E5
影响模块=Directory.Build.props; src/ClassroomToolkit.App; src/ClassroomToolkit.App/RollCallSettingsDialog.xaml.cs; src/ClassroomToolkit.App/RollCallSettingsDialog.Speech.cs; tests/ClassroomToolkit.Tests/TestPathHelper.cs; tests/ClassroomToolkit.Tests/TestPathHelperTests.cs; quality gates
当前落点=本轮先建立全仓优化基线，随后完成格式归一化批次、测试临时目录治理，并推进 RollCallSettingsDialog 的第一阶段职责拆分。
目标归宿=后续优化按独立批次推进，每批均以 build -> test -> contract/invariant -> hotspot 收口。
迁移批次=20260503-systematic-optimization-baseline
风险等级=低到中。包含 analyzer 前移、机械格式化，以及 repo 内测试临时目录的 best-effort 自清理；不改变运行时代码、数据格式、UI 行为或依赖版本。

执行命令=
- git status --short --branch
- dotnet build ClassroomToolkit.sln -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
- powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1
- powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug
- dotnet format ClassroomToolkit.sln --verify-no-changes --verbosity minimal
- dotnet format whitespace ClassroomToolkit.sln --verbosity minimal
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~TestPathHelperTests"

验证证据=
- 初始 git 状态干净，分支为 main...origin/main。
- 初始 hard gate 通过：build 0 warning/0 error；test 3474 passed；contract 28 passed；hotspot PASS。
- standard quality gate 通过：build、stable-tests-config、stable-tests、contract、hotspot、governance-truth-source、dependency-governance、dependency-vulnerability、logging-alert-threshold、analyzer-backlog-baseline 均 PASS。
- dependency-vulnerability: PASS no vulnerable packages detected.
- dependency-governance: stable outdated packages 均在 active waiver 覆盖内，未做盲目升级。
- analyzer-backlog-baseline: PASS total=0 report=artifacts/quality/analyzer-backlog-report.json。
- App analyzer 前移后复跑 hard gate 通过：build 0 warning/0 error；test 3474 passed；contract 28 passed；hotspot PASS。
- App analyzer 前移后复跑 standard quality gate 通过，analyzer-backlog-baseline 仍为 total=0。
- 已执行独立 whitespace 批次；`dotnet format whitespace ClassroomToolkit.sln --verify-no-changes --verbosity minimal` 现已通过。
- 格式批次后 hard gate 再次通过：build 0 warning/0 error；test 3476 passed；contract 28 passed；hotspot PASS。
- 格式批次后 standard quality gate 再次通过；analyzer-backlog-baseline 仍为 total=0，dependency-vulnerability 仍为 PASS。
- 测试基础设施新增 `tests/.tmp` best-effort 自清理：`TempDirectorySoftLimit=2048`，`TempDirectoryMaxAge=7 days`，仅在 repo 内 `tests/.tmp` 上生效。
- `TestPathHelperTests` 定向回归通过（7 passed），覆盖“超龄目录删除”和“超过软上限时删除最旧目录”。
- 运行态证据：`tests/.tmp` 目录数已从审查时的 31846 降到 11934，说明自清理已开始回收历史堆积。
- `RollCallSettingsDialog` 结构收敛第一阶段完成：将语音页状态捕获、dirty 判断、控件启停与语音下拉构建拆分到独立 partial 文件 `RollCallSettingsDialog.Speech.cs`。
- 同批删除了 `RollCallSettingsDialog.xaml.cs` 内已无调用链的私有死代码（旧注册表语音扫描与未使用语言/性别显示 helper），未改变可见行为。
- 结构收敛后，`RollCallSettingsDialog.xaml.cs` 行数由 977 降到 716；新增 `RollCallSettingsDialog.Speech.cs` 125 行，主文件职责更集中在通用流程与非语音标签页。
- `PaintToolbarWindow` 结构收敛第二阶段完成：将窗口拖拽、触控拖拽、区域截图恢复、辅助输入转发与命中测试拆分到独立 partial 文件 `PaintToolbarWindow.Pointer.cs`。
- 结构收敛后，`PaintToolbarWindow.xaml.cs` 行数由 1163 降到 874；新增 `PaintToolbarWindow.Pointer.cs` 296 行，主文件职责收敛到工具状态、板书与颜色业务。
- `PaintToolbarDragModeContractTests` 已按文件族聚合更新，保留 “managed mouse capture flow 必须存在，且不得回退到 DragMove” 的 contract 意图，不再把实现位置绑死在单一 `.xaml.cs` 文件。
- `VariableWidthBrushRenderer` 结构收敛第三阶段完成：将 move telemetry snapshot、环境开关解析与 telemetry ring-buffer 聚合逻辑拆分到独立 partial 文件 `VariableWidthBrushRenderer.Telemetry.cs`。
- 结构收敛后，`VariableWidthBrushRenderer.cs` 行数由 1080 降到 859；新增 `VariableWidthBrushRenderer.Telemetry.cs` 228 行，主文件更集中于输入处理、宽度/湿度/速度演算与 stroke 生命周期。
- 该批次未改动 brush 几何、采样、性能阈值或调参算法，仅调整诊断/遥测代码归宿；brush 定向测试、全量测试与 quality gate 均保持 PASS。
- 该批次后 hard gate 再次通过：build 0 warning/0 error；test 3476 passed；contract 28 passed；hotspot PASS。
- 该批次后 standard quality gate 再次通过，dependency-governance / vulnerability / analyzer backlog 均保持 PASS。
- 额外运行事实：并行执行 build 与 test(contract) 时出现过一次 WPF `_wpftmp.csproj` 临时生成竞态，串行复跑后全部通过；后续本仓最终验收应继续采用串行门禁收口。
- `PaintOverlayWindow` 结构收敛第四阶段完成：将照片平移惯性的速度采样、release velocity、`CompositionTarget.Rendering` loop 与停止清理逻辑拆分到独立 partial 文件 `PaintOverlayWindow.Photo.Transform.Inertia.cs`。
- 结构收敛后，`PaintOverlayWindow.Photo.Transform.PanInertia.cs` 行数降到 266；新增 `PaintOverlayWindow.Photo.Transform.Inertia.cs` 200 行，原文件更集中于 pan begin/update/end 与 bounds clamp。
- 该批次未改动惯性参数、边界 clamp、跨页刷新或 ink redraw 策略；`PhotoPanInertiaRenderingContractTests` 仍按 `PaintOverlayWindow.Photo.Transform*.cs` 聚合校验 `CompositionTarget.Rendering` attach/detach 和 release tuning。
- 照片惯性拆分后定向验证通过：build 0 warning/0 error；`PhotoPanInertiaRenderingContractTests|PhotoPanInertiaMotionPolicyTests|PhotoPanReleaseTuningPolicyTests|PhotoManipulationInertiaPolicyTests|PhotoPanInertiaProfilePolicyTests|PhotoPanInertiaDefaultsTests` 共 27 tests passed。
- `PaintOverlayWindow.Presentation` 结构收敛第五阶段完成：将 WPS hook 请求、WPS navigation fallback、hook runtime state、不可用通知和 WPS debounce 状态拆分到独立 partial 文件 `PaintOverlayWindow.Presentation.WpsHook.cs`。
- 结构收敛后，`PaintOverlayWindow.Presentation.cs` 行数降到 657；新增 `PaintOverlayWindow.Presentation.WpsHook.cs` 342 行，原文件更集中于演示前台检测、焦点恢复、通用 command routing 与 fullscreen 判定。
- `PaintOverlayWpsHookUnavailableContractTests` 与 `PaintOverlayForegroundProcessContractTests` 已改为聚合读取 `PaintOverlayWindow.Presentation*.cs`，保持原有源码 contract 意图，不再把实现位置绑死在单一文件。
- WPS/presentation 拆分后定向验证通过：build 0 warning/0 error；`PaintOverlayWpsHookUnavailableContractTests|PaintOverlayForegroundProcessContractTests|WpsNavigationDebouncePolicyTests|WpsHookInputDebouncePolicyTests|WpsPresentationRuntimePolicyTests|WpsHookKeyboardInjectionPolicyTests|WpsRawFallbackTargetPolicyTests|PresentationInputPolicyConsistencyTests|AuxWindowKeyRoutingHandlerTests|AuxWindowWheelRoutingHandlerTests` 共 52 tests passed。

回滚动作=
- 还原 Directory.Build.props 中 ClassroomToolkit.App 的 analyzer 条件加入项。
- 还原 whitespace 批次的机械格式化 diff。
- 还原 TestPathHelper 的 repo-local temp root 自清理逻辑与对应测试。
- 还原 `RollCallSettingsDialog.Speech.cs`、`PaintToolbarWindow.Pointer.cs`、`VariableWidthBrushRenderer.Telemetry.cs`、`PaintOverlayWindow.Photo.Transform.Inertia.cs`、`PaintOverlayWindow.Presentation.WpsHook.cs` 的 partial 拆分，并恢复对应原文件内容。
- 回滚后重跑 dotnet build ClassroomToolkit.sln -c Debug，以及本仓 contract/invariant 过滤集。
