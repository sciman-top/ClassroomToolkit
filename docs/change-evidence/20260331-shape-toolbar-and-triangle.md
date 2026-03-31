# 20260331-shape-toolbar-and-triangle

- rule_id: `R1 R2 R6 R8`
- risk_level: `medium`
- scope: `Paint toolbar shape entry + shape drawing pipeline`

## 依据
- 用户需求：在工具条撤销按钮后新增“图形”按钮，长按选择图形，短按启用最近图形；图标随最近图形变化；新增实/虚箭头、空心/实心矩形、任意三角形两段式交互。

## 变更落点
- `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
- `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
- `src/ClassroomToolkit.App/Paint/PaintShapeType.cs`
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Geometry.cs`
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs`
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs`
- `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs`
- `src/ClassroomToolkit.App/Settings/AppSettingsService.cs`
- `src/ClassroomToolkit.App/Assets/Styles/Icons.xaml`

## 命令与证据
- cmd: `codex status`
  - exit_code: `1`
  - key_output: `Error: stdin is not a terminal`
  - timestamp: `2026-03-31`
- cmd: `codex --version`
  - exit_code: `0`
  - key_output: `codex-cli 0.117.0`
  - timestamp: `2026-03-31`
- cmd: `codex --help`
  - exit_code: `0`
  - key_output: `Codex CLI usage printed`
  - timestamp: `2026-03-31`
- cmd: `Get-Command dotnet`
  - exit_code: `0`
  - key_output: `dotnet.exe found`
  - timestamp: `2026-03-31`
- cmd: `Get-Command powershell`
  - exit_code: `0`
  - key_output: `powershell.exe found`
  - timestamp: `2026-03-31`
- cmd: `Test-Path tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`
  - exit_code: `0`
  - key_output: `True`
  - timestamp: `2026-03-31`
- cmd: `dotnet build ClassroomToolkit.sln -c Debug`
  - exit_code: `0`
  - key_output: `0 warnings, 0 errors`
  - timestamp: `2026-03-31`
- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - exit_code: `0`
  - key_output: `passed=3025 failed=0`
  - timestamp: `2026-03-31`
- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code: `0`
  - key_output: `passed=24 failed=0`
  - timestamp: `2026-03-31`
- cmd: `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
  - exit_code: `0`
  - key_output: `[hotspot] status=PASS`
  - timestamp: `2026-03-31`

## N/A 记录
- type: `platform_na`
  - reason: `codex status 在非交互终端返回 stdin is not a terminal`
  - alternative_verification: `使用 codex --version 与 codex --help 完成平台能力补证`
  - evidence_link: `docs/change-evidence/20260331-shape-toolbar-and-triangle.md`
  - expires_at: `2026-04-30`

## 回滚动作
1. `git checkout -- src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
2. `git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Geometry.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs`
3. `git checkout -- src/ClassroomToolkit.App/Paint/PaintShapeType.cs src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs src/ClassroomToolkit.App/Settings/AppSettingsService.cs src/ClassroomToolkit.App/Assets/Styles/Icons.xaml`
4. 重新执行硬门禁链路确认回滚后状态。
