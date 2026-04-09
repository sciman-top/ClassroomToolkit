规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.cs
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.PanInertia.cs
当前落点=Photo.Transform.cs 内平移、速度采样、惯性渲染与边界夹取方法集中，单文件理解成本高
目标归宿=将 Pan/Inertia 方法组迁移到 Transform.PanInertia partial，主文件聚焦坐标变换与状态持久化
迁移批次=2026-04-09-maintainability-hardening-batch33
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
- 热点体量变化：PaintOverlayWindow.Photo.Transform.cs 从 860 降到 454（预算 1100）
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.cs
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.PanInertia.cs
