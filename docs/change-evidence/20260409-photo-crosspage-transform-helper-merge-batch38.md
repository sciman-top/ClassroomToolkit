规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Helpers.cs
当前落点=Photo.CrossPage.cs 仍包含邻页共享变换辅助方法，主文件职责边界不够清晰
目标归宿=将邻页共享变换 helper（ApplyNeighborSharedTransform / EnsureNeighborTransform）归并到 CrossPage.Helpers partial
迁移批次=2026-04-09-maintainability-hardening-batch38
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
- 热点体量变化：PaintOverlayWindow.Photo.CrossPage.cs 从 950 降到 913（预算 2380）
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Helpers.cs

# Backfill 2026-04-03
影响模块=legacy-governance-evidence
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
