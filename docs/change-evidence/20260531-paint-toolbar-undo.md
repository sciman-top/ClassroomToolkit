# 2026-05-31 画笔工具条三档粗细与 PDF/图片撤销修复

- 规则 ID：R1/R2/R3/R6/R7/R8，E4/E6
- 风险等级：中。涉及 WPF 画笔设置、工具条交互、PDF/图片笔迹撤销运行态与持久化状态。
- 当前落点：`D:\CODE\ClassroomToolkit`
- 目标归宿：画笔工具条支持三档可配置笔画粗细，设置页去重并显示实时效果；PDF/图片全屏擦除/区域删除/清空后撤销要真实恢复运行态、缓存与持久化状态。

## 依据

- 用户反馈：同一画笔按钮二次点击应在颜色弹窗中同时列出 3 个笔画粗细；设置页工具栏 tab 不再重复显示“图形工具”；笔画/橡皮/不透明度滑条右端需要实时圆点效果；PDF/图片全屏撤销后移动/缩放笔迹又消失。
- 根因：
  - 旧实现只有一个 `BrushSize`，工具条 3 个快捷画笔按钮共用同一粗细。
  - PDF/图片撤销优先走 `_globalInkHistory`，但应用快照后没有同步清理重复本地历史，也没有立即把恢复后的笔迹写回 sidecar，容易被此前的 empty 状态或延迟保存覆盖。

## 命令与证据

- `dotnet build ClassroomToolkit.sln -c Debug`
  - 结果：PASS，0 warnings，0 errors。
- `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~PaintToolbarTouchSettingsContractTests|FullyQualifiedName~PaintSettingsDialogBrushPresetContractTests|FullyQualifiedName~PaintOverlayClearAllCrossPageRecoveryContractTests|FullyQualifiedName~AppSettingsServiceTests|FullyQualifiedName~AppSettingsDefaultsTests"`
  - 结果：PASS，37/37。
- `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug`
  - 说明：前两次全量出现本机瞬时失败，分别为架构读取旧状态、`File.Replace` IO、性能比例波动；对应失败项单独复跑均通过。
  - 最终结果：PASS，3508/3508。
- `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - 结果：PASS，29/29。
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality\check-hotspot-line-budgets.ps1`
  - 结果：PASS，all .cs files within line budget。

## 回滚

- 代码回滚：撤回本次对 `PaintToolbarWindow*`、`QuickColorPaletteWindow*`、`PaintSettingsDialog*`、`AppSettings*`、`PaintWindowOrchestrator.cs`、`MainWindow.Paint.cs`、`PaintOverlayWindow.HistoryAndTransform.cs` 以及对应测试文件的改动。
- 数据兼容：新增 `quick_brush_size_1/2/3` 设置键；旧配置缺失时使用默认值，不需要迁移。回滚后这些键会被旧版本忽略。
