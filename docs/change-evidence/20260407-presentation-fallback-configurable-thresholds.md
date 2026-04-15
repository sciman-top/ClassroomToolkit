# 2026-04-07 放映回退阈值可配置（失败阈值/探活窗口）

- rule_id: `R1/R2/R6/R8`
- risk_level: `medium`
- current_landing: `演示放映回退策略参数固定（2/8）`
- target_destination: `将失败阈值与探活窗口配置化，并接入设置页高级参数`
- migration_batch: `2026-04-07-batch-4`

## 变更摘要

- 新增可配置参数：
  - `PresentationAutoFallbackFailureThreshold`（连续 raw 失败阈值，默认 2）
  - `PresentationAutoFallbackProbeIntervalCommands`（锁定后 message 探活窗口，默认 8）
- 参数贯通链路：
  - `AppSettings` / `AppSettingsService`（读写与归一化）
  - `PaintSettingsDialog`（高级参数下拉）
  - `MainWindow -> PaintWindowOrchestrator -> PaintOverlayWindow -> PresentationControlOptions`
  - `PresentationControlService`（按 options 生效，不再写死 2/8）
- 新增测试覆盖：
  - `AppSettingsServiceTests` 持久化与归一化断言
  - `PresentationControlServiceTests` 自定义阈值行为断言

## 执行命令

1. `dotnet build ClassroomToolkit.sln -c Debug`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 验证证据

- build: 通过（0 error）
- test: 通过（3193 passed）
- contract/invariant: 通过（25 passed）
- hotspot: PASS

## 说明

- 构建/测试期间出现 DLL 占用导致的重试 warning（VS/运行中应用占用），但最终命令均通过。

## 回滚动作

1. 回滚以下文件到变更前版本：
   - `src/ClassroomToolkit.App/Settings/AppSettings.cs`
   - `src/ClassroomToolkit.App/Settings/AppSettingsService.cs`
   - `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml`
   - `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs`
   - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Presentation.cs`
   - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs`
   - `src/ClassroomToolkit.App/Paint/PresentationInputPipeline.cs`
   - `src/ClassroomToolkit.App/MainWindow.Paint.cs`
   - `src/ClassroomToolkit.App/Services/PaintWindowOrchestrator.cs`
   - `src/ClassroomToolkit.App/Diagnostics/SystemDiagnostics.cs`
   - `src/ClassroomToolkit.Services/Presentation/PresentationControlOptions.cs`
   - `src/ClassroomToolkit.Services/Presentation/PresentationControlService.cs`
   - `src/ClassroomToolkit.Services/Presentation/PresentationGateway.cs`
   - `src/ClassroomToolkit.Application/UseCases/Presentation/PresentationContracts.cs`
   - `tests/ClassroomToolkit.Tests/AppSettingsServiceTests.cs`
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
