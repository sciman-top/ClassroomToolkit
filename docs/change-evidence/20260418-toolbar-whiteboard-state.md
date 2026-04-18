# 2026-04-18 Toolbar Whiteboard State

- 规则 ID: R1, R2, R3, R6, R8
- 风险等级: Low
- 边界: `src/ClassroomToolkit.App/Paint` 截图选区、白板按钮状态、工具条按钮切换
- 当前落点: `PaintToolbarWindow.xaml.cs`, `RegionSelectionOverlayWindow.xaml.cs`, `RegionScreenCaptureWorkflow.cs`, `MainWindow.Paint.cs`
- 目标归宿: 白板按钮只在真实白板/截图白板场景高亮；截图待框选状态不污染工具按钮；工具条区域可中断或切换功能
- 时间戳: 2026-04-18T21:54:35.0778247+08:00

## 依据

- 复现描述：先点红色画笔，再点白板，红色画笔和白板同时高亮；进入截图十字光标后移入工具条不能直接点其它按钮；再次点白板应直接进入白板；退出白板后白板按钮仍残留高亮。
- 根因：
  - `BoardButton` 是 `ToggleButton`，截图待框选/待直接进入白板阶段会受瞬时 checked 状态影响。
  - 白板点击没有清理画笔颜色、形状、橡皮擦等非白板选中态，导致“红色画笔 + 白板”同时高亮。
  - 区域选择窗口把小于有效区域的外部单击当作取消关闭，无法保持十字光标等待继续框选。
  - 区域选择窗口通过 `SafeShowDialog()` / `ShowDialog()` 模态打开，WPF 会禁用同进程工具条窗口，因此鼠标移入工具条后看似恢复箭头，但点击工具条按钮只触发系统不可点击提示声。
  - 穿透取消后先无条件 arm “直接白板/继续截图”，再重放工具条点击；当点击其它按钮时，按钮视觉能切换，但高层待截图恢复状态可能在同一流程末尾被重新提交，导致移出工具条后又恢复十字截图模式。
  - 仅依赖按钮 `Click` 清理待恢复状态不够稳；当鼠标先 `PointerMove` 进入工具条时，续截图定时器已经 arm，后续按钮点击必须在 `PreviewMouseDown` 阶段就抢先取消。
  - 区域选择遮罩覆盖工具条，第一次落在工具条上的点击只关闭遮罩，未转交给实际按钮。
  - 截图白板退出路径在 `ExitPhotoMode()` 后缺少一次白板视觉状态刷新。

## 命令

- `codex --version`
- `codex --help`
- `codex status`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ToolbarBoardSelectionVisualPolicyTests|FullyQualifiedName~RegionSelectionCompletionPolicyTests"`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ToolbarPassthroughActivationPolicyTests|FullyQualifiedName~ToolbarBoardSelectionVisualPolicyTests|FullyQualifiedName~RegionSelectionCompletionPolicyTests|FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests"`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ToolbarPassthroughActivationPolicyTests|FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests" /p:BaseOutputPath=D:/OneDrive/CODE/ClassroomToolkit/tests/ClassroomToolkit.Tests/bin-verify/`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ToolbarResumeCancellationPolicyTests|FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests|FullyQualifiedName~ToolbarPassthroughActivationPolicyTests" /p:BaseOutputPath=D:/OneDrive/CODE/ClassroomToolkit/tests/ClassroomToolkit.Tests/bin-verify/`
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests.ToolbarPassthroughCancel_ShouldReplayClickToToolbar"`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests.ToolbarPassthroughCancel_ShouldReplayClickToToolbar" /p:BaseOutputPath=D:/OneDrive/CODE/ClassroomToolkit/tests/ClassroomToolkit.Tests/bin-verify/`
- `dotnet build ClassroomToolkit.sln -c Debug /p:BaseOutputPath=D:/OneDrive/CODE/ClassroomToolkit/tests/ClassroomToolkit.Tests/bin-verify/`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug /p:BaseOutputPath=D:/OneDrive/CODE/ClassroomToolkit/tests/ClassroomToolkit.Tests/bin-verify/`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests" /p:BaseOutputPath=D:/OneDrive/CODE/ClassroomToolkit/tests/ClassroomToolkit.Tests/bin-verify/`
- `git diff --check`

## 关键输出

