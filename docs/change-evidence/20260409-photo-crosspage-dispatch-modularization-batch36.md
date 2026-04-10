规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Dispatch.cs
当前落点=Photo.CrossPage.cs 中 dispatch 管线（请求去重/延迟调度/失败恢复/执行）集中，主文件控制流复杂
目标归宿=将 CrossPage dispatch 管线迁移到独立 partial，提升可读性与故障隔离能力
迁移批次=2026-04-09-maintainability-hardening-batch36
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
- 热点体量变化：PaintOverlayWindow.Photo.CrossPage.cs 从 1400 降到 1073（预算 2380）
- 过程说明：一次 contract 执行出现 CS2012（.NET Host 锁文件）后重跑通过
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Dispatch.cs

# Backfill 2026-04-03
影响模块=legacy-governance-evidence
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
