# 2026-04-22 startup suppression persist guard

## Scope
- issue_id: `startup-suppression-persist-guard`
- risk_level: `low`
- boundary:
  - 仅在启动兼容性告警的“抑制当前问题并保存设置”分支增加非致命异常隔离。
  - 不改变外部接口、配置键语义、数据格式和持久化结构。

## Basis
- 代码事实：`StartupOrchestrator.RunCompatibilityGate` 中，`dialog.SuppressCurrentIssues` 分支直接调用 `AppSettingsService.Save(settings)`，缺少异常隔离。
- 风险：设置持久化失败可能中断告警收口流程，不符合课堂可用性优先约束。

## Changes
1. `src/ClassroomToolkit.App/Startup/StartupOrchestrator.cs`
   - 为 suppression persist 分支增加 `try/catch`，并使用 `AppGlobalExceptionHandlingPolicy.IsNonFatal` 过滤。
   - 失败时落日志：`StartupCompatibilitySuppressionPersist`。
2. `tests/ClassroomToolkit.Tests/App/StartupCompatibilitySuppressionPersistSafetyContractTests.cs`
   - 新增源码契约测试，锁定上述异常隔离与日志留痕。

## Commands and Evidence
- `dotnet build ClassroomToolkit.sln -c Debug`
  - exit_code: `0`
  - key_output: `0 errors, 0 warnings`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - exit_code: `0`
  - key_output: `Passed: 3403`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code: `0`
  - key_output: `Passed: 28`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
  - exit_code: `0`
  - key_output: `[hotspot] PASS - all .cs files within line budget (max=1200)`

## Rollback
- `git checkout -- src/ClassroomToolkit.App/Startup/StartupOrchestrator.cs tests/ClassroomToolkit.Tests/App/StartupCompatibilitySuppressionPersistSafetyContractTests.cs`
