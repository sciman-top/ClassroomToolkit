规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.cs
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.Seed.cs
当前落点=PaintOverlayWindow.Photo.Navigation.cs 包含交互切页预热帧方法组，热点接近预算
目标归宿=将交互切页预热帧方法组迁移到独立 partial，降低热点并维持导航行为一致
迁移批次=2026-04-08-maintainability-hardening-batch14
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
- 热点体量下降：PaintOverlayWindow.Photo.Navigation.cs 从 1297 降到 1122（预算 1340，delta 从 -43 改善到 -218）
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.cs
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.Seed.cs

# Backfill 2026-04-03
影响模块=legacy-governance-evidence
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
