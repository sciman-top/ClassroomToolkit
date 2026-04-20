# 2026-04-21 学生照片展示关闭按钮移除

- issue_id: `photo-overlay-close-button-removal`
- risk_level: `low`
- scope:
  - `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml`
  - `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs`
  - `tests/ClassroomToolkit.Tests/App/PhotoOverlayWindowXamlLayoutContractTests.cs`
  - `tests/ClassroomToolkit.Tests/App/DialogTouchFlowContractTests.cs`

## 依据

- 用户反馈：学生照片展示右上角关闭按钮遮挡展示内容，要求移除。

## 变更

- 移除 `PhotoOverlayWindow.xaml` 中右上角 `CloseButton`。
- 移除未再使用的 `OnCloseClick` 事件处理。
- 将关闭手势绑定到照片本体：点击 `PhotoImage` 调用 `CloseOverlay()`。
- 保留 `CloseOverlay()`，自动关闭与外部流程关闭仍沿用原有路径。
- 更新契约测试为“不显示覆盖层关闭按钮，但点击照片关闭”。

## 执行命令与关键输出

1. build（独立输出目录）
   - `dotnet build src/ClassroomToolkit.App/ClassroomToolkit.App.csproj -c Debug -p:UseAppHost=false -p:OutDir=D:\OneDrive\CODE\ClassroomToolkit\artifacts\verify\photo_close_button\app\`
   - 结果：`0 warning, 0 error`

2. test（照片展示相关契约）
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseAppHost=false -p:OutDir=D:\OneDrive\CODE\ClassroomToolkit\artifacts\verify\photo_close_button\tests\ --filter "FullyQualifiedName~PhotoOverlayWindowXamlLayoutContractTests|FullyQualifiedName~DialogTouchFlowContractTests|FullyQualifiedName~OverlayWindowsXamlContractTests"`
   - 结果：`Passed 7, Failed 0`

3. follow-up test（保留点击照片关闭）
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseAppHost=false -p:OutDir=D:\OneDrive\CODE\ClassroomToolkit\artifacts\verify\photo_tap_close\tests\ --filter "FullyQualifiedName~PhotoOverlayWindowXamlLayoutContractTests|FullyQualifiedName~DialogTouchFlowContractTests|FullyQualifiedName~OverlayWindowsXamlContractTests|FullyQualifiedName~PhotoOverlayCloseHideGuardContractTests"`
   - 结果：`Passed 8, Failed 0`

## 回滚

- 恢复 `PhotoOverlayWindow.xaml` 中的 `CloseButton` 与 `PhotoOverlayWindow.xaml.cs` 中的 `OnCloseClick`。
- 或对对应 commit 执行 `git revert <commit_sha>`。
