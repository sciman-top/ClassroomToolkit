规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Preview.cs
当前落点=PaintOverlayWindow.Ink.cs 集中承载笔迹预测预览逻辑，文件复杂度偏高
目标归宿=将笔迹预测/预览渲染方法组迁移到独立 partial，降低热点与维护成本，保持行为一致
迁移批次=2026-04-08-maintainability-hardening-batch19
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
- 热点体量下降：PaintOverlayWindow.Ink.cs 从 1258 降到 1103（预算 1420，delta 从 -162 改善到 -317）
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Preview.cs
