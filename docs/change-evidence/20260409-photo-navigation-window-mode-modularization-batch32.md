规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.cs
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.WindowMode.cs
- tests/ClassroomToolkit.Tests/PaintOverlayEventCallbackSafetyContractTests.cs
- tests/ClassroomToolkit.Tests/PaintOverlayWhiteboardPhotoCacheContractTests.cs
- tests/ClassroomToolkit.Tests/PhotoFullscreenBoundsEnforcementContractTests.cs
- tests/ClassroomToolkit.Tests/PhotoSaveNavigationContractTests.cs
- tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs
当前落点=Photo.Navigation.cs 同时承载导航流程与窗口模式/边界执行逻辑，且多处契约测试绑定单文件路径
目标归宿=窗口模式与全屏边界方法组迁移到 Navigation.WindowMode partial；导航契约改为 Navigation*.cs 聚合读取
迁移批次=2026-04-09-maintainability-hardening-batch32
风险等级=低
执行命令=
- dotnet build ClassroomToolkit.sln -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
- powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=
- build 通过：0 errors / 0 warnings
- 全量测试通过：3199 passed / 0 failed
- contract/invariant 通过：25 passed / 0 failed
- hotspot 通过：status=PASS
- 热点体量变化：PaintOverlayWindow.Photo.Navigation.cs 从 1122 降到 839（预算 1340）
- 过程说明：出现一次 MSB3026（testhost 占用测试 dll）自动重试后通过，不影响结果
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.cs
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.WindowMode.cs
- git checkout -- tests/ClassroomToolkit.Tests/PaintOverlayEventCallbackSafetyContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/PaintOverlayWhiteboardPhotoCacheContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/PhotoFullscreenBoundsEnforcementContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/PhotoSaveNavigationContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs
