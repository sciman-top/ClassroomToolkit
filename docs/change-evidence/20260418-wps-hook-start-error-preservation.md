## 变更证据

- 规则 ID: `R1/R2/R3/R6/R8`
- 风险等级: `low`
- 当前落点:
  - `src/ClassroomToolkit.Interop/Presentation/WpsSlideshowNavigationHook.Lifecycle.cs`
  - `tests/ClassroomToolkit.Tests/InteropHookLifecycleContractTests.cs`
- 目标归宿: `WpsSlideshowNavigationHook.StartAsync()` 失败返回 `false` 后，`LastError` 仍保留真实启动失败码，不被 `Stop()` 的清理路径覆盖

## 依据

- `PresentationDiagnosticsProbe.TryCheckWpsHook()` 在 `hook.StartAsync()` 返回 `false` 后，会读取 `hook.LastError` 拼成 `Win32 Error ...` 诊断信息。
- 旧实现中 `StartAsync()` 失败后先写入 `LastError = Marshal.GetLastWin32Error()`，随后调用 `Stop()`。
- `Stop()` 会执行 `LastError = unhookFailed ? lastUnhookError : 0;`，导致启动失败码被重置为 `0` 或改写成清理阶段错误码。

## 执行命令

1. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~InteropHookLifecycleContractTests.WpsHook_StartFailure_ShouldPreserveStartLastError_AfterCleanup"`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~InteropHookLifecycleContractTests"`
3. `dotnet build ClassroomToolkit.sln -c Debug`
4. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
5. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`

## 关键输出

- 新增契约测试先失败、后通过
- `InteropHookLifecycleContractTests`: `Passed, total 9`
- build: `0 warnings, 0 errors`
- 全量测试: `Passed, total 3224`
- contract/invariant: `Passed, total 28`

## 热点人工复核

- `WpsSlideshowNavigationHook.StartAsync`
  - 复核点: 启动失败后的 `LastError` 是否还能被调用方读取
  - 结论: 先缓存 `startLastError`，完成 `Stop()` 清理后恢复给 `LastError`，不改变成功路径和 unhook 日志逻辑
- `PresentationDiagnosticsProbe.TryCheckWpsHook`
  - 复核点: 依赖 `hook.LastError` 的错误展示是否因此更准确
  - 结论: `!started && hook.LastError != 0` 分支现在能拿到真实启动失败码

## 回滚

1. 回滚文件:
   - `src/ClassroomToolkit.Interop/Presentation/WpsSlideshowNavigationHook.Lifecycle.cs`
   - `tests/ClassroomToolkit.Tests/InteropHookLifecycleContractTests.cs`
   - `docs/change-evidence/20260418-wps-hook-start-error-preservation.md`
2. 回滚方式:
   - 使用版本控制回退本次改动，或删除 `startLastError` 恢复逻辑与对应契约测试
3. 回滚后复测:
   - 重新执行本文件中的 5 条命令
