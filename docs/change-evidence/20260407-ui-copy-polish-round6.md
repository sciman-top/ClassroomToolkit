# 20260407 UI Copy Polish Round6

## 依据
- 继续压缩剩余设置页和诊断页文案，优先收紧说明句和按钮名。
- 目标是减少阅读负担，不改变语义和交互路径。

## 变更
- `src/ClassroomToolkit.App/RollCallSettingsDialog.xaml`
  - 缩短显示、语音、遥控和提醒页说明及选项。
- `src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml`
  - 将“复制全部”收紧为“复制结果”。
- `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`
  - 同步更新点名设置与诊断页文案契约。

## 验证
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 结果
- build: PASS
- test: PASS
- contract/invariant: PASS
- hotspot: PASS

## 回滚
- 若“复制结果”不如原文清晰，回退为“复制全部”，并同步恢复测试断言。

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
