# 20260419-ui-link-alignment-cleanup

- issue_id: ui-link-alignment-cleanup
- attempt_count: 1
- clarification_mode: direct_fix
- rule_ids: R1,R2,R6,R8
- risk_level: low
- scope:
  - src/ClassroomToolkit.App/AboutDialog.xaml
  - src/ClassroomToolkit.App/AboutDialog.xaml.cs
  - src/ClassroomToolkit.App/Photos/RollCallGroupOverlayWindow.xaml

## Basis
- 工作区剩余未提交改动仅包含 3 个受版本管理文件。
- 变更内容为：
  1. About 对话框中的 GitHub 链接从 `ClassroomTools` 更正为 `ClassroomToolkit`
  2. About 对话框复制文本中的 GitHub 地址同步更正
  3. 点名分组浮层标题增加水平居中，避免文本视觉偏左
- `.agent-build/`、`.governed-ai/`、`tests/ClassroomToolkit.Tests/bin-agent/` 属于本地运行/缓存产物，不纳入版本管理。

## Commands / Evidence
- `dotnet build ClassroomToolkit.sln -c Debug`
  - exit_code: 0
  - key_output: `0 Warning(s) / 0 Error(s)`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - exit_code: 0
  - key_output: `Passed: 3295`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code: 0
  - key_output: `Passed: 28`

## Hotspot Review
- hotspot_files:
  - src/ClassroomToolkit.App/AboutDialog.xaml
  - src/ClassroomToolkit.App/AboutDialog.xaml.cs
  - src/ClassroomToolkit.App/Photos/RollCallGroupOverlayWindow.xaml
- checks:
  1. 链接展示文本与实际跳转地址保持一致。
  2. 剪贴板复制文本与 UI 展示一致，避免用户复制到旧仓库地址。
  3. `HorizontalAlignment=\"Center\"` 仅影响组名文本布局，不改变容器尺寸、命中测试或交互行为。

## Rollback
- `git restore --source=HEAD -- src/ClassroomToolkit.App/AboutDialog.xaml src/ClassroomToolkit.App/AboutDialog.xaml.cs src/ClassroomToolkit.App/Photos/RollCallGroupOverlayWindow.xaml docs/change-evidence/20260419-ui-link-alignment-cleanup.md`
