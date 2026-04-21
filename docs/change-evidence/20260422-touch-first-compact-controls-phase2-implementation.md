规则ID=R1,R2,R6,R8
影响模块=region capture passthrough; scrollable touch surfaces; image manager compact hit targets
当前落点=Task 5-6 已完成；Task 7 低风险子集已完成
目标归宿=真实触点来源、滚动主路径、图片管理器小热区收口
迁移批次=touch-compact-phase2
风险等级=Medium
执行命令=
- dotnet build ClassroomToolkit.sln -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests|FullyQualifiedName~DialogTouchFlowContractTests|FullyQualifiedName~RegionCaptureInitialPassthroughPolicyTests|FullyQualifiedName~ToolbarPassthroughActivationPolicyTests"
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ImageManagerTouchFlowContractTests|FullyQualifiedName~DialogTouchFlowContractTests|FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests"
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
- git diff -- src/ClassroomToolkit.App/Paint/RegionCaptureInitialPassthroughPolicy.cs src/ClassroomToolkit.App/Paint/RegionScreenCaptureWorkflow.cs src/ClassroomToolkit.App/Paint/RegionSelectionOverlayWindow.xaml.cs src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs src/ClassroomToolkit.App/MainWindow.Paint.cs src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml src/ClassroomToolkit.App/ClassSelectDialog.xaml src/ClassroomToolkit.App/StudentListDialog.xaml src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml tests/ClassroomToolkit.Tests/App/DialogTouchFlowContractTests.cs tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs tests/ClassroomToolkit.Tests/ImageManagerTouchFlowContractTests.cs
验证证据=
- build 通过，0 warning / 0 error
- 第二批定向测试通过：32/32
- 第三批定向测试通过：18/18
- 全量测试通过：3395/3395
- contract/invariant 通过：28/28
- 热点复核通过：截图重放链路已从 Cursor 回退为真实触点优先；主要滚动页面已补 VerticalFirst；图片管理器星标按钮已切换到小视觉+大热区样式
回滚动作=
- 回滚文件：
  - src/ClassroomToolkit.App/Paint/RegionCaptureInitialPassthroughPolicy.cs
  - src/ClassroomToolkit.App/Paint/RegionScreenCaptureWorkflow.cs
  - src/ClassroomToolkit.App/Paint/RegionSelectionOverlayWindow.xaml.cs
  - src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs
  - src/ClassroomToolkit.App/MainWindow.Paint.cs
  - src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml
  - src/ClassroomToolkit.App/ClassSelectDialog.xaml
  - src/ClassroomToolkit.App/StudentListDialog.xaml
  - src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml
  - tests/ClassroomToolkit.Tests/App/DialogTouchFlowContractTests.cs
  - tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs
  - tests/ClassroomToolkit.Tests/ImageManagerTouchFlowContractTests.cs
- 删除本证据文件

## 本轮改动

1. 区域截图初始取消、遮罩透传取消、工具条点击重放统一改为真实屏幕点优先，不再只依赖 `Cursor.Position`。
2. 工具条新增最近一次交互屏幕点记录，供区域截图恢复与点击重放复用。
3. 画笔设置、班级选择、学生名单、图片管理器主要滚动控件统一补上 `VerticalFirst` 触摸平移。
4. 图片管理器收藏/最近星标按钮切换到共享 `Style_IconButton`，实现小视觉 + 大热区。

## 热点人工复核

1. 真实触点链路：`MainWindow.Paint -> RegionScreenCaptureWorkflow -> RegionSelectionOverlayWindow -> toolbar replay` 已连通；风险点是键盘触发截图时仍会回退到系统光标，这属于合理兜底。
2. 滚动主路径：本轮只补显式 `PanningMode`，未调整滚动惯性和滚动条尺寸；真机上仍需确认具体手感。
3. 图片管理器热区：星标按钮已切到共享 icon button 热区模型；缩略图区与其它工具条按钮一致性问题仍未全部收口。

## 任务状态

1. Task 5：已完成
2. Task 6：已完成
3. Task 7：部分完成  
已完成：收藏/最近小热区、滚动主路径  
未完成：缩略图区更深层的触屏一致性与大目录性能/虚拟化优化
4. Task 8：未开始
5. Task 9：未开始
