规则ID=R1,R2,R6,R8
影响模块=src/ClassroomToolkit.App/RollCallWindow.xaml.cs;tests/ClassroomToolkit.Tests/App/RollCallWindowSettingsReloadContractTests.cs
当前落点=RollCallWindow 依赖进程内 _settings 快照，窗口重建时可能出现旧值/默认值回放
目标归宿=每次创建点名窗口时先从 settings store 重新加载点名设置并同步到 _settings，再应用窗口参数和 ViewModel
迁移批次=20260401-rollcall-settings-reload
风险等级=低
执行命令=dotnet build ClassroomToolkit.sln -c Debug;dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug;dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests";powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=build 0 error;test 3056 passed;contract/invariant 24 passed;hotspot status=PASS
回滚动作=git checkout -- src/ClassroomToolkit.App/RollCallWindow.xaml.cs tests/ClassroomToolkit.Tests/App/RollCallWindowSettingsReloadContractTests.cs docs/change-evidence/20260401-rollcall-remember-settings-on-open.md
