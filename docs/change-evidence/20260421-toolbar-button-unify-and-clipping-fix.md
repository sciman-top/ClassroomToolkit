# 2026-04-21 工具条按钮尺寸统一与图标裁剪修复

- issue_id: `toolbar-button-unify-clipping`
- risk_level: `low`
- scope:
  - `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`

## 依据

- 现象：工具条按钮视觉大小不一致，部分图标底部被裁剪。
- 根因：工具条容器固定 `Height="46"`，而共享样式中的按钮最小触控高度为 `44/48/56`，在同一容器中出现压缩与不一致布局。

## 变更

- 在 `PaintToolbarWindow.xaml` 内增加局部紧凑样式（仅作用于工具条窗口）：
  - `Style_ToolbarIconButton`
  - `Style_ToolbarIconToggleButton`
  - `Style_ToolbarColorBubbleToggle`
- 将工具条按钮统一切换到以上局部样式，统一视觉尺寸与最小尺寸。
- `ToolbarContainer` 从固定 `Height="46"` 调整为 `MinHeight="46"`，避免在内容略高时被硬裁剪。
- 统一第二组按钮容器垂直内边距：`Padding="4,1"` -> `Padding="4,2"`，减少上下偏差。

## 执行命令与关键输出

1. build（独立输出目录，避免运行中程序锁文件影响）
   - `dotnet build src/ClassroomToolkit.App/ClassroomToolkit.App.csproj -c Debug -p:UseAppHost=false -p:OutDir=D:\OneDrive\CODE\ClassroomToolkit\artifacts\verify\toolbar_ui\app\`
   - 结果：`0 warning, 0 error`

2. test（相关契约测试子集）
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseAppHost=false -p:OutDir=D:\OneDrive\CODE\ClassroomToolkit\artifacts\verify\toolbar_ui\tests\ --filter "FullyQualifiedName~OverlayWindowsXamlContractTests|FullyQualifiedName~PaintToolbarTouchSettingsContractTests|FullyQualifiedName~TouchFirstMetricsXamlContractTests|FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests"`
   - 结果：`Passed 21, Failed 0`

## 回滚

- `git checkout -- src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
- 或对对应 commit 执行 `git revert <commit_sha>`。
