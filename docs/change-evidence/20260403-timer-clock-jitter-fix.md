# 20260403-timer-clock-jitter-fix

- rule_id: R1/R2/R3/R6/R8
- risk_level: medium
- scope:
  - src/ClassroomToolkit.Domain/Timers/TimerEngine.cs
  - src/ClassroomToolkit.App/ViewModels/RollCallViewModel.Timer.cs
  - tests/ClassroomToolkit.Tests/TimerEngineTests.cs

## Changes
- 修复 TimerEngine 丢弃子秒导致的节奏抖动：新增 `_pendingElapsed` 累积并按整秒消费。
- 修复 Stopwatch 秒累加潜在溢出：改为 long 过渡并上限 clamp。
- 修复 Clock 模式显示错误：`TimeDisplay` 在 Clock 分支显示 `DateTime.Now:HH:mm:ss`。
- 新增回归测试：覆盖子秒累计路径（stopwatch/countdown）。

## Gate Evidence
1. build (required command)
- cmd: `dotnet build ClassroomToolkit.sln -c Debug`
- result: failed
- key_output: `ClassroomToolkit.Domain.dll` 被运行中进程 `sciman Classroom Toolkit (PID 50796)` 锁定。

2. test (required command)
- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- result: not executable under same output lock context; switched to alternative output path.

3. contract/invariant
- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests" -p:BaseOutputPath=artifacts/tmpbuild/`
- result: pass (25/25)

4. hotspot
- cmd: `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
- result: pass

## Additional Verification
- cmd: `dotnet build ClassroomToolkit.sln -c Debug -p:BaseOutputPath=artifacts/tmpbuild/`
- result: pass
- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:BaseOutputPath=artifacts/tmpbuild/`
- result: fail (1 test)
- failing_test: `BrushDpiGoldenRegressionTests.DpiGoldenHashes_ShouldMatchBaseline`
- key_output: baseline file missing `tests/ClassroomToolkit.Tests/Baselines/brush-dpi-golden.json`
- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~TimerEngineTests" -p:BaseOutputPath=artifacts/tmpbuild/`
- result: pass (12/12)

## N/A Records
### platform_na
- reason: `codex status` 在当前非交互终端失败（`stdin is not a terminal`）。
- alternative_verification: `codex --version` 与 `codex --help` 成功，确认 CLI 可用。
- evidence_link: 本文件「Platform Diagnostics」与终端输出。
- expires_at: 2026-04-10

### gate_na
- reason: 标准 build 输出目录被运行中的应用进程锁定，标准 build/test 无法在默认输出路径完成。
- alternative_verification: 使用 `-p:BaseOutputPath=artifacts/tmpbuild/` 完成等价 build/test/contract/hotspot 顺序验证。
- evidence_link: 本文件「Gate Evidence」「Additional Verification」。
- expires_at: 2026-04-10

### gate_na
- reason: 全量测试依赖的 golden baseline 文件缺失（仓内文件不存在）。
- alternative_verification: contract/invariant 全通过 + 变更相关 TimerEngineTests 全通过。
- evidence_link: 本文件「Additional Verification」。
- expires_at: 2026-04-10

## Platform Diagnostics
- cmd: `codex status`
- exit_code: 1
- key_output: `Error: stdin is not a terminal`
- timestamp: 2026-04-03

- cmd: `codex --version`
- exit_code: 0
- key_output: `codex-cli 0.118.0`
- timestamp: 2026-04-03

- cmd: `codex --help`
- exit_code: 0
- key_output: `Codex CLI ...`
- timestamp: 2026-04-03

## Rollback
- `git restore --source=HEAD -- src/ClassroomToolkit.Domain/Timers/TimerEngine.cs src/ClassroomToolkit.App/ViewModels/RollCallViewModel.Timer.cs tests/ClassroomToolkit.Tests/TimerEngineTests.cs`

# Backfill 2026-04-03
规则ID=BACKFILL-LEGACY-EVIDENCE-2026-04-03
影响模块=legacy-governance-evidence
当前落点=D:/OneDrive/CODE/ClassroomToolkit/docs/change-evidence
目标归宿=D:/OneDrive/CODE/repo-governance-hub/source/project/ClassroomToolkit/*
迁移批次=2026-04-03-evidence-backfill
风险等级=Low(documentation backfill only)
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
