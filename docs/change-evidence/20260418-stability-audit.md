## 变更证据

- 规则 ID: `R1/R2/R3/R6/R8`
- 风险等级: `low`
- 当前落点: `src/ClassroomToolkit.App/Photos/StudentPhotoResolver.cs`
- 目标归宿: `Dispose` 竞态下不再回填缓存，保持已释放对象状态一致
- 关联改动:
  - `src/ClassroomToolkit.App/Photos/StudentPhotoResolver.cs`
  - `src/ClassroomToolkit.Infra/Logging/FileLoggerProvider.cs`
  - `tests/ClassroomToolkit.Tests/StudentPhotoResolverTests.cs`

## 依据

- `StudentPhotoResolver.GetIndex` 在进入锁前只检查一次 `_disposed`，后台预热线程可在 `Dispose()` 清空缓存后继续写回 `_cache`，形成释放后状态回填。
- `FileLoggerProvider` 在队列刷写、追加文件、关闭等待等路径吞掉可恢复异常，问题定位成本高。
- `FileLoggerProvider` 轮转读取相关测试结论已由 `docs/change-evidence/20260418-filelogger-rotation-test-stability.md` 覆盖；当前文档仅保留诊断可观测性相关证据。

## 平台诊断

- `cmd`: `codex --version`
  - `exit_code`: `0`
  - `key_output`: `codex-cli 0.121.0`
- `cmd`: `codex --help`
  - `exit_code`: `0`
  - `key_output`: `Codex CLI ... Commands: exec/review/...`
- `cmd`: `codex status`
  - `exit_code`: `1`
  - `key_output`: `Error: stdin is not a terminal`
  - `classification`: `platform_na`
  - `reason`: 非交互 shell 下 `codex status` 不可用
  - `alternative_verification`: 使用 `codex --version`、`codex --help` 与当前项目 `AGENTS.md`
  - `evidence_link`: `docs/change-evidence/20260418-stability-audit.md`
  - `expires_at`: `2026-04-18`

## 执行命令

1. `dotnet build ClassroomToolkit.sln -c Debug -m:1`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~StudentPhotoResolverTests|FullyQualifiedName~FileLoggerProviderTests|FullyQualifiedName~FileLoggerProviderShutdownSafetyContractTests"`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1`
4. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`

## 关键输出

- `build`: `0 warnings, 0 errors`
- 定向测试: `Passed, total 24`
- 全量测试: `Passed, total 3223`（当前工作区最近一次复测）
- contract/invariant: `Passed, total 25`

## 热点人工复核

- `StudentPhotoResolver`
  - 复核点: `Dispose()` 与 `WarmupCache/GetIndex()` 并发时的状态一致性
  - 结论: 增加锁内二次释放检查后，后台任务恢复执行也不会重新写入 `_cache`
- `FileLoggerProvider`
  - 复核点: 队列消费、批量落盘、关闭等待的异常可观测性
  - 结论: 保持原有容错语义，仅补 `Debug.WriteLine` 诊断，不改变外部行为

## 回滚

1. 回滚文件:
   - `src/ClassroomToolkit.App/Photos/StudentPhotoResolver.cs`
   - `src/ClassroomToolkit.Infra/Logging/FileLoggerProvider.cs`
   - `tests/ClassroomToolkit.Tests/StudentPhotoResolverTests.cs`
2. 回滚方式:
   - 使用版本控制回退本次提交，或按 `git diff` 逆向恢复上述文件
3. 回滚后复测:
   - 重新执行本文件中的 4 条命令
