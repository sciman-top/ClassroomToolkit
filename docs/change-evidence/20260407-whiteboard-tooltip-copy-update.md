# 20260407 Toolbar And Shell Copy Update

- Rule: C.5 evidence and rollback traceability.
- Risk: low
- Change: refined toolbar and shell tooltip copy for clearer action wording, removed the extra `使用` prefix from quick-color tooltips, shortened paint settings helper copy, compacted several management subtitles, trimmed one image-manager input hint, and corrected the ink settings/diagnostics path wording from a photo-storage label to a neutral image-retention label without changing the underlying settings key. During verification, management-dialog titles were kept on the repo's short-title contract and not expanded.
- Verification plan: `dotnet build ClassroomToolkit.sln -c Debug`, `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`, contract/invariant subset, hotspot budget check.
- Rollback: restore the previous tooltip strings in `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`, `src/ClassroomToolkit.App/MainWindow.xaml`, and `src/ClassroomToolkit.App/RollCallWindow.xaml`, plus the previous path wording in `src/ClassroomToolkit.App/Ink/InkSettingsDialog.xaml` and `src/ClassroomToolkit.App/Diagnostics/SystemDiagnostics.cs`, and the previous titles in `src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml` and `src/ClassroomToolkit.App/RemoteKeyDialog.xaml`; keep the board-button contract assertion aligned in `tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs`.

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
