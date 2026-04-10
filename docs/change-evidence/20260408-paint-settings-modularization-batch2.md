规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs
- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.Selection.cs
当前落点=App/Paint settings dialog implementation
目标归宿=在不改变 UI 行为与设置契约的前提下，按 partial class 拆分选择/解析工具方法，降低主文件复杂度
迁移批次=2026-04-08-maintainability-hardening-batch2
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
- 热点体量下降：PaintSettingsDialog.xaml.cs 从 1880 预算内继续下降到 1558 行（delta -322）
- 结构变化：新增 PaintSettingsDialog.Selection.cs 承载组合框选择/解析工具方法，主文件保留事件编排与业务流程
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs
- git checkout -- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.Selection.cs

# Backfill 2026-04-03
影响模块=legacy-governance-evidence
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
