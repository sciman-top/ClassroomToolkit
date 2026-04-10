# 20260407 UI Copy Polish Round28

## 依据
- 继续压缩点名窗口、导出流程和系统诊断里的运行时提示。
- 目标是让教师直接看到的错误、建议和确认句更短、更直接。

## 变更
- `src/ClassroomToolkit.App/RollCallWindow.State.cs`
  - 将名册读取/保存和设置保存失败提示收短。
- `src/ClassroomToolkit.App/RollCallWindow.Input.cs`
  - 将翻页笔监听不可用提示收短。
- `src/ClassroomToolkit.App/RollCallSettingsDialog.xaml.cs`
  - 将冲突提示、恢复提示和未修改提示收短。
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Export.cs`
  - 将加载笔迹、导出失败、自动保存失败和空目录提示收短。
- `tests/ClassroomToolkit.Tests/App/SystemDiagnosticsCopyContractTests.cs`
  - 补充系统诊断新增短句契约。
- `tests/ClassroomToolkit.Tests/App/RollCallAndExportCopyContractTests.cs`
  - 新增点名窗口与导出流程文案契约。

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
- 若运行时错误提示过短影响排查，回退到上一版表述。

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
