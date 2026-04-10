# 2026-04-07 放映控制自动回退增强（按目标锁 + 连续失败阈值）

- rule_id: `R1/R2/R3/R6/R8`
- risk_level: `medium`
- current_landing: `PresentationControlService 自动回退状态管理`
- target_destination: `降级锁按窗口目标生效，且连续失败达到阈值后才锁定 message`
- migration_batch: `2026-04-07-batch-2`

## 变更摘要

- 将 `PresentationControlService` 的 WPS/Office 自动降级状态由“全局布尔”改为“按目标句柄状态表”。
- 新增连续失败阈值（`2` 次）后才进入 `ForceMessage` 锁定。
- 在目标使用非 message 策略发送成功后，自动清除该目标的失败状态。
- `PresentationInputPipeline.ResolveWpsSendMode` 增加目标句柄参数，自动模式下可按目标判断是否被锁定。
- 新增测试覆盖：
  - 锁定阈值生效（第 3 次命令才直接 message）
  - 一个失败目标不会污染另一个目标

## 执行命令

1. `dotnet build ClassroomToolkit.sln -c Debug`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 验证证据

- build: 通过（0 error）
- test: 通过（3191 passed）
- contract/invariant: 通过（25 passed）
- hotspot: PASS

## 回滚动作

1. 回滚以下文件到变更前版本：
   - `src/ClassroomToolkit.Services/Presentation/PresentationControlService.cs`
   - `src/ClassroomToolkit.App/Paint/PresentationInputPipeline.cs`
   - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Presentation.cs`
   - `tests/ClassroomToolkit.Tests/PresentationControlServiceTests.cs`
2. 重跑门禁链：`build -> test -> contract/invariant -> hotspot`

# Backfill 2026-04-03
规则ID=BACKFILL-LEGACY-EVIDENCE-2026-04-03
影响模块=legacy-governance-evidence
当前落点=E:/CODE/ClassroomToolkit/docs/change-evidence
目标归宿=E:/CODE/governance-kit/source/project/ClassroomToolkit/*
迁移批次=2026-04-03-evidence-backfill
风险等级=Low(documentation backfill only)
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
