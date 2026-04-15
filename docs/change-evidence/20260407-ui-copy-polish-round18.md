# 20260407 UI Copy Polish Round18

## 依据
- 继续压缩资源管理页和兼容提示页里仍然偏长、但语义稳定的提示句。
- 目标是在不丢上下文的前提下，把空态提示和勾选项再收短一点。

## 变更
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml`
  - 将空态提示收紧为“先选左侧文件夹”。
- `src/ClassroomToolkit.App/Diagnostics/StartupCompatibilityWarningDialog.xaml`
  - 将“不再提示”选项收紧为“本问题不再提示”。
- `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`
  - 同步更新资源管理页和兼容提示页的文案契约。

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
- 若“先选左侧文件夹”或“本问题不再提示”显得过短或歧义，回退到上一版表述。

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
