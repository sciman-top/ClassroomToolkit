规则ID=R1,R2,R3,R5,R6,R8
影响模块=
- tests/ClassroomToolkit.Tests/OverlayDeferredBoundsRecoveryContractTests.cs
- tests/ClassroomToolkit.Tests/PaintOverlaySetModeDispatchFallbackContractTests.cs
- tests/ClassroomToolkit.Tests/PaintOverlayEventCallbackSafetyContractTests.cs
- tests/ClassroomToolkit.Tests/PaintOverlayClearAllCrossPageRecoveryContractTests.cs
- tests/ClassroomToolkit.Tests/ImageManagerWindowDispatchFallbackContractTests.cs
- tests/ClassroomToolkit.Tests/ImageManagerEventCallbackSafetyContractTests.cs
- tests/ClassroomToolkit.Tests/ContractSourceAggregateLoader.cs
- src/ClassroomToolkit.Services/Presentation/PresentationDiagnosticsProbe.cs
当前落点=契约测试稳定性与诊断探针阻塞等待路径
目标归宿=将单文件字符串契约升级为跨 partial 聚合契约，去重测试读取逻辑；将探针等待改为 WaitAsync 并统一 hook finally 收尾，提升稳健性
迁移批次=2026-04-08-stability-hardening-batch5-7
风险等级=低
执行命令=
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PaintOverlaySetModeDispatchFallbackContractTests|FullyQualifiedName~PaintOverlayEventCallbackSafetyContractTests|FullyQualifiedName~PaintOverlayClearAllCrossPageRecoveryContractTests|FullyQualifiedName~OverlayDeferredBoundsRecoveryContractTests"
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PresentationDiagnosticsProbeBlockingSafetyContractTests"
- dotnet build ClassroomToolkit.sln -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
- powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=
- 相关定向契约测试通过：11 passed / 0 failed
- 诊断探针阻塞安全测试通过：1 passed / 0 failed
- build 通过：0 errors / 0 warnings
- 全量测试通过：3199 passed / 0 failed
- contract/invariant 通过：25 passed / 0 failed
- hotspot 通过：status=PASS
- 结构收益：契约测试不再依赖单一 code-behind 文件，支持 partial 重构连续推进
回滚动作=
- git checkout -- tests/ClassroomToolkit.Tests/OverlayDeferredBoundsRecoveryContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/PaintOverlaySetModeDispatchFallbackContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/PaintOverlayEventCallbackSafetyContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/PaintOverlayClearAllCrossPageRecoveryContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/ImageManagerWindowDispatchFallbackContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/ImageManagerEventCallbackSafetyContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/ContractSourceAggregateLoader.cs
- git checkout -- src/ClassroomToolkit.Services/Presentation/PresentationDiagnosticsProbe.cs
