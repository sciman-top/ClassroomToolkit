# 20260407 UI Copy Polish Round7

## 依据
- 继续压缩资源管理、预览层和画笔设置里的辅助提示。
- 目标是让说明更短、更好扫读，同时不改变操作含义。

## 变更
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml`
  - 缩短地址栏和视图切换提示。
- `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml`
  - 缩短预览关闭提示。
- `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`
  - 同步更新资源窗口相关文案契约。

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
- 若“点击空白关闭”“输入后回车”“列表”“缩略图”造成歧义，回退到原表述并恢复测试断言。

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
