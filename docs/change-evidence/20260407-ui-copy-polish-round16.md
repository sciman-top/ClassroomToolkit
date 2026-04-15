# 20260407 UI Copy Polish Round16

## 依据
- 继续压缩画笔设置页里剩下的高级项和规则包标题。
- 目标是保留上下文可理解性，同时减少冗余字词。

## 变更
- `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml`
  - 将高级项默认说明收紧为“高级项默认收起。”。
  - 将兼容相关开关和规则包标题进一步收短。

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
- 若“收起”“锁定兼容”“自动学习特征”过于省略，回退为上一版表述。

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
