规则ID=R2,R5,R6,R8
影响模块=
- tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs
- tests/ClassroomToolkit.Tests/PaintOverlayEventCallbackSafetyContractTests.cs
- tests/ClassroomToolkit.Tests/PaintOverlayClearAllCrossPageRecoveryContractTests.cs
- tests/ClassroomToolkit.Tests/PhotoPanInertiaRenderingContractTests.cs
当前落点=多处契约测试将 Transform 行为断言绑定在单文件 PaintOverlayWindow.Photo.Transform.cs
目标归宿=改为读取 PaintOverlayWindow.Photo.Transform*.cs 聚合源码，保持行为契约同时支持 partial 模块化
迁移批次=2026-04-08-test-hardening-batch16
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
回滚动作=
- git checkout -- tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/PaintOverlayEventCallbackSafetyContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/PaintOverlayClearAllCrossPageRecoveryContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/PhotoPanInertiaRenderingContractTests.cs
