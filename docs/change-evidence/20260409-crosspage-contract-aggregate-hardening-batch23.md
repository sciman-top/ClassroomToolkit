规则ID=R2,R5,R6,R8
影响模块=
- tests/ClassroomToolkit.Tests/CrossPageDisplayLifecycleContractTests.cs
- tests/ClassroomToolkit.Tests/CrossPageNeighborInkRenderSurfaceContractTests.cs
- tests/ClassroomToolkit.Tests/PaintOverlayClearAllCrossPageRecoveryContractTests.cs
当前落点=CrossPage 相关契约对单文件 PaintOverlayWindow.Photo.CrossPage.cs 绑定较强，重构空间受限
目标归宿=改为读取 PaintOverlayWindow.Photo.CrossPage*.cs 聚合源码，保持契约语义并提升模块化兼容性
迁移批次=2026-04-09-test-hardening-batch23
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
- git checkout -- tests/ClassroomToolkit.Tests/CrossPageDisplayLifecycleContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/CrossPageNeighborInkRenderSurfaceContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/PaintOverlayClearAllCrossPageRecoveryContractTests.cs
