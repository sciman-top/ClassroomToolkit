规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs
- src/ClassroomToolkit.App/Photos/ImageManagerWindow.Tree.cs
当前落点=ImageManagerWindow.xaml.cs 仍承载目录树展开与批量追加逻辑，维护复杂度偏高
目标归宿=将目录树与批量追加方法组迁移到独立 partial，降低主文件复杂度并保持行为一致
迁移批次=2026-04-09-maintainability-hardening-batch24
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
- 热点体量下降：ImageManagerWindow.xaml.cs 从 1022 降到 863（预算 1540，delta 从 -518 改善到 -677）
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs
- git checkout -- src/ClassroomToolkit.App/Photos/ImageManagerWindow.Tree.cs
