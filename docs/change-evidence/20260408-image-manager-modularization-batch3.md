规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs
- src/ClassroomToolkit.App/Photos/ImageManagerWindow.Layout.cs
- src/ClassroomToolkit.App/Photos/ImageManagerWindow.Converters.cs
- tests/ClassroomToolkit.Tests/ImageManagerWindowDispatchFallbackContractTests.cs
- tests/ClassroomToolkit.Tests/ImageManagerEventCallbackSafetyContractTests.cs
当前落点=App/Photos ImageManagerWindow 主实现
目标归宿=将窗口布局/标题栏交互与 converter 从主文件拆分为独立 partial/独立文件，降低复杂度并保留行为；同步放宽源码结构契约为跨 partial 聚合检查
迁移批次=2026-04-08-maintainability-hardening-batch3
风险等级=低
执行命令=
- dotnet build ClassroomToolkit.sln -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ImageManagerWindowDispatchFallbackContractTests|FullyQualifiedName~ImageManagerEventCallbackSafetyContractTests"
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
- powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=
- build 通过：0 errors / 0 warnings
- 定向契约测试通过：2 passed / 0 failed
- 全量测试通过：3199 passed / 0 failed
- contract/invariant 通过：25 passed / 0 failed
- hotspot 通过：status=PASS
- 热点体量下降：ImageManagerWindow.xaml.cs 从 1540 预算基线进一步下降到 1157（delta -383）
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs
- git checkout -- src/ClassroomToolkit.App/Photos/ImageManagerWindow.Layout.cs
- git checkout -- src/ClassroomToolkit.App/Photos/ImageManagerWindow.Converters.cs
- git checkout -- tests/ClassroomToolkit.Tests/ImageManagerWindowDispatchFallbackContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/ImageManagerEventCallbackSafetyContractTests.cs

# Backfill 2026-04-03
影响模块=legacy-governance-evidence
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
