规则ID=R1,R2,R3,R4,R6,R8
影响模块=src/ClassroomToolkit.App/Paint
当前落点=PaintOverlayWindow.Photo.CrossPage.cs（邻页 ink 槽位渲染）
目标归宿=当 runtime 明确判定页面已 clear-all(empty) 时，禁止 preserved/cache/hold frame 回填并强制折叠该页邻页 ink 槽位，消除旧笔迹单帧闪现
迁移批次=2026-04-01-04
风险等级=中
执行命令=dotnet build ClassroomToolkit.sln -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=build 0 error; 全量测试 3054 通过; contract/invariant 24 通过; hotspot PASS（CrossPage.cs=2379/2380）
回滚动作=git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs docs/change-evidence/20260401-crosspage-clearall-flash-guard.md
