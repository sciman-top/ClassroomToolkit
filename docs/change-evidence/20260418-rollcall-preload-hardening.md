## 变更证据

- 规则 ID: `R1/R2/R3/R6/R8`
- 风险等级: `low`
- 当前落点: `RollCallViewModel` 预加载并发状态机
- 目标归宿: 旧预加载任务不污染当前状态，失败/取消路径可诊断，任务引用能及时收敛

## 依据

- `WarmupData()` 使用 `ContinueWith(..., TaskScheduler.Default)`，预加载完成后的状态收敛逻辑分散在匿名回调中。
- 失败结果虽然不会缓存，但之前没有针对“失败结果不缓存、任务引用及时清理”的回归测试。
- 任务失败/取消路径缺少专用诊断信息，定位成本高。

## 关联改动

- `src/ClassroomToolkit.App/ViewModels/RollCallViewModel.Data.cs`
- `src/ClassroomToolkit.App/ViewModels/RollCallDataLoadDiagnosticsPolicy.cs`
- `tests/ClassroomToolkit.Tests/RollCallViewModelPreloadConcurrencyTests.cs`

## 执行命令

1. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~RollCallViewModelPreloadConcurrencyTests|FullyQualifiedName~RollCallDataLoadDiagnosticsPolicyTests|FullyQualifiedName~RollCallViewModelPhotoPathRefreshTests"`
2. `dotnet build ClassroomToolkit.sln -c Debug -m:1`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1`
4. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`

## 关键输出

- 定向测试: `Passed, total 6`
- build: `0 warnings, 0 errors`
- 全量测试: `Passed, total 3223`（当前工作区最近一次复测）
- contract/invariant: `Passed, total 25`

## 热点人工复核

- `WarmupData`:
  - 复核点: 旧任务完成后是否会清空新任务引用
  - 结论: 仍用 `ReferenceEquals(_preloadTask, preloadTask)` 保护，未破坏既有并发语义
- `CompletePreloadTask`:
  - 复核点: faulted/canceled/completed 三条路径是否都收敛 `_preloadTask`
  - 结论: 通过 `finally` 统一清理，避免分支遗漏
- `failed preload result`:
  - 复核点: 失败结果是否被 `_preloadedResult` 缓存
  - 结论: 新增回归测试确认不会缓存错误结果

## 回滚

1. 回滚文件:
   - `src/ClassroomToolkit.App/ViewModels/RollCallViewModel.Data.cs`
   - `src/ClassroomToolkit.App/ViewModels/RollCallDataLoadDiagnosticsPolicy.cs`
   - `tests/ClassroomToolkit.Tests/RollCallViewModelPreloadConcurrencyTests.cs`
2. 回滚方式:
   - 使用版本控制回退本次提交，或按 `git diff` 逆向恢复
3. 回滚后复测:
   - 重新执行本文件中的 4 条命令
