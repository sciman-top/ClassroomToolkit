## 变更证据

- 规则 ID: `R1/R2/R3/R6/R8`
- 风险等级: `low`
- 当前落点:
  - `src/ClassroomToolkit.App/MainWindow.Lifecycle.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Lifecycle.cs`
  - `tests/ClassroomToolkit.Tests/MainWindowExitLifecycleContractTests.cs`
  - `tests/ClassroomToolkit.Tests/PaintOverlayWindowLifecycleContractTests.cs`
- 目标归宿: 关闭路径的 `DispatcherTimer` 和后台任务在关机/关闭中只做早退，不再继续触发 UI 工作或重复收尾

## 依据

- `MainWindow` 的自动退出、前台抑制、topmost watchdog 三个 timer 在关闭中仍可能触发，且原实现没有统一的关机态早退。
- `PaintOverlayWindow` 的 `presentation focus monitor`、`ink monitor`、`ink sidecar autosave` 也需要在关闭中静默退出，避免 close path 上继续调度 UI 工作。
- 本次只添加早退守卫，不改变关闭顺序、事件解绑顺序或资源释放顺序。

## 关联改动

- `src/ClassroomToolkit.App/MainWindow.Lifecycle.cs`
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Lifecycle.cs`
- `tests/ClassroomToolkit.Tests/MainWindowExitLifecycleContractTests.cs`
- `tests/ClassroomToolkit.Tests/PaintOverlayWindowLifecycleContractTests.cs`

## 执行命令

1. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~MainWindowExitLifecycleContractTests|FullyQualifiedName~PaintOverlayWindowLifecycleContractTests"`
2. `dotnet build ClassroomToolkit.sln -c Debug -m:1`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1`
4. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests|FullyQualifiedName~MainWindowExitLifecycleContractTests|FullyQualifiedName~PaintOverlayWindowLifecycleContractTests"`

## 关键输出

- 定向测试: `Passed, total 3`
- build: `0 warnings, 0 errors`
- 全量测试: `Passed, total 3223`
- contract/invariant: `Passed, total 30`

## 热点人工复核

- `MainWindow.OnAutoExitTimerTick / OnPresentationForegroundSuppressionTimerTick / OnFloatingTopmostWatchdogTick`
  - 复核点: 关闭态下是否仍可能继续执行窗口恢复、Z-order policy 或退出逻辑
  - 结论: 通过 `_backgroundTasksCancellationDisposed || _backgroundTasksCancellation.IsCancellationRequested` 统一早退，关闭后不再继续调度
- `PaintOverlayWindow.OnPresentationFocusMonitorTick / OnInkMonitorTick / OnInkSidecarAutoSaveTimerTick`
  - 复核点: overlay 关闭中是否仍可能继续刷新、轮询或自动保存
  - 结论: 通过 `_overlayClosed` / `_overlayLifecycleCancellation` 早退，避免关闭后继续访问 UI 或重入自动保存

## 回滚

1. 回滚文件:
   - `src/ClassroomToolkit.App/MainWindow.Lifecycle.cs`
   - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Lifecycle.cs`
   - `tests/ClassroomToolkit.Tests/MainWindowExitLifecycleContractTests.cs`
   - `tests/ClassroomToolkit.Tests/PaintOverlayWindowLifecycleContractTests.cs`
2. 回滚方式:
   - 使用版本控制回退本次提交，或按 `git diff` 逆向恢复上述文件
3. 回滚后复测:
   - 重新执行本文件中的 4 条命令
