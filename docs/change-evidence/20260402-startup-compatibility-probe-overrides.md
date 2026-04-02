规则ID=R1,R2,R4,R6,R8
影响模块=StartupCompatibilityProbe, App startup compatibility wiring, tests
当前落点=src/ClassroomToolkit.Services/Compatibility/StartupCompatibilityProbe.cs
目标归宿=启动兼容探针支持可配置演示进程识别并可单测验证
迁移批次=P0-1/2
风险等级=中
执行命令=dotnet build ClassroomToolkit.sln -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=build 0 error; test 3070/3070 pass; contract 24/24 pass; hotspot PASS
回滚动作=git restore --source=HEAD -- src/ClassroomToolkit.Services/Compatibility/StartupCompatibilityProbe.cs src/ClassroomToolkit.App/App.xaml.cs tests/ClassroomToolkit.Tests/StartupCompatibilityProbeTests.cs docs/change-evidence/20260402-startup-compatibility-probe-overrides.md
