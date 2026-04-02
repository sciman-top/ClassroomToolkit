规则ID=R1,R2,R4,R6,R8
影响模块=PresentationDiagnosticsProbe, diagnostics tests
当前落点=src/ClassroomToolkit.Services/Presentation/PresentationDiagnosticsProbe.cs
目标归宿=系统兼容性诊断输出演示进程文件/产品版本，提升版本排障效率
迁移批次=P1-1/2
风险等级=低
执行命令=dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PresentationDiagnosticsProbeVersionTests|FullyQualifiedName~PresentationDiagnosticsProbeBlockingSafetyContractTests|FullyQualifiedName~StartupCompatibilityProbeTests"; dotnet build ClassroomToolkit.sln -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=targeted 7/7 pass; build 0 error; full test 3072/3072 pass; contract 24/24 pass; hotspot PASS
回滚动作=git restore --source=HEAD -- src/ClassroomToolkit.Services/Presentation/PresentationDiagnosticsProbe.cs tests/ClassroomToolkit.Tests/PresentationDiagnosticsProbeVersionTests.cs docs/change-evidence/20260402-presentation-diagnostics-process-version.md
