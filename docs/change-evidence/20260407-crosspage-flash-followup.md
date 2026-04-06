规则ID=R1/R2/R3/R6/R8
影响模块=src/ClassroomToolkit.App/Paint/CrossPageInteractiveInkSlotRemapPolicy.cs; src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.cs; tests/ClassroomToolkit.Tests/CrossPageInteractiveInkSlotRemapPolicyTests.cs; tests/ClassroomToolkit.Tests/CrossPageInputResumeExecutionContractTests.cs
当前落点=跨页交互槽位重映射与续笔首段插值
目标归宿=减少跨页前页笔迹闪烁；提升跨页首段跟手流畅度
迁移批次=20260407-2
风险等级=中
执行命令=
- dotnet build ClassroomToolkit.sln -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPageInteractiveInkSlotRemapPolicyTests|FullyQualifiedName~CrossPageInputResumeExecutionContractTests"
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
- powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=
- 重映射策略：inkOperationActive 且有 preserved frame 时改为 UsePreservedFrame（不再先 ClearCurrentFrame）。
- 重映射策略（二次收敛）：inkOperationActive 且无 preserved frame 时也不再 Clear，改为 KeepCurrentFrame，优先视觉连续性。
- 续笔首段：AppendCrossPageContinuationSamples 再加密（步长 0.9 DIP、最大 64 段）。
- 相关策略/契约测试通过，完整门禁通过。
回滚动作=
- git restore --source=HEAD~1 src/ClassroomToolkit.App/Paint/CrossPageInteractiveInkSlotRemapPolicy.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.cs tests/ClassroomToolkit.Tests/CrossPageInteractiveInkSlotRemapPolicyTests.cs tests/ClassroomToolkit.Tests/CrossPageInputResumeExecutionContractTests.cs
