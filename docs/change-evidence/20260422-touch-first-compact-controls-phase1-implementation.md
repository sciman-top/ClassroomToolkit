规则ID=R1,R2,R6,R8
影响模块=Paint toolbar; touch contracts; docs/change-evidence
当前落点=Task 1-4 第一批实现
目标归宿=紧凑视觉、热区下限、自由触摸拖动、白板显式二级面板
迁移批次=touch-compact-phase1
风险等级=Medium
执行命令=
- dotnet build ClassroomToolkit.sln -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PaintToolbarTouchSettingsContractTests|FullyQualifiedName~PaintToolbarDragModeContractTests|FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests|FullyQualifiedName~TouchFirstMetricsXamlContractTests|FullyQualifiedName~ToolbarScaleDefaultsTests"
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
- git diff -- src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs src/ClassroomToolkit.App/Paint/PaintToolbarWindow.TouchFirstActions.cs tests/ClassroomToolkit.Tests/PaintToolbarTouchSettingsContractTests.cs tests/ClassroomToolkit.Tests/PaintToolbarDragModeContractTests.cs tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs
验证证据=
- build 通过，0 warning / 0 error
- 定向工具条与触屏合同测试通过：19/19
- 全量测试通过：3394/3394
- contract/invariant 通过：28/28
- 热点复核通过：本轮仅修改工具条局部样式、工具条代码与合同测试；未引入按钮吸附；未增加一级按钮数量
回滚动作=
- 回滚文件：
  - src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml
  - src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs
  - src/ClassroomToolkit.App/Paint/PaintToolbarWindow.TouchFirstActions.cs
  - tests/ClassroomToolkit.Tests/PaintToolbarTouchSettingsContractTests.cs
  - tests/ClassroomToolkit.Tests/PaintToolbarDragModeContractTests.cs
  - tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs
- 删除本证据文件

## 本轮改动

1. 去掉工具条局部样式对 `30x30` 最小命中区的覆盖，避免共享触摸最小值被直接压扁。
2. 在工具条缩放逻辑中加入按缩放反算的最小热区，保证缩小视觉时仍保留触摸命中下限。
3. 新增工具条触摸拖动路径，继续保持自由拖动，只做边界夹紧，不做吸附。
4. 白板按钮空闲态改为显式打开现有二级操作弹层，不再默认直接进入截图。

## 热点人工复核

1. 缩放与热区：当前通过 `44 / scale` 反算热区下限，能够在 `0.8` 缩放时保持约 `44 DIP` 的实际命中区；风险点是后续若改为别的缩放承载层，需要同步复核命中公式。
2. 自由拖动：鼠标与触摸都走统一的窗口边界夹紧逻辑，未出现吸附代码；风险点是触摸拖动与按钮轻点冲突仍需真机回归。
3. 白板显式入口：`BoardActionsPopup` 现在具备显式打开路径；风险点是教师从“单击截图”旧习惯切换到“单击弹层”新路径，需要真机课堂流复核。

## 后续建议

1. 下一批继续做颜色/图形二级入口的显式化，减少对 tooltip 和记忆规则的依赖。
2. 在真机上补一轮最小缩放状态下的手指点击与拖动验收。
