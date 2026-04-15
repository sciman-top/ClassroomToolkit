规则ID=CTK-INK-CROSSPAGE-ERASE-DUPDEF-20260410
风险等级=low
执行命令=dotnet build ClassroomToolkit.sln -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=build pass; full test pass (3213); contract/invariant subset pass (25); hotspot status PASS
回滚动作=删除 PaintOverlayWindow.Ink.EraserAndRegion.cs 中新增的 cross-page region erase 三个方法并恢复 PaintOverlayWindow.Ink.CrossPageRegionErase.cs 的原实现；随后重跑 build/test/contract/hotspot

# Backfill 2026-04-03
影响模块=legacy-governance-evidence
当前落点=D:/OneDrive/CODE/ClassroomToolkit/docs/change-evidence
目标归宿=D:/OneDrive/CODE/repo-governance-hub/source/project/ClassroomToolkit/*
迁移批次=2026-04-03-evidence-backfill
