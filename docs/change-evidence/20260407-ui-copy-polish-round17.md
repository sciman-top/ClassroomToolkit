# 20260407 UI Copy Polish Round17

## 依据
- 继续压缩点名设置页和画笔设置页里仍然偏长、但语义明确的说明句。
- 目标是在不改变含义的前提下，进一步减少句尾冗余。

## 变更
- `src/ClassroomToolkit.App/RollCallSettingsDialog.xaml`
  - 将学生照片说明收紧为“显示学生照片”。
  - 将照片页提示改为“按需设置。”。
  - 将语音、遥控和提醒相关说明收短。
- `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml`
  - 将两条排障提示分别收紧为“排障时再调。”和“兼容异常时再调。”。
- `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`
  - 同步更新点名设置页和画笔设置页的文案契约。

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
- 若“显示学生照片”“按需设置。”或两条排障提示过度压缩，回退到上一版表述。

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