- `codex --version`: `codex-cli 0.121.0`
- `codex --help`: help 可用，列出 `exec/review/login/mcp/...` 等子命令
- `codex status`: 非交互环境失败，`stdin is not a terminal`
- TDD 红测：策略测试先因 `regionCapturePending` 参数和 `RegionSelectionCompletionPolicy` 缺失编译失败。
- TDD 红测：`ToolbarPassthroughCancel_ShouldReplayClickToToolbar` 先因 `RegionScreenCaptureWorkflow` 仍包含 `SafeShowDialog()` 失败。
- TDD 红测：`ToolbarPassthroughActivationPolicyTests` 先因缺少 `ShouldArmDirectWhiteboardEntry` 编译失败。
- TDD 红测：`ToolbarResumeCancellationPolicyTests` 先因缺少 `ToolbarResumeCancellationPolicy` 编译失败。
- 定向策略测试：10/10 通过
- 定向回归测试：24/24 通过
- 标准输出目录验证受阻：`sciman Classroom Toolkit (40272)` 和 `Microsoft Visual Studio (68688)` 锁定 `bin/Debug/net10.0-windows/sciman Classroom Toolkit.exe/.dll`
- 隔离输出目录 `build`: 0 warning, 0 error
- 隔离输出目录全量测试：3256/3256 通过
- 隔离输出目录 `contract/invariant`: 28/28 通过
- `git diff --check`: 无空白错误；仅有 Git LF -> CRLF 工作区提示

## N/A / 异常

- `platform_na`
  - reason: `codex status` 在当前非交互 shell 下返回 `stdin is not a terminal`
  - alternative_verification: 记录 `codex --version` 与 `codex --help`，并以仓库根 `AGENTS.md` 作为活动规则来源
  - evidence_link: `docs/change-evidence/20260418-toolbar-whiteboard-state.md`
  - expires_at: 2026-04-18

## Hotspot 复核

- `PaintToolbarWindow.xaml.cs`
  - 白板按钮高亮统一由 `ToolbarBoardSelectionVisualPolicy` 决定。
  - `_regionCapturePending`、`_directWhiteboardEntryArmed`、真实白板、会话截图白板统一映射为白板按钮高亮。
  - 白板动作会清理颜色、形状、橡皮擦、区域擦除等非白板选中态，避免多个功能同时高亮。
  - 非白板工具动作先清理待命状态；需要切换工具时会退出白板并刷新视觉状态。
  - `TryActivateButtonAtScreenPoint` 只查找 WPF `ButtonBase` 并触发对应 Click，不引入 WinForms 控件路径。
- `RegionSelectionOverlayWindow.xaml.cs` / `RegionScreenCaptureWorkflow.cs`
  - 透传取消保留输入来源：移入工具条是 `PointerMove`，按下工具条是 `PointerPress`。
  - 只有 `PointerPress` 会重放工具条点击，避免“只是移入工具条就误触按钮”。
  - 小于有效截图区域的鼠标/触摸释放只清空当前临时框，不关闭选区窗口，继续保持十字模式等待框选。
  - 区域选择窗口不再使用 `DialogResult`，完成状态由 `SelectionAccepted` 暴露给调用方。
- `RegionScreenCaptureWorkflow.cs`
  - 区域选择窗口改为 `Show()` + `DispatcherFrame` 同步等待，不再通过 `ShowDialog()` 禁用工具条。
  - 工具条保持可交互后，遮罩的透传取消与工具条点击重放策略才能生效。
- `MainWindow.Paint.cs`
  - 工具条 `PointerPress` 透传取消后先重放按钮点击，再根据重放结果决定是否 arm 直接白板入口。
  - 如果按钮点击重放成功，调用 `ClearDirectWhiteboardEntryArm()`，确保点击其它工具后不会在移出工具条时恢复十字截图模式。
  - 只有 `PointerMove` 进入工具条且没有实际按钮点击时，才保留继续等待截图/再次点击白板直接进入的待命状态。
  - 点击其它按钮会通过原有 `PrepareForNonBoardToolbarAction` 中断白板待命/白板状态。
  - 会话截图进入图片白板后再次刷新工具条白板视觉状态，确保退出/进入状态不残留。
