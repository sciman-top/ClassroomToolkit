# 20260407 UI Copy Polish Round30

## 依据
- 继续压缩点名窗口和导出流程里剩余的运行时提示。
- 目标是在不改变修复动作的前提下，继续减少弹窗里的冗余字数。

## 变更
- `src/ClassroomToolkit.App/RollCallWindow.State.cs`
  - 将学生名册保存失败和语音播报不可用提示进一步收短。
- `src/ClassroomToolkit.App/RollCallWindow.Input.cs`
  - 将翻页笔监听不可用提示进一步收短。
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Export.cs`
  - 将导出空目录和导出前自动保存失败提示收短。
- `tests/ClassroomToolkit.Tests/App/RollCallAndExportCopyContractTests.cs`
  - 同步更新点名与导出流程契约。

## 验证
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~SystemDiagnosticsCopyContractTests|FullyQualifiedName~RollCallAndExportCopyContractTests|FullyQualifiedName~StartupCompatibilityAutoRemediationPolicyTests|FullyQualifiedName~StartupCompatibilityWarningCopyContractTests"`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 结果
- build: PASS
- test: PASS
- contract/invariant: PASS
- hotspot: PASS

## 回滚
- 若保存、监听或导出提示过短影响排查，回退到上一版表述。

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
