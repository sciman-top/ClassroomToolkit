# 2026-04-20 ClassRoster「全部」保留组语义修复

## 规则与目标
- 规则 ID: `R1/R2/R6/R7/R8`
- 风险等级: `low`
- 当前落点: `src/ClassroomToolkit.Domain/Models/ClassRoster.cs`
- 目标归宿: `GroupIndexMap["全部"]` 始终表示全量学生索引，不受同名普通分组输入影响

## 问题 / 根因 / 方案
- 问题: 当学生分组值包含 `全部` 时，`GroupIndexMap["全部"]` 可能仅包含该组子集，违反“全部=全量”语义。
- 根因: `BuildGroupIndexMap` 仅在缺失 `全部` 键时才回填全量索引。
- 方案: 无条件覆盖 `map[IdentityUtils.AllGroupName]` 为 `Enumerable.Range(0, students.Count)`。
- 兼容性: 不改变外部接口、数据格式、配置语义与持久化结构；仅修正保留组聚合行为。

## 变更文件
- `src/ClassroomToolkit.Domain/Models/ClassRoster.cs`
- `tests/ClassroomToolkit.Tests/ClassRosterTests.cs`

## 执行命令与关键证据
1. `dotnet build ClassroomToolkit.sln -c Debug`  
   - 结果: PASS（0 warning, 0 error）
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`  
   - 结果: PASS（3363 passed）
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`  
   - 结果: PASS（28 passed）
4. `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`  
   - 结果: PASS

## 回滚方式
- 文件级回滚: `git restore --source=HEAD -- src/ClassroomToolkit.Domain/Models/ClassRoster.cs tests/ClassroomToolkit.Tests/ClassRosterTests.cs`
- 回滚后验证: 按上文 1~4 命令重跑门禁链。