- `ToolbarPassthroughActivationPolicy.cs`
  - 增加 `ShouldArmDirectWhiteboardEntry`，把“移入工具条继续等待”和“点击工具条按钮中断白板功能”拆成显式策略。
- `ToolbarResumeCancellationPolicy.cs`
  - 增加工具条预按下取消策略：续截图/直接白板待命期间，非白板按钮一旦按下，先取消待命状态，再让按钮自己的 Click 切换实际工具。
  - 白板按钮按下不取消待命，保留“再次点击白板直接进入白板”的语义。
- `PaintToolbarWindow.xaml`
  - 图形按钮纳入 ToggleButton 高亮状态，避免形状工具和颜色/白板状态混杂。

## 回滚

- 回滚文件：
  - `src/ClassroomToolkit.App/MainWindow.Paint.cs`
  - `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
  - `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
  - `src/ClassroomToolkit.App/Paint/RegionCaptureInitialPassthroughPolicy.cs`
  - `src/ClassroomToolkit.App/Paint/RegionScreenCaptureWorkflow.cs`
  - `src/ClassroomToolkit.App/Paint/RegionSelectionOverlayWindow.xaml.cs`
  - `src/ClassroomToolkit.App/Paint/RegionSelectionCompletionPolicy.cs`
  - `src/ClassroomToolkit.App/Paint/ToolbarBoardSelectionVisualPolicy.cs`
  - `src/ClassroomToolkit.App/Paint/ToolbarPassthroughActivationPolicy.cs`
  - `tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs`
  - `tests/ClassroomToolkit.Tests/RegionCaptureInitialPassthroughPolicyTests.cs`
  - `tests/ClassroomToolkit.Tests/RegionSelectionCompletionPolicyTests.cs`
  - `tests/ClassroomToolkit.Tests/ToolbarBoardSelectionVisualPolicyTests.cs`
  - `tests/ClassroomToolkit.Tests/ToolbarPassthroughActivationPolicyTests.cs`
- 回滚动作：撤销以上文件的本次修改并重新执行 `build -> test -> contract/invariant`。
- 临时输出目录：`tests/ClassroomToolkit.Tests/bin-verify/` 已删除。

## 2026-04-18 补充修复：初始鼠标已在工具条区域

- 规则 ID: R1, R2, R3, R6, R8
- 风险等级: Low
- 边界: `src/ClassroomToolkit.App/Paint/RegionScreenCaptureWorkflow.cs` 截图启动入口与 `RegionCaptureInitialPassthroughPolicy`
- 当前落点: 区域截图开始时的初始鼠标位置判定
- 目标归宿: 单击白板按钮后，如果鼠标仍在工具条区域，立即进入工具条暂停态，不显示截图遮罩；只有鼠标移出工具条后才恢复十字光标和截图遮罩
- 时间戳: 2026-04-18T22:12:00+08:00

### 依据

- 复现补充：单击白板按钮时鼠标天然位于工具条按钮上；如果截图遮罩立即显示，后续点击其它工具按钮可能仍被遮罩截获，表现为白板待截图状态没有真正中断。
- 根因补充：原透传取消依赖 `RegionSelectionOverlayWindow` 接收到后续 `MouseMove` 或 `MouseLeftButtonDown`。截图启动瞬间鼠标已在工具条区域时，没有在遮罩显示前完成初始 passthrough 判定。
- 修复策略：在 `TryCaptureToPng` 显示 `RegionSelectionOverlayWindow` 前读取 `Cursor.Position`；若命中工具条 passthrough 矩形，直接返回 `ToolbarPassthroughCanceled + PointerMove`，复用既有 `ArmDirectWhiteboardEntry()` 暂停/移出恢复逻辑。

### 命令

- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter FullyQualifiedName~RegionCaptureInitialPassthroughPolicyTests`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ToolbarPassthroughActivationPolicyTests|FullyQualifiedName~ToolbarResumeCancellationPolicyTests|FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests|FullyQualifiedName~RegionCaptureInitialPassthroughPolicyTests"`
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- `git diff --check`

### 关键输出

- TDD 红测：`RegionCaptureInitialPassthroughPolicyTests` 先因 `RegionCaptureInitialPassthroughPolicy` 缺失编译失败。
- TDD 绿测：`RegionCaptureInitialPassthroughPolicyTests` 2/2 通过。
- 相邻状态回归：26/26 通过。
- build：0 warning, 0 error。
- 全量测试：3258/3258 通过。
- contract/invariant：28/28 通过。
- `git diff --check`：无空白错误；仅有 Git LF -> CRLF 工作区提示。

