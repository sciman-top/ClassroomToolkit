规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Settings.cs
当前落点=PaintOverlayWindow 根文件中的 Ink/Photo 配置更新与切换逻辑
目标归宿=将 Ink/Photo 设置切换方法组迁移至独立 partial，降低根文件复杂度并保持行为不变
迁移批次=2026-04-08-maintainability-hardening-batch11
风险等级=低
执行命令=
- dotnet build ClassroomToolkit.sln -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PaintOverlayClearAllCrossPageRecoveryContractTests|FullyQualifiedName~PaintOverlayEventCallbackSafetyContractTests|FullyQualifiedName~OverlayDeferredBoundsRecoveryContractTests|FullyQualifiedName~PaintOverlaySetModeDispatchFallbackContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
- powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=
- build 通过：0 errors / 0 warnings
- 相关定向契约测试通过：14 passed / 0 failed
- 全量测试通过：3199 passed / 0 failed
- contract/invariant 通过：25 passed / 0 failed
- hotspot 通过：status=PASS
- 热点体量下降：PaintOverlayWindow.xaml.cs 从 1450 预算基线下降到 1053（delta -397）
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Settings.cs
