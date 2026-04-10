# 20260407 UI Copy Polish Round4

## 依据
- 继续收紧剩余界面文案，优先缩短不改变语义的提示、说明和选项。
- 以用户更容易快速扫读为目标，不改动功能路径与交互契约。

## 变更
- `src/ClassroomToolkit.App/AutoExitDialog.xaml`
  - 缩短自动关闭说明。
- `src/ClassroomToolkit.App/Ink/InkSettingsDialog.xaml`
  - 缩短笔迹回看、清理和路径提示。
- `src/ClassroomToolkit.App/Diagnostics/StartupCompatibilityWarningDialog.xaml`
  - 压缩兼容提示底部说明。
- `src/ClassroomToolkit.App/RollCallSettingsDialog.xaml`
  - 缩短语音、遥控与提醒页说明和选项。
- `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml`
  - 缩短画笔、图片和放映相关选项说明。
- `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`
  - 同步更新自动关闭文案契约。
- `tests/ClassroomToolkit.Tests/App/StartupCompatibilityWarningDialogContractTests.cs`
  - 同步更新兼容提示文案契约。

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
- 若缩写导致歧义或契约回归，回退上述字符串修改，并同步恢复测试断言。

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
