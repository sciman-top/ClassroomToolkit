# 20260407 UI Copy Polish Round13

## 依据
- 继续压缩诊断页里仍偏长的按钮文案。
- 在上下文明确的前提下减少重复词。

## 变更
- `src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml`
  - 将“导出诊断包”收紧为“导出包”。
- `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`
  - 同步更新诊断页按钮文案契约。

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
- 若用户觉得“导出包”不够明确，回退为“导出诊断包”。

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
