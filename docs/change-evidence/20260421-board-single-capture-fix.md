# 2026-04-21 白板按钮单击截图回归修复

- issue_id: `toolbar-board-single-capture-regression`
- risk_level: `medium`
- scope:
  - `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.TouchFirstActions.cs`
  - `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`

## 依据

- 用户反馈：原行为为“单击截图，双击直入白板”，现状变成“单击直接进入白板”。
- 根因：白板主动作状态被记忆为 `EnterWhiteboard` 后，后续主点击可沿用该状态，导致单击路径漂移。

## 变更

- 修复白板按钮主点击路径：
  - 默认主点击回归为截图（`BeginRegionCaptureAction`）。
  - 当处于“截图待选态”再次点击（第二击）时，直接进入白板（`EnterWhiteboardAction`）。
- 保留 `_lastBoardPrimaryAction` 字段用于契约兼容与状态记录，但不再驱动单击主路径决策，避免再次漂移。

## 执行命令与关键输出

1. 常规门禁尝试（受进程占用阻断）
   - `dotnet build ClassroomToolkit.sln -c Debug`
   - 失败：`MSB3021/MSB3027`，`sciman Classroom Toolkit.exe` 占用输出文件（含 `pdfium.dll`）。
   - N/A: `gate_na`
     - reason: 运行中的应用锁定默认输出目录，无法在默认路径完成 build/test 覆盖写入
     - alternative_verification: 使用独立输出目录进行等效编译/测试验证
     - evidence_link: 本文件
     - expires_at: `2026-05-21`

2. 替代 build 验证（独立输出目录）
   - `dotnet build src/ClassroomToolkit.App/ClassroomToolkit.App.csproj -c Debug -p:UseAppHost=false -p:OutDir=D:\OneDrive\CODE\ClassroomToolkit\artifacts\verify\app\`
   - 结果：`0 warning, 0 error`

3. 替代 test 验证（独立输出目录 + 相关测试子集）
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseAppHost=false -p:OutDir=D:\OneDrive\CODE\ClassroomToolkit\artifacts\verify\tests\ --filter "FullyQualifiedName~PaintToolbarTouchSettingsContractTests|FullyQualifiedName~ToolbarSecondTapIntentPolicyTests|FullyQualifiedName~BoardPrimaryActionTests|FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests"`
   - 结果：`Passed 21, Failed 0`

## 回滚

- `git checkout -- src/ClassroomToolkit.App/Paint/PaintToolbarWindow.TouchFirstActions.cs src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
- 或对对应 commit 执行 `git revert <commit_sha>`。
