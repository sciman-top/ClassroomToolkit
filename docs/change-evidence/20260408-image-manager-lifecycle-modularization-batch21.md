规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs
- src/ClassroomToolkit.App/Photos/ImageManagerWindow.Lifecycle.cs
- tests/ClassroomToolkit.Tests/ImageManagerCloseCallbackSafetyContractTests.cs
当前落点=ImageManagerWindow.xaml.cs 内含生命周期与布局设置逻辑，且契约测试绑定单文件源码
目标归宿=将生命周期/布局设置方法迁移至独立 partial，并将契约测试改为 ImageManagerWindow*.cs 聚合断言，保证行为契约同时支持模块化
迁移批次=2026-04-08-maintainability-hardening-batch21
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
- 热点体量下降：ImageManagerWindow.xaml.cs 从 1157 降到 1022（预算 1540，delta 从 -383 改善到 -518）
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs
- git checkout -- src/ClassroomToolkit.App/Photos/ImageManagerWindow.Lifecycle.cs
- git checkout -- tests/ClassroomToolkit.Tests/ImageManagerCloseCallbackSafetyContractTests.cs

# Backfill 2026-04-03
影响模块=legacy-governance-evidence
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
