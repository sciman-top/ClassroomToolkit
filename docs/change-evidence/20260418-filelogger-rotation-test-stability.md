## 变更证据

- 规则 ID: `R1/R2/R6/R8`
- 风险等级: `low`
- 当前落点: `tests/ClassroomToolkit.Tests/FileLoggerProviderTests.cs`
- 目标归宿: 修正日志轮转测试的错误前提，避免在 provider 仍持有写句柄时用 `File.ReadAllText` 直接读取文件导致全量测试偶发失败

## 依据

- `FileLoggerProviderTests.Log_ShouldRotateFile_WhenDateChanges()` 在确认文件存在后立即调用 `File.ReadAllText(...)`。
- 该用例读取时 `provider` 仍处于 `using` 作用域内，后台队列线程可能尚未完成刷写。
- `File.ReadAllText` 的默认共享模式不能与活动写句柄稳定并存，导致全量测试中出现 `IOException: file is being used by another process`。
- 根因在测试前提，不在生产逻辑；本次未更改 `FileLoggerProvider` 的行为。

## 执行命令

1. `dotnet build ClassroomToolkit.sln -c Debug`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~FileLoggerProviderTests"`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
4. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`

## 关键输出

- build: `0 warnings, 0 errors`
- `FileLoggerProviderTests`: `Passed, total 13`
- 全量测试: `Passed, total 3223`
- contract/invariant: `Passed, total 27`

## 热点人工复核

- `FileLoggerProviderTests.Log_ShouldRotateFile_WhenDateChanges`
  - 复核点: 测试是否在稳定前提下验证“跨日写入落在两个文件桶”
  - 结论: 先 `Dispose()` 再读取文件，验证的是已完成刷写后的稳定状态，不再把后台队列线程的时序窗口误判成生产缺陷

## 回滚

1. 回滚文件:
   - `tests/ClassroomToolkit.Tests/FileLoggerProviderTests.cs`
   - `docs/change-evidence/20260418-filelogger-rotation-test-stability.md`
2. 回滚方式:
   - 使用版本控制回退本次改动，或逆向移除新增的 `provider.Dispose();`
3. 回滚后复测:
   - 重新执行本文件中的 4 条命令
