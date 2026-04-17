## 变更证据

- 规则 ID: `R1/R2/R3/R6/R8`
- 风险等级: `low`
- 当前落点: `src/ClassroomToolkit.App/Ink/*`
- 目标归宿: 关键 ink 读写失败路径具备最小诊断，但不改变现有降级行为

## 依据

- `InkPersistenceService`、`InkStorageService`、`InkWriteAheadLogService` 存在多个静默失败 catch，定位“为什么数据没写进去”成本较高。
- 本次仅补 `Debug.WriteLine`，不改变返回值、不抛出新异常、不改文件格式。

## 关联改动

- `src/ClassroomToolkit.App/Ink/InkPersistenceService.cs`
- `src/ClassroomToolkit.App/Ink/InkStorageService.cs`
- `src/ClassroomToolkit.App/Ink/InkWriteAheadLogService.cs`
- `tests/ClassroomToolkit.Tests/InkStorageDiagnosticsContractTests.cs`

## 执行命令

1. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~InkStorageDiagnosticsContractTests|FullyQualifiedName~InkPersistenceServiceTests|FullyQualifiedName~InkStorageServiceTests|FullyQualifiedName~InkWriteAheadLogServiceTests"`
2. `dotnet build ClassroomToolkit.sln -c Debug -m:1`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1`
4. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`

## 关键输出

- 定向测试: `Passed, total 29`
- build: `0 warnings, 0 errors`
- 全量测试: `Passed, total 3219`
- contract/invariant: `Passed, total 25`

## 热点人工复核

- `InkPersistenceService`
  - 复核点: 读取损坏 sidecar、删除临时文件失败
  - 结论: 仅补调试输出，不改变 null-return 降级语义
- `InkStorageService`
  - 复核点: 页面 JSON 读取失败与清理失败
  - 结论: 保持兼容，失败仍按原逻辑返回 null/忽略
- `InkWriteAheadLogService`
  - 复核点: WAL 加载/保存/临时文件清理失败
  - 结论: 仅增强诊断，不改恢复逻辑

## 回滚

1. 回滚文件:
   - `src/ClassroomToolkit.App/Ink/InkPersistenceService.cs`
   - `src/ClassroomToolkit.App/Ink/InkStorageService.cs`
   - `src/ClassroomToolkit.App/Ink/InkWriteAheadLogService.cs`
   - `tests/ClassroomToolkit.Tests/InkStorageDiagnosticsContractTests.cs`
2. 回滚方式:
   - 使用版本控制回退本次提交，或按 `git diff` 逆向恢复
3. 回滚后复测:
   - 重新执行本文件中的 4 条命令
