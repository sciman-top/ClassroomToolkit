# 2026-05-31 白板入口、截图白板与工具切换修复证据

## 规则与风险
- 规则 ID: R1/R2/R3/R6/R8, E4
- 风险等级: 中。涉及 PDF/图片全屏、白板入口弹层、截图选区、工具条切换和课堂触屏操作连续性。
- 边界: 仅处理白板按钮入口策略、截图进入白板、底色进入白板和白板内工具切换；不整理当前工作树中既有规则文件、画笔粗细、RollCall、WPS hook 等无关漂移。
- 当前落点: `src/ClassroomToolkit.App/Paint` 工具条与截图选区流程。
- 目标归宿: 入口决策落到小型 policy，窗口文件保留事件接线；截图选区保留在 `RegionScreenCaptureWorkflow` / `RegionSelectionOverlayWindow`。

## 依据
- PDF/图片全屏时点击白板按钮应先弹出“截图/白板/底色”，而不是直接进入白板。
- 截图进入白板失败的根因之一是白板按钮点击位置会被当作初始 passthrough 区域并立即取消选区。
- 点击白板底色颜色后应立即进入该底色白板模式。
- 白板模式中切换“擦除擦/区域删除/图形”等工具不应退出白板。

## 变更
- 新增 `ToolbarBoardClickActionPolicy`，将白板按钮点击策略从窗口事件中抽出：PDF/图片全屏且未在白板中时默认打开动作弹层；已有白板中再次点击仍退出白板。
- `PaintToolbarWindow` 的非白板工具动作不再调用退出白板逻辑，只清理弹层、截图恢复 arm 和直接进入 arm。
- 白板底色按钮选择颜色成功后调用 `EnterWhiteboardAction()`，即时进入对应底色白板。
- 截图流程在工具条初始点击仍位于 passthrough 区域时延迟 hover 取消；只有指针离开初始区域后再按 passthrough 规则处理。
- 补充 `ToolbarBoardClickActionPolicyTests` 和 `RegionCaptureWhiteboardIntegrationContractTests` 契约断言，锁定上述行为。

## 命令与证据
- `codex --version`
  - exit_code=0
  - 结果: `codex-cli 0.135.0`
- `codex --help`
  - exit_code=0
  - 结果: help 中可见 `exec/review/login/logout/mcp/plugin/doctor/sandbox/debug/features` 等命令。
- `codex status`
  - platform_na: 非交互环境报错 `Error: stdin is not a terminal`
  - alternative_verification: 已记录 `codex --version` 与 `codex --help`，功能验证使用 dotnet 门禁。
  - expires_at: 2026-06-30
- `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ToolbarBoardClickActionPolicyTests|FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests|FullyQualifiedName~RegionCaptureInitialPassthroughPolicyTests|FullyQualifiedName~ToolbarPassthroughActivationPolicyTests"`
  - exit_code=0
  - 结果: `已通过! - 失败: 0，通过: 37，已跳过: 0，总计: 37`
- `dotnet build ClassroomToolkit.sln -c Debug`
  - exit_code=0
  - 结果: `已成功生成。0 个警告 0 个错误`
- `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug`
  - exit_code=0
  - 结果: `已通过! - 失败: 0，通过: 3508，已跳过: 0，总计: 3508`
- `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code=0
  - 结果: `已通过! - 失败: 0，通过: 29，已跳过: 0，总计: 29`
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
  - exit_code=0
  - 结果: `[hotspot] PASS - all .cs files within line budget (max=1200)`

## 热点复核
- 工具条入口: `OnBoardClick` 只根据 `ToolbarBoardClickActionPolicy` 选择弹层、退出或进入白板，避免 PDF/图片全屏被 photo mode 分支直接吃掉。
- 工具切换: `PrepareForNonBoardToolbarAction()` 不再包含 `exitWhiteboard` 参数，也不再调用 `SetBoardActive(false)`。
- 截图选区: 初始工具条区域只忽略 pointer move 造成的 passthrough cancel，仍保留后续指针离开与显式按压取消能力。
- 白板底色: 颜色选择成功后先应用底色，再进入白板，保持用户点击颜色即进入的触屏路径。

## 回滚
- 回滚入口策略: 移除 `ToolbarBoardClickActionPolicy`，恢复 `OnBoardClick` 原内联分支。
- 回滚截图修复: 移除 `deferInitialPassthroughCancelUntilPointerLeaves` 参数和初始 passthrough hover 忽略逻辑。
- 回滚工具切换: 恢复 `PrepareForNonBoardToolbarAction(exitWhiteboard: ...)` 与 `ExitWhiteboardForToolSwitchIfNeeded()`。
- 回滚底色进入: 将 `OnBoardColorActionClick` 恢复为仅打开颜色对话框。