### Hotspot 复核

- `RegionCaptureInitialPassthroughPolicy.cs`: 只接受屏幕坐标和工具条矩形，忽略无效矩形，不引入 UI 依赖，便于单元测试。
- `RegionScreenCaptureWorkflow.cs`: 初始命中工具条时不创建截图遮罩窗口，避免遮罩压住工具条；未命中时仍走原有选区窗口和文件捕获流程。
- `PaintToolbarWindow.xaml.cs`: 既有 `_resumeRegionCaptureArmed` 定时器仍负责“鼠标离开工具条后恢复截图”，非白板按钮 `PreviewMouseDown` 仍负责取消待恢复状态。

### 回滚

- 回滚文件：
  - `src/ClassroomToolkit.App/Paint/RegionCaptureInitialPassthroughPolicy.cs`
  - `src/ClassroomToolkit.App/Paint/RegionScreenCaptureWorkflow.cs`
  - `tests/ClassroomToolkit.Tests/RegionCaptureInitialPassthroughPolicyTests.cs`
- 回滚动作：删除新增策略与测试，并移除 `TryCaptureToPng` 中显示遮罩前的初始 passthrough 判定，然后重新执行 `build -> test -> contract/invariant`。

## 2026-04-18 补充修复：DPI 坐标统一（工具条命中/遮罩透传）

- 规则 ID: R1, R2, R3, R6, R8
- 风险等级: Low
- 边界: `MainWindow.Paint`、`PaintToolbarWindow`、`RegionSelectionOverlayWindow` 的工具条区域命中与截图遮罩透传
- 当前落点: `src/ClassroomToolkit.App/MainWindow.Paint.cs`、`src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`、`src/ClassroomToolkit.App/Paint/RegionSelectionOverlayWindow.xaml.cs`
- 目标归宿: 工具条区域命中统一使用屏幕像素坐标，进入工具条即可可靠退出截图遮罩并允许点击其它按钮；非白板按钮点击后不再恢复待截图状态
- 时间戳: 2026-04-18T22:56:00+08:00

### 依据

- 现象与根因：截图流程使用 `Cursor.Position`（屏幕像素），工具条命中判断使用 `Left/ActualWidth`（WPF DIP），DPI 缩放下会出现“看起来在工具条内，逻辑判定却在外”的状态漂移，导致遮罩不消失、非白板按钮点击后移出工具条又恢复十字截图。
- 修复策略：
  - 新增 `WindowDipToScreenRectPolicy` 与 `WindowScreenBoundsResolver`，统一窗口屏幕矩形解析。
  - `ResolveCapturePassthroughRegions()` 改为屏幕像素矩形。
  - `PaintToolbarWindow` 的 `IsPointInsideToolbar()` 与 `TryActivateButtonAtScreenPoint()` 改为屏幕像素命中，并按 DPI 折算回本地坐标做按钮 hit test。
  - `RegionSelectionOverlayWindow` 透传取消改为 `Cursor.Position` 屏幕坐标判定。

### 命令

- `codex --version`
- `codex --help`
- `codex status`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~WindowDipToScreenRectPolicyTests"`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~WindowDipToScreenRectPolicyTests|FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests|FullyQualifiedName~ToolbarPassthroughActivationPolicyTests|FullyQualifiedName~ToolbarBoardSelectionVisualPolicyTests|FullyQualifiedName~RegionCaptureInitialPassthroughPolicyTests|FullyQualifiedName~ToolbarResumeCancellationPolicyTests|FullyQualifiedName~RegionSelectionCompletionPolicyTests"`
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`

### 关键输出

- `codex --version`: `codex-cli 0.121.0`
- `codex --help`: help 可用
- `codex status`: `stdin is not a terminal`
- TDD 红测：新增 `WindowDipToScreenRectPolicyTests` 初次因策略类缺失编译失败；实现后一次因最小宽高边界断言失败（`width=2`），修正策略后通过。
- 定向测试：39/39 通过。
- build：0 warning, 0 error。
- 全量测试：3261/3261 通过。
- contract/invariant：28/28 通过。

### N/A / 异常

