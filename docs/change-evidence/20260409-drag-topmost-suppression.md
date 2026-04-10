规则ID=R1,R2,R4,R6,R8
影响模块=src/ClassroomToolkit.App/Windowing, src/ClassroomToolkit.App/Helpers, src/ClassroomToolkit.App, tests/ClassroomToolkit.Tests/App
当前落点=拖拽流程与Topmost互操作冲突导致窗口回弹
目标归宿=在拖拽期间统一抑制Topmost互操作，拖拽结束后恢复常规Z序修复
迁移批次=2026-04-09-batch-1
风险等级=中
执行命令=
- codex status
- codex --version
- codex --help
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~WindowTopmostExecutorTests"
- dotnet build ClassroomToolkit.sln -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
- powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=
- 新增失败先行测试：WindowTopmostExecutorTests.TryApplyHandleNoActivate_ShouldSkip_WhenDragOperationIsActive（先红后绿）
- build/test/contract/hotspot 四道门禁全部通过
- platform_na:
  - reason=codex status 在非交互终端返回 "stdin is not a terminal"
  - alternative_verification=codex --version 与 codex --help 成功
  - evidence_link=本文件“执行命令/验证证据”
  - expires_at=2026-05-09
回滚动作=
- git restore src/ClassroomToolkit.App/Helpers/WindowExtensions.cs
- git restore src/ClassroomToolkit.App/LauncherBubbleWindow.xaml.cs
- git restore src/ClassroomToolkit.App/Windowing/WindowTopmostExecutor.cs
- git restore tests/ClassroomToolkit.Tests/App/WindowTopmostExecutorTests.cs
- git restore --staged src/ClassroomToolkit.App/Windowing/WindowDragOperationState.cs
- git clean -f src/ClassroomToolkit.App/Windowing/WindowDragOperationState.cs

# Backfill 2026-04-03
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
