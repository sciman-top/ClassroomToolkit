规则ID=R1,R2,R3,R4,R6,R8
影响模块=RollCallWindow/MainWindow/PaintToolbarWindow/PhotoOverlayWindow/Windowing
当前落点=src/ClassroomToolkit.App/*
目标归宿=窗口层级协调与点名照片展示时序稳定化
迁移批次=2026-04-09-bugfix-rollcall-minimize-zorder
风险等级=中
执行命令=codex status(非交互失败), codex --version, codex --help, dotnet build ClassroomToolkit.sln -c Debug, dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug, dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests", powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=build/test/contract/hotspot 全通过；新增 FloatingTopmostDialogSuppressionStateTests 通过
回滚动作=git checkout -- src/ClassroomToolkit.App/MainWindow.xaml.cs src/ClassroomToolkit.App/RollCallWindow.xaml.cs src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs src/ClassroomToolkit.App/RollCallWindow.Input.cs src/ClassroomToolkit.App/RollCall/RollCallRemoteHookActionExecutionCoordinator.cs src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs tests/ClassroomToolkit.Tests/App/FloatingTopmostDialogSuppressionStateTests.cs src/ClassroomToolkit.App/Windowing/FloatingTopmostDialogSuppressionState.cs

platform_na:
- reason=codex status 在当前非交互终端触发 "stdin is not a terminal"
- alternative_verification=使用 codex --version 与 codex --help 校验 CLI 可用性
- evidence_link=本次终端执行日志（2026-04-09）
- expires_at=2026-05-09
补充修复（同日二次迭代）:
- 修正照片自动关闭计时竞态：由“请求即开计时”改为“记录截止时间，图片显示后按剩余时长关闭；若加载已超时，最少展示 450ms”。
- 新增策略与测试：PhotoOverlayAutoClosePolicy + PhotoOverlayAutoClosePolicyTests。

补充验证:
- dotnet test ... --filter "...PhotoOverlayAutoClosePolicyTests..." 通过
- dotnet test 全量 3205 通过
补充修复（同日三次迭代）:
- 覆盖窗照片路径统一到点名窗口同源：RollCallWindow.UpdatePhotoDisplay 改为直接使用 _viewModel.CurrentStudentPhotoPath。
- 移除 RollCallWindow 层的重复 StudentPhotoResolver 生命周期管理，避免双路径分歧。
- 同步更新契约测试：RollCallWindowPhotoOverlayReuseContractTests、RollCallWindowLifecycleSubscriptionContractTests。

补充验证:
- dotnet test 全量 3205 通过
- contract/invariant 25 通过
- hotspot PASS
补充修复（同日四次迭代）:
- 最小化场景照片路径进一步统一：RollCallWindow 覆盖窗直接复用 ViewModel.CurrentStudentPhotoPath；移除窗口层重复 resolver。
- 覆盖窗实例复用：切换学生不再先 ClosePhotoOverlay 重建窗口。
- 覆盖窗加载性能优化：移除 IgnoreImageCache，并按屏幕尺寸设置 DecodePixelWidth（含上限）。
- 新增契约测试：PhotoOverlayLoadBitmapDecodeContractTests。

补充验证:
- dotnet test 全量 3206 通过
补充修复（同日五次迭代，按用户反馈）:
- 覆盖窗改为非全屏窗口：移除 WindowState=Maximized，采用窗口化居中展示。
- 恢复 3 个关闭按钮：Left/Center/Right 全保留并布局到照片底部。
- 自动关闭回退为“图片实际显示后再计时”，取消预扣截止时间机制，修复一闪即逝/未显示。

补充验证:
- dotnet test 全量 3206 通过（一次文件锁偶发后重跑通过）
