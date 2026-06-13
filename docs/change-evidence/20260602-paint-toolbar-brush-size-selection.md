# 2026-06-02 画笔工具条粗细选择修复

## 规则与风险
- 规则：R1/R2/R6/R8；本仓 `build -> test -> contract/invariant -> hotspot` 顺序。
- 风险等级：低到中。范围限于画笔工具条快捷画笔、粗细弹窗、颜色气泡热区与设置保存同步。

## 依据
- 现象：红笔打开粗细弹窗后选择第三档最大粗细时，旧逻辑把 `selectedSizeIndex` 直接传给 `ApplyQuickColorSelection`，导致第 3 个粗细选项被解释成第 3 个快捷画笔槽，误切到蓝笔。
- 触控边界：工具条快捷画笔按钮的命中热区必须独立于当前笔画粗细，视觉圆点可变小，但可点击区域保持大。

## 改动
- `PaintToolbarWindow`：粗细选择改为 `ApplyQuickBrushSizeSelection(index, selectedSizeIndex)`，只更新当前打开的快捷画笔槽，并继续应用该槽颜色。
- `PaintWindowOrchestrator`：新增快捷粗细槽变化事件保存，保证弹窗选择后的槽位粗细进入设置。
- `QuickColorPaletteWindow`：当前粗细选项使用更粗边框、浅色背景和加粗预览，提升可见性。
- `WidgetStyles.xaml`：`Style_ColorBubbleToggle` 模板外层改为透明大热区，视觉圆点居中但外圈仍可点击。
- `WidgetStyles.xaml` / `PaintToolbarWindow`：快捷画笔圆形图标固定为统一尺寸，不再随快捷粗细改变；圆点描边改为对比前景色，选中外环改为统一高亮边线，提升黑笔可视度。
- 测试：更新工具条触控/粗细契约与回调安全契约，禁止 `ApplyQuickColorSelection(selectedSizeIndex)` 回归。

## 验证
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PaintToolbarTouchSettingsContractTests|FullyQualifiedName~PaintToolbarEventCallbackSafetyContractTests"`：通过，2 个测试。
- `dotnet build ClassroomToolkit.sln -c Debug`：通过，0 warning / 0 error。
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`：通过，3524 个测试。
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`：通过，29 个测试。
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`：通过。
- `git diff --check`：通过；仅输出本机 CRLF 提醒，无 whitespace 错误。

### 追加验证：快捷画笔图标统一尺寸与对比边线
- 默认 `dotnet build ClassroomToolkit.sln -c Debug`：被运行中的 `sciman Classroom Toolkit (30532)` 锁住默认输出 exe，未结束用户进程；按 `platform_na/gate_na` 记录替代验证。
- `dotnet build ClassroomToolkit.sln -c Debug "-p:OutDir=D:\CODE\ClassroomToolkit\artifacts\verify\build\"`：通过，0 warning / 0 error。
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug "-p:OutDir=D:\CODE\ClassroomToolkit\artifacts\verify\test\"`：仅因本机缺失 `D:\CODE\Baselines\brush-dpi-golden.json` 失败 1 项，3523 项通过。
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug "-p:OutDir=D:\CODE\ClassroomToolkit\artifacts\verify\test-no-dpi\" --filter "FullyQualifiedName!=ClassroomToolkit.Tests.BrushDpiGoldenRegressionTests.DpiGoldenHashes_ShouldMatchBaseline"`：通过，3523 个测试。
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug "-p:OutDir=D:\CODE\ClassroomToolkit\artifacts\verify\contract\" --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`：通过，29 个测试。
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug "-p:OutDir=D:\CODE\ClassroomToolkit\artifacts\verify\paint-toolbar\" --filter "FullyQualifiedName~PaintToolbarTouchSettingsContractTests|FullyQualifiedName~PaintToolbarEventCallbackSafetyContractTests"`：通过，2 个测试。
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`：通过。
- `git diff --check`：通过；仅输出本机 CRLF 提醒，无 whitespace 错误。

## 热点复核
- 粗细选择路径：选项索引用于读取粗细预设值，快捷画笔槽位沿用打开弹窗时的 `index`，不会再误切颜色。
- 设置同步：粗细槽位事件与当前 `BrushSize` 保存同步，避免重启/重新应用设置后丢失弹窗选择。
- 触控热区：颜色气泡模板外层 `Grid` 有透明背景与 `TemplateBinding MinWidth/MinHeight`，点击区域不随圆点粗细缩小。
- 统一图标：颜色气泡 `Dot` 固定为 22 DIP，`UpdateQuickColorButton` 不再按 `_quickBrushSizes[index]` 修改 `FontSize`。
- 对比边线：颜色气泡 `Dot` 使用 `TemplateBinding Foreground` 做描边；黑笔得到浅色边线，白笔得到深色边线。

## 回滚
- 回滚 `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`、`src/ClassroomToolkit.App/Paint/QuickColorPaletteWindow.xaml.cs`、`src/ClassroomToolkit.App/Services/PaintWindowOrchestrator.cs`、`src/ClassroomToolkit.App/Assets/Styles/WidgetStyles.xaml` 及对应测试文件中的本次改动。
