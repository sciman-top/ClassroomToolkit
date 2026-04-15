# 2026-04-07 放映控制自动回退增强（二次）：锁定后自动探活恢复

- rule_id: `R1/R2/R3/R6/R8`
- risk_level: `medium`
- current_landing: `PresentationControlService 锁定后恢复机制`
- target_destination: `ForceMessage 锁定态下按命令窗口自动探活 raw，成功后自动解锁`
- migration_batch: `2026-04-07-batch-3`

## 变更摘要

- 在按目标句柄锁定的基础上新增探活窗口：
  - 连续失败阈值仍为 `2` 次。
  - 锁定后累计 `8` 次 message 成功后，自动放行 1 次 raw 探活。
  - 探活成功即移除该目标锁定状态，恢复 raw 主路径。
- 新增回归测试覆盖“锁定 -> 累计 message -> 自动探活 -> 自动恢复”完整链路。

## 执行命令

1. `dotnet build ClassroomToolkit.sln -c Debug`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 验证证据

- build: 通过（0 error，存在 DLL 占用重试 warning）
- test: 通过（3192 passed）
- contract/invariant: 通过（25 passed）
- hotspot: PASS

## 回滚动作

1. 回滚以下文件到变更前版本：
   - `src/ClassroomToolkit.Services/Presentation/PresentationControlService.cs`
   - `tests/ClassroomToolkit.Tests/PresentationControlServiceTests.cs`
2. 重跑门禁链：`build -> test -> contract/invariant -> hotspot`

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
