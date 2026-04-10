# 20260407 UI Copy Polish Round 2

- Rule: C.5 evidence and rollback traceability.
- Risk: low
- Change: tightened remaining visible copy in diagnostics, about, image manager, and photo overlay surfaces; shortened window titles and footer text; kept the startup warning and diagnostics contract tests aligned with the new wording.
- Verification: `dotnet build ClassroomToolkit.sln -c Debug`, `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`, contract subset, `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`.
- Rollback: restore the previous title and footer strings in `src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml`, `src/ClassroomToolkit.App/Diagnostics/StartupCompatibilityWarningDialog.xaml`, `src/ClassroomToolkit.App/AboutDialog.xaml`, `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml`, and `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml`; restore the matching titles in `src/ClassroomToolkit.App/Diagnostics/SystemDiagnostics.cs` and `src/ClassroomToolkit.App/Diagnostics/StartupCompatibilityWarningDialog.xaml.cs`; keep `tests/ClassroomToolkit.Tests/App/StartupCompatibilityWarningDialogContractTests.cs` aligned with the dialog footer copy.

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