- `platform_na`
  - reason: `codex status` 在非交互 shell 下返回 `stdin is not a terminal`
  - alternative_verification: 使用 `codex --version`、`codex --help` 与本仓 `AGENTS.md` 加载结果作为替代证据
  - evidence_link: `docs/change-evidence/20260418-toolbar-whiteboard-state.md`
  - expires_at: 2026-04-18

### Hotspot 复核

- `WindowScreenBoundsResolver`：优先 Win32 `GetWindowRect`（屏幕像素），失败时按 DPI 从 DIP 回退换算，避免命中来源不一致。
- `PaintToolbarWindow`：恢复计时器判定与按钮重放判定同源（同一屏幕矩形），消除“判定在外但视觉在内”的抖动。
- `RegionSelectionOverlayWindow`：透传取消判定改为屏幕像素，和 `ResolveCapturePassthroughRegions` 使用同一坐标系。

### 回滚

- 回滚文件：
  - `src/ClassroomToolkit.App/MainWindow.Paint.cs`
  - `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
  - `src/ClassroomToolkit.App/Paint/RegionSelectionOverlayWindow.xaml.cs`
  - `src/ClassroomToolkit.App/Paint/WindowDipToScreenRectPolicy.cs`
  - `src/ClassroomToolkit.App/Paint/WindowScreenBoundsResolver.cs`
  - `tests/ClassroomToolkit.Tests/WindowDipToScreenRectPolicyTests.cs`
- 回滚动作：撤销以上文件后重新执行 `build -> test -> contract/invariant`。

## 2026-04-18 补充修复：工具条高于遮罩时主动取消活动选区

- 规则 ID: R1, R2, R3, R6, R8
- 风险等级: Low
- 边界: `PaintToolbarWindow` 工具条预按下、`RegionScreenCaptureWorkflow` 活动选区生命周期、`RegionSelectionOverlayWindow` 选区取消结果
- 当前落点: 工具条按钮已实际收到鼠标按下，但区域选择遮罩仍在后台运行的状态缝隙
- 目标归宿: 点击非白板工具按钮时立即关闭活动截图选区，不重放该点击、不重新 arm 截图
- 时间戳: 2026-04-18T22:56:50.4866004+08:00

### 依据

- 复现补充：当工具条窗口位于截图遮罩之上时，鼠标进入工具条区域后遮罩窗口可能收不到 `MouseMove/MouseDown`，因此不会触发遮罩侧 passthrough 取消。
- 根因补充：此前只在工具条本地清理 `_regionCapturePending/_resumeRegionCaptureArmed/_directWhiteboardEntryArmed`，没有关闭仍处于 `DispatcherFrame` 中的 `RegionSelectionOverlayWindow`；用户移出工具条后仍会看到遮罩和十字光标。
- 修复策略：
  - `RegionScreenCaptureWorkflow` 暴露当前活动选区的受控取消入口 `CancelActiveSelectionFromToolbarHandledPress()`。
  - 新增 `ToolbarHandledPress`，区分“遮罩截获的按钮按下”（需要重放）和“工具条已经处理的按钮按下”（不得重放）。
  - `PaintToolbarWindow.OnPreviewMouseDown` 在非白板按钮按下阶段主动取消活动选区并清理待恢复状态。

### 命令

- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ToolbarPassthroughActivationPolicyTests|FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests.ToolbarNonBoardPress_ShouldCancelActiveRegionSelection_WhenToolbarIsAboveMask"`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ToolbarPassthroughActivationPolicyTests|FullyQualifiedName~ToolbarResumeCancellationPolicyTests|FullyQualifiedName~ToolbarBoardSelectionVisualPolicyTests|FullyQualifiedName~RegionCaptureInitialPassthroughPolicyTests|FullyQualifiedName~RegionSelectionCompletionPolicyTests|FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests"`
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- `git diff --check`

### 关键输出

- TDD 红测：新增 `ToolbarHandledPress` 用例先因 `RegionScreenCapturePassthroughInputKind.ToolbarHandledPress` 不存在编译失败。
- TDD 绿测：定向测试 14/14 通过。
- 相邻状态回归：39/39 通过。
- build：0 warning, 0 error。
- 全量测试：3264/3264 通过。
- contract/invariant：28/28 通过。
- `git diff --check`：退出码 0；仅有 Git LF -> CRLF 工作区提示。

