# 2026-04-06 白板截图态与工具条交互修复

- rule_id: R1/R2/R6/R8
- risk_level: medium
- scope: `src/ClassroomToolkit.App` 白板按钮、区域截图选择窗、截图工作流参数

## 依据
- 用户反馈：进入截图态后无法再点击白板按钮；期望鼠标进入工具条区域时退出截图态以便点击按钮；白板内点击白板按钮应退出白板。

## 命令
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 验证证据
- build: 0 error / 0 warning。
- test: `3177` passed, `0` failed。
- contract/invariant 子集: `25` passed, `0` failed。
- hotspot: `status=PASS`。

## 二次修正（同日）
- 新增截图取消原因：区分普通取消与“工具条豁免区取消”。
- 当取消原因为“工具条豁免区取消”时，下一次点击白板按钮将直接进入白板，不再触发截图。
- 修复白板状态失配时 `SetBoardActive(false)` 提前返回导致无法退出白板的问题。

## 三次修正（同日）
- 对“工具条豁免区取消截图”增加自动恢复机制：
  - 当光标停留在工具条区域：维持可点击工具条状态。
  - 当光标离开工具条区域：自动重新触发区域截图，十字光标恢复，无需再点一次白板。

## 回滚动作
- 回滚以下文件到变更前版本：
  - `src/ClassroomToolkit.App/MainWindow.Paint.cs`
  - `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
  - `src/ClassroomToolkit.App/Paint/RegionScreenCaptureWorkflow.cs`
  - `src/ClassroomToolkit.App/Paint/RegionSelectionOverlayWindow.xaml`
  - `src/ClassroomToolkit.App/Paint/RegionSelectionOverlayWindow.xaml.cs`
