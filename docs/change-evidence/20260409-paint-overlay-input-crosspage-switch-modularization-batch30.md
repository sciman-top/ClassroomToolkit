规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.cs
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.CrossPageSwitch.cs
当前落点=Input.cs 内跨页切页判定、邻页命中解析、切页导航与输入门控逻辑耦合在主文件
目标归宿=跨页切换相关方法组迁移到 Input.CrossPageSwitch partial，主文件聚焦剩余输入主干
迁移批次=2026-04-09-maintainability-hardening-batch30
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
- 热点体量变化：PaintOverlayWindow.Input.cs 从 754 降到 450（预算 1650）
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.cs
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.CrossPageSwitch.cs
