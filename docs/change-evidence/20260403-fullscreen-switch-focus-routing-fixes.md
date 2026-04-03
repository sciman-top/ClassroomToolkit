规则ID=R1/R2/R4/R6/R8
影响模块=src/ClassroomToolkit.App/Paint, src/ClassroomToolkit.App, tests/ClassroomToolkit.Tests, docs/validation
当前落点=全屏互切/焦点恢复/辅助窗口输入路由
目标归宿=PPT/WPS/PDF/图片/白板互切与前台焦点/置顶逻辑稳定
迁移批次=20260403-2
风险等级=中
执行命令=1) dotnet build ClassroomToolkit.sln -c Debug; 2) dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseSharedCompilation=false; 3) dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseSharedCompilation=false --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; 4) powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=build PASS; test PASS(3123); contract/invariant PASS(25); hotspot PASS; 详见本文第4节
回滚动作=回滚本次涉及文件并重跑 build->test->contract/invariant->hotspot

# 变更证据：全屏互切/焦点恢复/辅助窗口输入路由修复（2026-04-03）

## 1. 依据

- 需求：PPT/WPS 全屏、PDF/图片全屏、白板互切时，工具条/点名/启动器保持前置；光标/绘图模式、键盘/滚轮翻页、焦点与笔迹行为一致。
- 审查发现：
- 演示焦点恢复门禁未启用。
- 辅助窗口键盘路由存在“误吞键”。
- 辅助窗口缺少滚轮翻页转发链路。
- 图片重入时全屏状态被硬重置。

## 2. 变更文件

- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs`
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Presentation.cs`
- `src/ClassroomToolkit.App/Paint/AuxWindowKeyRoutingHandler.cs`
- `src/ClassroomToolkit.App/Paint/AuxWindowWheelRoutingHandler.cs`
- `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
- `src/ClassroomToolkit.App/MainWindow.Photo.cs`
- `src/ClassroomToolkit.App/RollCallWindow.xaml.cs`
- `src/ClassroomToolkit.App/RollCallWindow.Input.cs`
- `src/ClassroomToolkit.App/RollCallWindow.Windowing.cs`
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.cs`
- `tests/ClassroomToolkit.Tests/AuxWindowKeyRoutingHandlerTests.cs`
- `tests/ClassroomToolkit.Tests/AuxWindowWheelRoutingHandlerTests.cs`
- `docs/validation/manual-fullscreen-switch-regression-matrix-20260403.md`

## 3. 执行命令

1. `dotnet build ClassroomToolkit.sln -c Debug`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseSharedCompilation=false`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseSharedCompilation=false --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 4. 结果证据

- build：通过（0 warning / 0 error）
- test（全量）：通过（3123 passed / 0 failed）
- contract/invariant 子集：通过（25 passed / 0 failed）
- hotspot：`PASS`

## 5. 风险等级

- 变更风险：`中`
- 原因：涉及输入路由和前台焦点行为，存在环境差异敏感性（Office/WPS 版本、DPI、多屏）。

## 6. 回滚动作

1. 回滚本次提交涉及文件。
2. 重新执行第 3 节门禁命令确认回滚后稳定。
3. 按 `docs/validation/manual-fullscreen-switch-regression-matrix-20260403.md` 对关键场景复验。

## 7. N/A 记录

- `platform_na`
- reason：`codex status` 在非交互终端报错 `stdin is not a terminal`。
- alternative_verification：通过代码证据 + build/test/contract/hotspot 全链验证补证。
- evidence_link：本文档与本次命令输出记录。
- expires_at：`2026-06-30`
