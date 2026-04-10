规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs
- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.PresetScheme.cs
当前落点=PaintSettingsDialog.xaml.cs 承载预设方案管理逻辑（切换/降级为自定义/托管参数映射）导致主文件复杂度偏高
目标归宿=将 Preset 方案管理方法组迁移到独立 partial，降低耦合并保持行为不变
迁移批次=2026-04-08-maintainability-hardening-batch20
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
- 热点体量下降：PaintSettingsDialog.xaml.cs 从 1313 降到 1062（预算 1880，delta 从 -567 改善到 -818）
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs
- git checkout -- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.PresetScheme.cs

# Backfill 2026-04-03
影响模块=legacy-governance-evidence
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
