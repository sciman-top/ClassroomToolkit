规则ID=R1,R2,R3,R4,R6,R8
影响模块=src/ClassroomToolkit.App/Paint + tests/ClassroomToolkit.Tests
当前落点=PaintOverlayWindow.xaml.cs + PaintOverlayWindow.Photo.Transform.cs + PaintOverlayClearAllCrossPageRecoveryContractTests.cs
目标归宿=光标模式下 clear-all 后，拖拽/缩放触发重绘时不再回灌旧笔迹；runtime=empty 页面只允许保持空态
迁移批次=2026-04-01-05
风险等级=中
执行命令=dotnet build ClassroomToolkit.sln -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests|FullyQualifiedName~PaintOverlayClearAllCrossPageRecoveryContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=build 0 error; 全量测试 3056 通过; contract/invariant+clearall-contract 27 通过; hotspot PASS
回滚动作=git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.cs tests/ClassroomToolkit.Tests/PaintOverlayClearAllCrossPageRecoveryContractTests.cs docs/change-evidence/20260401-cursor-clearall-panzoom-recovery-guard.md
