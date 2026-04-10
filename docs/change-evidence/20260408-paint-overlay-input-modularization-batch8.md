规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.cs
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Manipulation.cs
当前落点=PaintOverlayWindow 输入处理实现
目标归宿=将手势 Manipulation 相关方法拆分到独立 partial，降低 Input 主文件复杂度并保持行为不变
迁移批次=2026-04-08-maintainability-hardening-batch8
风险等级=低
执行命令=
- dotnet build ClassroomToolkit.sln -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPageInputResumeExecutionContractTests|FullyQualifiedName~PaintOverlayDrawingStateContractTests|FullyQualifiedName~PaintOverlaySetModeDispatchFallbackContractTests|FullyQualifiedName~PaintOverlayEventCallbackSafetyContractTests|FullyQualifiedName~PaintOverlayClearAllCrossPageRecoveryContractTests|FullyQualifiedName~OverlayDeferredBoundsRecoveryContractTests"
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
- powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=
- build 通过：0 errors / 0 warnings
- 相关定向契约测试通过：12 passed / 0 failed
- 全量测试通过：3199 passed / 0 failed
- contract/invariant 通过：25 passed / 0 failed
- hotspot 通过：status=PASS
- 热点体量下降：PaintOverlayWindow.Input.cs 从 1650 预算基线下降到 1447（delta -203）
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.cs
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Manipulation.cs

# Backfill 2026-04-03
影响模块=legacy-governance-evidence
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
