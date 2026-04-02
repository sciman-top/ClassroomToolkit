规则ID=R1,R2,R4,R6,R8
影响模块=PresentationClassifierOverridesPackagePolicy, package policy tests
当前落点=src/ClassroomToolkit.Services/Presentation/PresentationClassifierOverridesPackagePolicy.cs
目标归宿=演示识别覆盖规则支持导入/导出签名包（完整性校验 + 规范化）
迁移批次=P1-2/2
风险等级=中
执行命令=dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PresentationClassifierOverridesPackagePolicyTests|FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~PresentationDiagnosticsProbeVersionTests|FullyQualifiedName~StartupCompatibilityProbeTests"; dotnet build ClassroomToolkit.sln -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=targeted 15/15 pass; build 0 error; full test 3076/3076 pass; contract 24/24 pass; hotspot PASS
回滚动作=git restore --source=HEAD -- src/ClassroomToolkit.Services/Presentation/PresentationClassifierOverridesPackagePolicy.cs tests/ClassroomToolkit.Tests/PresentationClassifierOverridesPackagePolicyTests.cs docs/change-evidence/20260402-presentation-overrides-package-policy.md