### N/A / 异常

- validation_anomaly
  - reason: 曾并行执行两个 `dotnet test` 过滤命令，WPF 项目共享 `obj/Debug/net10.0-windows/sciman Classroom Toolkit.dll` 输出导致一次 `MC2000` 文件占用错误。
  - alternative_verification: 改为串行执行同一批定向测试与完整硬门禁，均已通过。
  - evidence_link: `docs/change-evidence/20260418-toolbar-whiteboard-state.md`
  - expires_at: 2026-04-18
- `platform_na`
  - reason: `codex status` 在当前非交互 shell 下返回 `stdin is not a terminal`
  - alternative_verification: 已记录 `codex --version`、`codex --help`，并以仓库根 `AGENTS.md` 作为活动规则来源
  - evidence_link: `docs/change-evidence/20260418-toolbar-whiteboard-state.md`
  - expires_at: 2026-04-18

### Hotspot 复核

- `PaintToolbarWindow.xaml.cs`
  - 非白板按钮在 `PreviewMouseDown` 阶段先取消活动选区，再清理待恢复状态，早于按钮自身 `Click` 切换工具模式。
  - 白板按钮仍由既有直接白板入口逻辑处理，本次不扩大该交互语义。
- `RegionScreenCaptureWorkflow.cs`
  - `_activeSelector` 只在 `ShowSelectionOverlay` 生命周期内持有，`finally` 中按引用清理，避免陈旧窗口引用。
  - `ToolbarHandledPress` 不会被 `ToolbarPassthroughActivationPolicy.ShouldReplayToolbarClick` 识别为可重放点击。
- `RegionSelectionOverlayWindow.xaml.cs`
  - 工具条已处理按下会设置 `CanceledByPassthrough + ToolbarHandledPress` 并关闭窗口，使 `DispatcherFrame` 退出。
- `ToolbarPassthroughActivationPolicyTests`
  - 覆盖 `ToolbarHandledPress` 不重放、不 arm 的分支，防止回归为“点击其它按钮后移出工具条又恢复截图”。

### 回滚

- 回滚文件：
  - `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
  - `src/ClassroomToolkit.App/Paint/RegionScreenCaptureWorkflow.cs`
  - `src/ClassroomToolkit.App/Paint/RegionSelectionOverlayWindow.xaml.cs`
  - `tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs`
  - `tests/ClassroomToolkit.Tests/ToolbarPassthroughActivationPolicyTests.cs`
- 回滚动作：移除 `ToolbarHandledPress`、活动选区取消入口与工具条预按下调用后，重新执行 `build -> test -> contract/invariant`。

## 2026-04-18 补充修复：移入工具条即关闭待截图遮罩并 arm 直入白板

- 规则 ID: R1, R2, R3, R6, R8
- 风险等级: Low
- 边界: `PaintToolbarWindow` 工具条 hover 事件、`RegionScreenCaptureWorkflow` 活动选区取消入口、`RegionSelectionOverlayWindow` PointerMove 取消结果
- 当前落点: 鼠标移入工具条时光标已变回普通箭头，但活动截图遮罩仍未关闭，导致主截图流程拿不到 `PointerMove` 取消结果，也不会 arm “再次点击白板直入白板”
- 目标归宿: 鼠标一进入工具条区域就关闭活动截图遮罩；区域截图流程以 `PointerMove` 取消返回；再次单击白板按钮时不再截图而是直接进入白板
- 时间戳: 2026-04-18T23:19:00.7963728+08:00

### 依据

- 复现补充：用户反馈“鼠标移到工具条区域内时，十字光标能变回普通光标，但截图遮罩没有消失；再次单击白板按钮也未直入白板”。
- 根因：
  - 现有修复只覆盖了“工具条已收到鼠标按下”的取消路径：`OnPreviewMouseDown -> CancelActiveSelectionFromToolbarHandledPress()`。
  - 对于“仅移入工具条，还未点击任何按钮”的情况，活动 `RegionSelectionOverlayWindow` 没有被工具条侧主动关闭，因此 `TryCaptureToPng()` 继续卡在 `DispatcherFrame`，主流程拿不到 `ToolbarPassthroughCanceled + PointerMove`。
  - 由于主流程未收到 `PointerMove` 取消结果，`ToolbarPassthroughActivationPolicy.ShouldArmDirectWhiteboardEntry(...)` 不会触发，故再次点击白板仍被当成重新截图而非直入白板。
- 修复策略：
  - 在工具条窗口增加 `MouseEnter` 监听；当 `_regionCapturePending == true` 时，立即调用 `RegionScreenCaptureWorkflow.CancelActiveSelectionFromToolbarPointerMove()`。
  - 在 `RegionScreenCaptureWorkflow` 与 `RegionSelectionOverlayWindow` 中补充 `PointerMove` 版主动取消入口，确保 hover 触发的关闭结果与遮罩自身 `TryCancelForPassthrough(..., PointerMove)` 语义一致。

### 命令

- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests.ToolbarHoverDuringPendingCapture_ShouldCancelActiveRegionSelection_AsPointerMove"`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests.ToolbarHoverDuringPendingCapture_ShouldCancelActiveRegionSelection_AsPointerMove" /p:BaseOutputPath=D:/OneDrive/CODE/ClassroomToolkit/tests/ClassroomToolkit.Tests/bin-verify-hover/`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ToolbarPassthroughActivationPolicyTests|FullyQualifiedName~ToolbarResumeCancellationPolicyTests|FullyQualifiedName~ToolbarBoardSelectionVisualPolicyTests|FullyQualifiedName~RegionCaptureInitialPassthroughPolicyTests|FullyQualifiedName~RegionSelectionCompletionPolicyTests|FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests" /p:BaseOutputPath=D:/OneDrive/CODE/ClassroomToolkit/tests/ClassroomToolkit.Tests/bin-verify-hover/`
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet build ClassroomToolkit.sln -c Debug /p:BaseOutputPath=D:/OneDrive/CODE/ClassroomToolkit/tests/ClassroomToolkit.Tests/bin-verify-hover/`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug /p:BaseOutputPath=D:/OneDrive/CODE/ClassroomToolkit/tests/ClassroomToolkit.Tests/bin-verify-hover/`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests" /p:BaseOutputPath=D:/OneDrive/CODE/ClassroomToolkit/tests/ClassroomToolkit.Tests/bin-verify-hover/`
- `git diff --check`

