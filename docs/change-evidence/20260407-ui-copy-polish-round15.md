# 20260407 UI Copy Polish Round15

## 依据
- 继续压缩画笔设置页里语义明确的开关文案。
- 目标是让按钮和复选框更短，但不失去可理解性。

## 变更
- `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml`
  - 将 `滚轮映射为翻页键` 收紧为 `滚轮翻页`。
  - 将 `全屏放映自动置顶` 收紧为 `全屏置顶`。
- `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`
  - 同步更新规则条与画笔设置契约。

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
- 若“滚轮翻页”或“全屏置顶”导致语义不够直观，回退到原表述。

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
