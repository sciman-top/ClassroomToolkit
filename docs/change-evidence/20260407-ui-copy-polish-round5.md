# 20260407 UI Copy Polish Round5

## 依据
- 继续压缩剩余诊断和设置页文案，优先去掉上下文里已经可省略的限定词。
- 保持含义不变，只收紧表述。

## 变更
- `src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml`
  - 将“恢复启动提示”收紧为“恢复提示”。
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
- 若按钮语义不够明确，恢复为“恢复启动提示”，并同步回退测试断言。

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