### 关键输出

- TDD 红测阶段 1：新增契约测试先因缺少 `MouseEnter += OnToolbarMouseEnter;` 和 `CancelActiveSelectionFromToolbarPointerMove()` 失败。
- TDD 红测阶段 2：首次实现后因 `MouseEventArgs` 命名冲突编译失败，修正为 `System.Windows.Input.MouseEventArgs` 后通过。
- 定向绿测：新增 hover 契约测试 1/1 通过。
- 相邻回归：40/40 通过。
- 标准 `build`：失败，`src/ClassroomToolkit.App/bin/Debug/net10.0-windows` 被 `sciman Classroom Toolkit (109936)` 与 `Microsoft Visual Studio (68688)` 锁定。
- 隔离输出目录 `build`：0 warning, 0 error。
- 隔离输出目录全量测试：3265/3265 通过。
- 隔离输出目录 `contract/invariant`：28/28 通过。
- `git diff --check`：退出码 0；仅有 Git LF -> CRLF 工作区提示。

### N/A / 异常

- `gate_na`
  - reason: 标准硬门禁 `dotnet build ClassroomToolkit.sln -c Debug` 客观不可执行，运行中的 `sciman Classroom Toolkit` 与 `Microsoft Visual Studio` 锁定默认 `bin/Debug` 输出文件。
  - alternative_verification: 依固定顺序改用隔离输出目录执行 `build -> test -> contract/invariant`，命令见上，均通过。
  - evidence_link: `docs/change-evidence/20260418-toolbar-whiteboard-state.md`
  - expires_at: 2026-04-18

### Hotspot 复核

- `PaintToolbarWindow.xaml.cs`
  - `MouseEnter` 只在 `_regionCapturePending` 为真时触发 hover 取消，不会干扰普通工具条悬停。
  - 继续保留 `OnPreviewMouseDown` 处理“已按下其它按钮”的显式中断链，hover 和 press 两条取消路径互补。
- `RegionScreenCaptureWorkflow.cs`
  - `CancelActiveSelectionFromToolbarPointerMove()` 与 `CancelActiveSelectionFromToolbarHandledPress()` 都通过 `_activeSelector` 单点收口，不分散窗口生命周期控制。
