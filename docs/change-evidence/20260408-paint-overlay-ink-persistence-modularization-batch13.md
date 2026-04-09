规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Persistence.cs
当前落点=PaintOverlayWindow.Ink.cs 底部包含脏页标记/WAL/哈希方法组，接近热点预算上限
目标归宿=将持久化与脏页状态维护方法迁移到独立 partial，降低热点风险并保持行为一致
迁移批次=2026-04-08-maintainability-hardening-batch13
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
- 热点体量下降：PaintOverlayWindow.Ink.cs 从 1419 降到 1258（预算 1420，delta 从 -1 改善到 -162）
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs
- git checkout -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Persistence.cs
