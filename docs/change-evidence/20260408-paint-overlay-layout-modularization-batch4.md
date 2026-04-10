规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Layout.cs
- tests/ClassroomToolkit.Tests/OverlayDeferredBoundsRecoveryContractTests.cs
当前落点=App/Paint Overlay window 主实现
目标归宿=将窗口样式/焦点/输入穿透/全屏恢复调度逻辑迁移到 Layout partial，主文件聚焦业务编排；同步契约测试改为跨 partial 聚合校验
迁移批次=2026-04-08-maintainability-hardening-batch4
风险等级=低
执行命令=
- dotnet build ClassroomToolkit.sln -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~OverlayDeferredBoundsRecoveryContractTests|FullyQualifiedName~ImageManagerWindowDispatchFallbackContractTests|FullyQualifiedName~ImageManagerEventCallbackSafetyContractTests"
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
- powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=
- build 通过：0 errors / 0 warnings
- 定向契约测试通过：3 passed / 0 failed
- 全量测试通过：3199 passed / 0 failed
- contract/invariant 通过：25 passed / 0 failed
- hotspot 通过：status=PASS
- 热点体量下降：PaintOverlayWindow.xaml.cs 从 1450 预算基线下降到 1238（delta -212）
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Layout.cs
- git checkout -- tests/ClassroomToolkit.Tests/OverlayDeferredBoundsRecoveryContractTests.cs

# Backfill 2026-04-03
影响模块=legacy-governance-evidence
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
