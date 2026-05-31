# 2026-05-31 画笔快捷粗细与 PDF/图片撤销修复证据

## 规则与风险
- 规则 ID: R1/R2/R3/R6/R8, E4/E6
- 风险等级: 中。涉及课堂实时画笔 UI、设置持久化、PDF/图片全屏墨迹撤销与 sidecar 持久化同步。
- 边界: 仅处理画笔工具条、画笔设置、快捷笔槽粗细、PDF/图片墨迹撤销链路与对应回归契约；不整理现有规则文件、WPS hook、RollCall 等无关工作树漂移。

## 依据
- 画笔工具条已有 3 个快捷画笔按钮；用户要求同一画笔按钮二次点击弹出的颜色框同时列出 3 个笔画粗细选择。
- 画笔设置需要 3 个笔画粗细滑条，并作为工具条 3 个快捷笔槽的来源。
- PDF/图片全屏清空后撤销只恢复当时画面，移动/缩放后又消失，说明 undo 没有同步运行时 empty guard、缓存和 sidecar 持久化状态。

## 变更
- `AppSettings` 新增 `QuickBrushSize1/2/3`，`AppSettingsService` 读写 `quick_brush_size_1/2/3` 并 clamp 到 1-50。
- `PaintSettingsDialog` 将单一笔画粗细扩展为 3 个滑条；笔画、橡皮、不透明度右侧均显示实时圆点预览；工具栏 tab 不再显示图形工具配置。
- `PaintToolbarWindow` 将 3 个快捷按钮与 3 个粗细槽绑定，按钮圆点大小随笔槽粗细变化；二次点击/长按弹出的 `QuickColorPaletteWindow` 同时显示颜色和 3 个粗细选择。
- `PaintWindowOrchestrator` 保存当前画笔颜色时同步保存当前粗细，初始化 overlay 时使用工具条解析后的快捷粗细。
- `PaintOverlayWindow` 的 PDF/图片全局 undo 在成功应用后才弹出全局历史；恢复当前页时移除匹配的本地重复历史、取消待执行 autosave、同步持久化恢复后的墨迹并触发 cross-page 刷新；恢复非当前页时同步更新/失效缓存并持久化。

## 命令与证据
- `codex --version`
  - `codex-cli 0.135.0`
- `codex --help`
  - exit_code=0，help 中可见 `exec/review/login/logout/mcp/plugin/doctor/sandbox/debug/features`。
- `codex status`
  - platform_na: 非交互环境报错 `Error: stdin is not a terminal`
  - alternative_verification: 已记录 `codex --version` 与 `codex --help`，代码门禁使用 dotnet 命令。
  - expires_at: 2026-06-30
- `dotnet build ClassroomToolkit.sln -c Debug`
  - exit_code=0
  - 结果: `已成功生成。0 个警告 0 个错误`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - exit_code=0
  - 结果: `已通过! - 失败: 0，通过: 3508，已跳过: 0，总计: 3508`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code=0
  - 结果: `已通过! - 失败: 0，通过: 29，已跳过: 0，总计: 29`

## 热点复核
- 工具条热点: 3 个快捷画笔按钮使用各自粗细槽；二次点击同一按钮进入 `QuickColorPaletteWindow`，颜色行和粗细行同时存在；选择粗细会切到对应快捷笔槽。
- 设置热点: 基础页显示 3 个笔画粗细滑条；橡皮和不透明度均有右端圆点预览；工具栏页仅保留工具条缩放，不显示“图形工具”。
- PDF/图片 undo 热点: 清空/区域删除后的 undo 会恢复 `_inkStrokes`、更新 runtime hash、取消旧 autosave generation、同步 sidecar，并触发 cross-page display update，避免后续移动/缩放被 `empty` guard 再次清空。

## 回滚
- 回滚 UI/设置: 移除 `QuickBrushSize1/2/3`、恢复单一 `BrushSizeSlider` 与旧 `brush_base_size` 读写路径。
- 回滚工具条: 取消 `QuickColorPaletteWindow` 的粗细行，恢复快捷按钮只表示颜色。
- 回滚 undo: 恢复 `TryUndoAcrossPages` 原逻辑，并移除 `PersistUndoRestoredPhotoInkSnapshot` / `RemoveMatchingCurrentInkHistorySnapshot` 调用。
