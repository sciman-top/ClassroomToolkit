规则ID=R1,R2,R5,R6,R8
影响模块=
- tests/ClassroomToolkit.Tests/CrossPageInputResumeExecutionContractTests.cs
- tests/ClassroomToolkit.Tests/PaintOverlayDrawingStateContractTests.cs
- tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.cs
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Telemetry.cs
当前落点=部分契约测试仍绑定单一 Input 文件路径，阻碍后续 partial 拆分；Input.cs 末尾包含 telemetry 空实现
目标归宿=契约测试改为 Input*.cs 聚合断言；跨页首笔 trace 方法归位至 Input.Telemetry.cs
迁移批次=2026-04-09-maintainability-hardening-batch27
风险等级=低
执行命令=
- dotnet build ClassroomToolkit.sln -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPageInputResumeExecutionContractTests|FullyQualifiedName~PaintOverlayDrawingStateContractTests|FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests"
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
- powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=
- build 通过：0 errors / 0 warnings
- 定向契约测试通过：8 passed / 0 failed
- 全量测试通过：3199 passed / 0 failed
- contract/invariant 通过：25 passed / 0 failed
- hotspot 通过：status=PASS
- 热点体量变化：PaintOverlayWindow.Input.cs 从 1112 降到 1099（预算 1650）
回滚动作=
- git checkout -- tests/ClassroomToolkit.Tests/CrossPageInputResumeExecutionContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/PaintOverlayDrawingStateContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.cs
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Telemetry.cs

# Backfill 2026-04-03
影响模块=legacy-governance-evidence
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
