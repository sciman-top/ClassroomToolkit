# 20260407 UI Copy Polish Round3

## 依据
- 继续收敛课堂工具箱可见文案，优先保留语义，压缩冗长提示与按钮说明。
- 目标是让界面提示更短、更直接，同时不改动现有操作路径和契约行为。

## 变更
- `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml`
  - 缩短基础页与工具栏说明。
  - 收紧规则包状态提示。
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml`
  - 缩短收藏操作提示。
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml`
  - 收紧预览关闭、适宽与导出提示。
- `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
  - 收紧白板按钮提示。
- `tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs`
  - 同步更新白板按钮文案契约。

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
- 若文案调整引入识别偏差或契约失败，回退上述 XAML 和测试中的对应字符串修改。

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
