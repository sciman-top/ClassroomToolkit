规则ID=R1,R2,R6,R8
影响模块=src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs;tests/ClassroomToolkit.Tests/PhotoOverlayShowOrderContractTests.cs
当前落点=PhotoOverlayWindow.ShowPhoto 在非同图切换时先显示窗口，后清空旧图
目标归宿=非同图切换先清空旧图并显示遮挡层，再显示窗口/应用新图，避免旧帧闪现
迁移批次=20260401-photo-overlay-switch-order
风险等级=低
执行命令=dotnet build ClassroomToolkit.sln -c Debug;dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug;dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests";powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=build 0 error;test 3054 passed;contract/invariant 24 passed;hotspot status=PASS
回滚动作=git checkout -- src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs tests/ClassroomToolkit.Tests/PhotoOverlayShowOrderContractTests.cs docs/change-evidence/20260401-photo-overlay-old-frame-flash.md
