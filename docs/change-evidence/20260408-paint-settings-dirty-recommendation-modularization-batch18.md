规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs
- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.DirtyAndRecommendation.cs
当前落点=PaintSettingsDialog.xaml.cs 内聚了脏状态判定与预设推荐显示逻辑，主文件复杂度偏高
目标归宿=将“脏状态判定 + 预设推荐”方法组迁移至独立 partial，提升可维护性并保持行为等价
迁移批次=2026-04-08-maintainability-hardening-batch18
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
- 热点体量下降：PaintSettingsDialog.xaml.cs 从 1428 降到 1313（预算 1880，delta 从 -452 改善到 -567）
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs
- git checkout -- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.DirtyAndRecommendation.cs

# Backfill 2026-04-03
影响模块=legacy-governance-evidence
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