- `RegionSelectionOverlayWindow.xaml.cs`
  - `CancelFromToolbarPointerMove()` 复用了与原始 passthrough move 一致的结果：`CanceledByPassthrough + PointerMove + Arrow + Close()`，保证主流程能继续沿用既有 `ShouldArmDirectWhiteboardEntry(...)` 判断。
- `RegionCaptureWhiteboardIntegrationContractTests.cs`
  - 新增契约锁定 hover 取消链，防止回归到“光标恢复箭头但遮罩没消失、再次点白板不直入”的状态。

### 回滚

- 回滚文件：
  - `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
  - `src/ClassroomToolkit.App/Paint/RegionScreenCaptureWorkflow.cs`
  - `src/ClassroomToolkit.App/Paint/RegionSelectionOverlayWindow.xaml.cs`
  - `tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs`
- 回滚动作：移除 `MouseEnter` hover 取消链与 `PointerMove` 主动取消入口，恢复到仅处理工具条按下取消的版本，然后重新执行 `build -> test -> contract/invariant`。

## 2026-04-18 补充修复：白板二次点击直入与工具条悬停强制取消遮罩

- 规则 ID: R1, R2, R3, R6, R8
- 风险等级: Low
- 边界: `PaintToolbarWindow` 白板按钮状态机、工具条 hover/press 对活动截图选区的取消路径
- 当前落点:
  - `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
  - `tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs`
- 目标归宿:
  - 鼠标进入工具条区域时立即关闭活动截图遮罩
  - 截图等待态下再次点击白板按钮时不再重新截图，直接进入白板
  - 先双击白板再切换其它按钮时，不再卡在等待截图状态
- 时间戳: 2026-04-18T23:44:00+08:00

### 依据

- 现象：
  - 鼠标进入工具条区域后，遮罩未及时关闭
  - 再次点击白板未触发“直入白板”
  - 双击白板后点击其它按钮，存在持续等待截图的残留状态
- 根因：
  - `OnToolbarMouseEnter` 仅在 `_regionCapturePending` 为真时才尝试取消活动选区，无法覆盖重入/恢复链路
  - `OnBoardClick` 在截图等待态下没有显式“二次点击直入白板”分支
  - 白板按钮预按下场景未统一主动取消活动选区，导致 `DispatcherFrame` 可能继续等待

### 命令

- `codex --version`
- `codex --help`
- `codex status`
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`

### 关键输出

- `codex --version`: `codex-cli 0.121.0`
- `codex --help`: help 可用
- `codex status`: `stdin is not a terminal`
- `dotnet build`: 0 warning, 0 error
- `dotnet test`（全量）: 3266/3266 通过
- `dotnet test`（contract/invariant 过滤）: 28/28 通过

### N/A / 异常

- `platform_na`
  - reason: `codex status` 在当前非交互 shell 下返回 `stdin is not a terminal`
  - alternative_verification: 记录 `codex --version`、`codex --help` 与项目级 `AGENTS.md` 路径
  - evidence_link: `docs/change-evidence/20260418-toolbar-whiteboard-state.md`
  - expires_at: 2026-04-18

### Hotspot 复核

- `PaintToolbarWindow.xaml.cs`
  - 白板按钮按下前若存在截图交互态，先执行 `CancelActiveSelectionFromToolbarHandledPress()` 收敛活动选区
  - 白板点击增加 `(_directWhiteboardEntryArmed || _resumeRegionCaptureArmed || _regionCapturePending)` 直入分支
  - `OnToolbarMouseEnter` 与 `OnToolbarDragMove` 统一调用 `CancelActiveSelectionFromToolbarPointerMove()`，确保移入工具条即退出遮罩
  - 非白板按钮仍通过 `ClearDirectWhiteboardEntryArm()` 清理待恢复状态，避免再次移出工具条时回到截图等待
- `RegionCaptureWhiteboardIntegrationContractTests.cs`
  - 增加“二次点击白板直入”契约
  - 明确约束 hover 取消路径不再依赖 `if (!_regionCapturePending)` 的局部前提

### 回滚

- 回滚文件：
  - `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
  - `tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs`
- 回滚动作：
  1. 撤销以上文件至本次修改前版本
  2. 重新执行 `build -> test -> contract/invariant`
