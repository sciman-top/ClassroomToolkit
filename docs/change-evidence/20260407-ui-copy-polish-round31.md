# 20260407 UI Copy Polish Round31

## 依据
- 继续压缩导出进度、导出完成和点名重置确认句。
- 目标是让长时间操作的反馈更短，减少弹窗阅读负担。

## 变更
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Export.cs`
  - 将导出进度、导出完成和自动保存失败提示进一步收短。
- `src/ClassroomToolkit.App/RollCallWindow.Input.cs`
  - 将点名重置确认句收短。
- `tests/ClassroomToolkit.Tests/App/RollCallAndExportCopyContractTests.cs`
  - 同步更新点名重置与导出完成契约。

## 验证
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RollCallAndExportCopyContractTests|FullyQualifiedName~StartupCompatibilityAutoRemediationPolicyTests|FullyQualifiedName~StartupCompatibilityWarningCopyContractTests|FullyQualifiedName~SystemDiagnosticsCopyContractTests"`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 结果
- build: PASS
- test: PASS
- contract/invariant: PASS
- hotspot: PASS

## 回滚
- 若导出进度或重置确认句过短影响理解，回退到上一版表述。

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
