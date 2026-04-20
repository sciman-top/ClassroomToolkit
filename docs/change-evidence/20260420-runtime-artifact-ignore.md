# 2026-04-20 Runtime Artifact Ignore Rule

- 规则映射: R1/R2/R6/R8
- 风险等级: Low
- 当前落点: `.gitignore`
- 目标归宿: 将运行态自动产物 `docs/runtime-*.txt` 从工作区噪音中隔离，降低误提交风险，不改变业务行为。

## 变更依据

- 连续多轮验证后，`docs/runtime-*.txt` 持续出现在未跟踪列表中。
- 该类文件为运行态临时产物，不属于版本管理目标，持续暴露会增加误提交概率和代码审查噪音。

## 实施变更

1. `.gitignore`
   - 追加规则：`docs/runtime-*.txt`

## 验证命令与结果

1. `dotnet build ClassroomToolkit.sln -c Debug`
   - result: PASS
   - key_output: `0 warnings, 0 errors`

2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - result: PASS
   - key_output: `Passed: 3344, Failed: 0, Skipped: 0`

3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - result: PASS
   - key_output: `Passed: 28, Failed: 0, Skipped: 0`

4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
   - result: PASS
   - key_output: `[hotspot] PASS - all .cs files within line budget (max=1200)`

5. `git status --short`
   - result: PASS
   - key_output: 仅剩 `.gitignore` 变更，`docs/runtime-*.txt` 不再出现在未跟踪列表。

## Hotspot 人工复核

- 复核对象: `.gitignore`
- 结论:
  - 仅增加一条忽略规则，不影响运行时逻辑、配置语义、持久化格式和外部接口。
  - 变更可回滚且影响面极小。

## 回滚

- 回滚文件:
  - `.gitignore`
  - `docs/change-evidence/20260420-runtime-artifact-ignore.md`
- 回滚动作: 删除新增规则并重跑 `build -> test -> contract/invariant -> hotspot`。
