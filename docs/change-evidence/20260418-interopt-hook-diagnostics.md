## 变更证据

- 规则 ID: `R1/R2/R3/R6/R8`
- 风险等级: `low`
- 当前落点: `src/ClassroomToolkit.Interop/Presentation/*`
- 目标归宿: Hook 启动失败路径具备最小诊断，保持原有 LastError 与停止语义

## 依据

- `KeyboardHook.StartCore()` 和 `WpsSlideshowNavigationHook.StartAsync()` 失败时仅返回或设置 `LastError`，缺少统一失败日志。
- 这类失败通常和系统权限、模块句柄、窗口状态相关，调试成本高。

## 关联改动

- `src/ClassroomToolkit.Interop/Presentation/KeyboardHook.Lifecycle.cs`
- `src/ClassroomToolkit.Interop/Presentation/WpsSlideshowNavigationHook.Lifecycle.cs`
- `tests/ClassroomToolkit.Tests/InteropHookLifecycleContractTests.cs`

## 执行命令

1. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~PresentationDiagnosticsProbeBlockingSafetyContractTests"`
2. `dotnet build ClassroomToolkit.sln -c Debug -m:1`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1`
4. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`

## 关键输出

- 定向测试: `Passed, total 16`
- build: `0 warnings, 0 errors`
- 全量测试: `Passed, total 3223`（当前工作区最近一次复测）
- contract/invariant: `Passed, total 27`

## 热点人工复核

- `KeyboardHook.StartCore`
  - 复核点: 失败后是否仍保持现有 `LastError` 语义
  - 结论: 仅新增 `Debug.WriteLine`，未改控制流
- `WpsSlideshowNavigationHook.StartAsync`
  - 复核点: 失败后是否仍停止并返回 `false`
  - 结论: 保持原语义，只补诊断

## 回滚

1. 回滚文件:
   - `src/ClassroomToolkit.Interop/Presentation/KeyboardHook.Lifecycle.cs`
   - `src/ClassroomToolkit.Interop/Presentation/WpsSlideshowNavigationHook.Lifecycle.cs`
   - `tests/ClassroomToolkit.Tests/InteropHookLifecycleContractTests.cs`
2. 回滚方式:
   - 使用版本控制回退本次提交，或按 `git diff` 逆向恢复
3. 回滚后复测:
   - 重新执行本文件中的 4 条命令
