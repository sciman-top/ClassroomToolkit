规则ID=R2,R5,R6,R8
影响模块=
- tests/ClassroomToolkit.Tests/ImageManagerCloseCallbackSafetyContractTests.cs
- tests/ClassroomToolkit.Tests/ImageManagerDispatcherShutdownGuardContractTests.cs
- tests/ClassroomToolkit.Tests/ImageManagerLoadImagesPostAwaitGuardContractTests.cs
- tests/ClassroomToolkit.Tests/ImageManagerThumbnailDispatchFallbackContractTests.cs
- tests/ClassroomToolkit.Tests/ImageManagerWindowFolderExpandLifecycleContractTests.cs
当前落点=多处 ImageManager 契约测试绑定单文件 ImageManagerWindow.xaml.cs，重构时易触发结构性误报
目标归宿=统一改为读取 ImageManagerWindow*.cs 聚合源码，保持行为契约并允许 partial 模块化演进
迁移批次=2026-04-08-test-hardening-batch22
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
回滚动作=
- git checkout -- tests/ClassroomToolkit.Tests/ImageManagerCloseCallbackSafetyContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/ImageManagerDispatcherShutdownGuardContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/ImageManagerLoadImagesPostAwaitGuardContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/ImageManagerThumbnailDispatchFallbackContractTests.cs
- git checkout -- tests/ClassroomToolkit.Tests/ImageManagerWindowFolderExpandLifecycleContractTests.cs

# Backfill 2026-04-03
影响模块=legacy-governance-evidence
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
