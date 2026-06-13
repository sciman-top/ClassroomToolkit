# 2026-06-02 Photo/PDF 撤销运行态历史修复

## 规则与风险
- 规则：R3 根因优先，R6 硬门禁，R7 兼容保护，R8 可追溯。
- 风险等级：中低。改动限定在 PDF/图片批注 undo 运行态历史策略，不改变持久化 JSON 格式。

## 依据
- 现象：PDF/图片全屏批注中，清空/擦除后撤销会暂时恢复笔迹，但移动或缩放后笔迹又消失；手写后撤销会暂时消失，但移动或缩放后已撤销笔迹又重现。
- 根因：默认设置 `InkRecordEnabled=false` 时，photo/PDF 模式仍会记录 `_inkStrokes` 供重绘使用，但 `PushHistory` 和 `Undo` 仍被 `_inkRecordEnabled` 阻断，只剩 raster 快照。撤销只改变当前画面，没有更新后续重绘使用的向量模型、cache、dirty/sidecar 状态。

## 变更
- 新增 `InkUndoHistoryPolicy`，将“是否启用长期笔迹记录”和“photo/PDF 批注撤销必须维护运行态向量历史”分离。
- `PushHistory` 在 photo/PDF 批注模式下即使关闭笔迹记录，也记录向量 undo snapshot。
- `Undo` 在 photo 模式下不再依赖 `_inkRecordEnabled` 才进入 global/local vector undo 分支；local fallback 在 photo 模式下同步持久化并触发跨页刷新。
- 新增 `PhotoInkUndoHistoryPolicyTests`，覆盖策略与 `PaintOverlayWindow` 接线合同。

## 验证命令
- `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug --filter FullyQualifiedName~PhotoInkUndoHistoryPolicyTests`
  - 先失败：`CS0103: 当前上下文中不存在名称“InkUndoHistoryPolicy”`
  - 修复后通过：12 passed。
- `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoInkUndoHistoryPolicyTests|FullyQualifiedName~PaintOverlayClearAllCrossPageRecoveryContractTests"`
  - 18 passed。
- `dotnet build ClassroomToolkit.sln -c Debug`
  - 0 warning，0 error。
- `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug`
  - 3524 passed。
- `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - 29 passed。
- `git diff --check`
  - 无 whitespace error；仅有当前工作区 LF/CRLF 转换提示。
- `powershell -File scripts\quality\check-hotspot-line-budgets.ps1`
  - `[hotspot] PASS - all .cs files within line budget (max=1200)`

## Hotspot 复核
- `PaintOverlayWindow.Ink.History.cs`：入栈条件改为策略判定，保证 photo/PDF 批注撤销快照不受长期记录开关影响。
- `PaintOverlayWindow.HistoryAndTransform.cs`：photo 模式优先 vector undo；local fallback 恢复后同步 cache/dirty/sidecar/cross-page update，避免后续 pan/zoom 从旧模型重绘。
- `InkUndoHistoryPolicy.cs`：纯条件策略，无 UI/IO/持久化副作用。

## 回滚
- 删除 `src/ClassroomToolkit.App/Paint/InkUndoHistoryPolicy.cs` 和 `tests/ClassroomToolkit.Tests/PhotoInkUndoHistoryPolicyTests.cs`。
- 将 `PaintOverlayWindow.Ink.History.cs` 的入栈条件恢复为 `_inkRecordEnabled`。
- 将 `PaintOverlayWindow.HistoryAndTransform.cs` 的 undo 条件恢复为 `_inkRecordEnabled && ...`，并移除 local fallback 的 photo 持久化/跨页刷新补偿。
