规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs
- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.LayoutAndLabels.cs
当前落点=PaintSettingsDialog.xaml.cs 集中承载布局切换与标签刷新逻辑，文件体量持续增大
目标归宿=将“布局与标签更新”方法组迁移到独立 partial，降低主文件复杂度并保持行为不变
迁移批次=2026-04-08-maintainability-hardening-batch17
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
- 热点体量下降：PaintSettingsDialog.xaml.cs 从 1558 降到 1428（预算 1880，delta 从 -322 改善到 -452）
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs
- git checkout -- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.LayoutAndLabels.cs
