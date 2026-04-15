# 20260407 UI Copy Polish Round19

## 依据
- 继续压缩兼容提示页、点名设置页和启动兼容结果句里仍然略长的表述。
- 目标是让界面和运行时输出保持同一套更短的口径。

## 变更
- `src/ClassroomToolkit.App/Diagnostics/StartupCompatibilityWarningDialog.xaml`
  - 将底部恢复提示说明收紧为“已切到兼容优先模式。可在兼容诊断恢复提示。”。
- `src/ClassroomToolkit.App/Startup/StartupOrchestrator.cs`
  - 将启动兼容结果句收紧为“结论：已切到兼容优先模式，可继续上课。”。
- `src/ClassroomToolkit.App/Diagnostics/StartupCompatibilityAutoRemediationPolicy.cs`
  - 将自动修复动作说明收短。
- `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml`
  - 将排障输入日志开关收紧为“输入日志”。
- `src/ClassroomToolkit.App/RollCallSettingsDialog.xaml`
  - 将语音说明改成并列口径“控制朗读、发音人、提醒声。”。
- `tests/ClassroomToolkit.Tests/App/StartupCompatibilityWarningDialogContractTests.cs`
  - 同步更新兼容提示页契约。
- `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`
  - 同步更新点名设置页和画笔设置页契约。

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
- 若兼容提示、自动修复动作或语音说明过短影响理解，回退到上一版表述。

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
