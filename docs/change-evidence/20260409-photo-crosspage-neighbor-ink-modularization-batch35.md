规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.NeighborInk.cs
当前落点=Photo.CrossPage.cs 中邻页墨迹缓存/延迟渲染/后输入刷新方法组体量大且与主渲染强耦合
目标归宿=将 NeighborInk 缓存与调度方法组迁移到独立 partial，降低主文件复杂度并保持行为一致
迁移批次=2026-04-09-maintainability-hardening-batch35
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
- 热点体量变化：PaintOverlayWindow.Photo.CrossPage.cs 从 1916 降到 1400（预算 2380）
- 过程说明：出现一次临时文件锁（CS2012，VBCSCompiler 占用），随后重跑全量测试通过
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.NeighborInk.cs

# Backfill 2026-04-03
影响模块=legacy-governance-evidence
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
