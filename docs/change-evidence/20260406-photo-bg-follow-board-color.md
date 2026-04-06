# 2026-04-06 图片全屏背景跟随白板最近背景色

- Rule IDs: R1, R2, R6, R8
- Risk: Low
- Scope:
  - `src/ClassroomToolkit.App/Services/PaintWindowOrchestrator.cs`
  - `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Board.cs`

## 依据
- 用户确认实现：PDF/图片全屏背景跟随白板最近背景色；白板背景以白/黑/绿为主。
- 回归问题根因：退出白板时将 Overlay board color 置为透明，导致图片窗口无法读取“最近白板色”。

## 变更
- 退出白板时不再清空 board color，仅将 board opacity 置为 0。
- board color 变更时，无论是否处于白板激活态，都会同步到 overlay 内部颜色状态。
- 图片窗口背景由统一方法 `ResolvePhotoWindowBackgroundBrush()` 计算，直接使用最近 board color。
- board color 更新时，若在图片模式，实时刷新 `PhotoWindowFrame.Background`。

## 命令
1. `dotnet build ClassroomToolkit.sln -c Debug`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 证据
- build: PASS (0 warning, 0 error)
- test(full): PASS (3177 passed)
- test(contract/invariant): PASS (25 passed)
- hotspot: PASS

## 回滚
- 回滚上述四个文件到本次变更前版本。
- 若仅需功能开关回退：恢复 `SetBoardColor(Colors.Transparent)` 旧逻辑与 `PhotoWindowFrame.Background` 资源色逻辑。
