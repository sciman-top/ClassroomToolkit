# 20260407 UI Copy Polish Round29

## 依据
- 继续压缩点名窗口、导出流程和系统诊断里仍然偏长的运行时提示。
- 目标是保持可执行建议不变，同时让教师在弹窗里更快扫完。

## 变更
- `src/ClassroomToolkit.App/RollCallWindow.State.cs`
  - 将语音播报不可用提示收短。
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Export.cs`
  - 将截图缺失、导出异常和导出前自动保存失败提示收短。
- `src/ClassroomToolkit.App/Diagnostics/SystemDiagnostics.cs`
  - 将系统语音建议和当前目录可写建议收短。
- `tests/ClassroomToolkit.Tests/App/SystemDiagnosticsCopyContractTests.cs`
  - 更新系统诊断文案契约。
- `tests/ClassroomToolkit.Tests/App/RollCallAndExportCopyContractTests.cs`
  - 更新点名窗口与导出流程文案契约。

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
- 若语音、导出或系统建议过短影响排查，回退到上一版表述。

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
