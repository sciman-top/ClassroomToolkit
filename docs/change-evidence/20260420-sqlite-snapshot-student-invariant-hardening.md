# 2026-04-20 SQLite 快照回放学生字段不变量加固

## 规则与目标
- 规则 ID: `R1/R2/R6/R7/R8`
- 风险等级: `low`
- 当前落点: `src/ClassroomToolkit.Infra/Storage/*SqliteStoreAdapter.cs`
- 目标归宿: 快照回放时确保 `StudentRecord` 关键字段不变量（类名/行标识）成立

## 问题 / 根因 / 方案
- 问题: 快照反序列化为 `StudentRecord` 时，仅对 `null` 值做回退；空字符串 `""` 或空白 `" "` 会直接穿透。
- 根因: 回退条件使用 `??`，未覆盖空白字符串。
- 方案:
  - `student.className` 为空白时回退到 `classSnapshot.className`。
  - `student.rowId` 为空白时生成新的 `Guid("N")`。
  - 在 `RollCallSqliteStoreAdapter` 与 `StudentWorkbookSqliteStoreAdapter` 做同构修复。
  - 新增两组回归测试，构造“桥接层失败 + 恶化快照”场景验证恢复行为。

## 变更文件
- `src/ClassroomToolkit.Infra/Storage/RollCallSqliteStoreAdapter.cs`
- `src/ClassroomToolkit.Infra/Storage/StudentWorkbookSqliteStoreAdapter.cs`
- `tests/ClassroomToolkit.Tests/RollCallSqliteStoreAdapterTests.cs`
- `tests/ClassroomToolkit.Tests/StudentWorkbookSqliteStoreAdapterTests.cs`

## 执行命令与关键证据
1. `dotnet build ClassroomToolkit.sln -c Debug`  
   - 结果: PASS（0 warning, 0 error）
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RollCallSqliteStoreAdapterTests|FullyQualifiedName~StudentWorkbookSqliteStoreAdapterTests|FullyQualifiedName~ClassRosterTests"`  
   - 结果: PASS（38 passed）
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`  
   - 结果: PASS（3365 passed）
4. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`  
   - 结果: PASS（28 passed）
5. `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`  
   - 结果: PASS

## N/A 记录
- `platform_na`: 否
- `gate_na`: 否

## 回滚方式
- 文件级回滚:
  - `git restore --source=HEAD -- src/ClassroomToolkit.Infra/Storage/RollCallSqliteStoreAdapter.cs src/ClassroomToolkit.Infra/Storage/StudentWorkbookSqliteStoreAdapter.cs tests/ClassroomToolkit.Tests/RollCallSqliteStoreAdapterTests.cs tests/ClassroomToolkit.Tests/StudentWorkbookSqliteStoreAdapterTests.cs`
- 回滚后验证: 按上文 1~5 顺序重跑。
