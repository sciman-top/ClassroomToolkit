# sciman课堂工具箱 当前接手说明

最后更新：2026-07-15
当前主线：`课堂高频链路稳定性 + 触控体验收口 + 发布前现场验证`

## 1. 先看什么

当前真相的优先级如下：

1. 代码与测试结果
2. [README](../README.md)
3. 本文档
4. 最近两批 [change-evidence](./change-evidence/)
5. 旧的计划、阶段文档和历史验收材料

如果历史文档和当前代码 / 当前测试冲突，以当前代码和当前测试为准。

## 2. 当前状态快照

### 最新本地验证结果

- `dotnet build ClassroomToolkit.sln -c Debug`
  - 通过，0 warning / 0 error
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - 通过，29/29
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - 当前结果：3533 通过，0 失败

### 当前代码阻断项

- 无。完整测试集、契约 / 不变量过滤集与构建均已通过。
- 自动化冻结复检任务 `automated-freeze-recheck-after-gap-closure` 已完成：`ArchitectureDependencyTests`=`5/5`，全量 Debug=`2227/2227`，全量 Release=`2227/2227`（2026-03-13）；当前自动化门已闭合，下一步仅剩人工最终回归。
- 已收口项：照片叠加层空 bitmap 失败分支的契约已同步到 `EnterInactivePassthroughState()`，与关闭分支的透明穿透语义保持一致。
- 发布边界：代码门禁全绿不等于现场发布完成；多显示器、DPI、投影、PPT / WPS 放映和学生照片悬浮层仍需课堂现场验证。

## 3. 最近已经落地的切片

这些能力和加固已经进入代码主线，应作为当前事实而不是“未来计划”理解：

### 2026-05-31

- 白板入口、截图入白板、底色白板和白板内工具切换收口
  - 证据：`docs/change-evidence/20260531-whiteboard-entry-and-capture.md`
- 3 个快捷画笔粗细槽、设置页预览、图片 / PDF 撤销修复
  - 证据：`docs/change-evidence/20260531-toolbar-brush-slots-and-photo-undo.md`
- PPT / WPS 放映 retouch、WPS Enter 保留键与点名分组切换隔离
  - 证据：`docs/change-evidence/20260531-presentation-ink-retouch-wps-enter.md`
- 低风险鲁棒性加固
  - 证据：`docs/change-evidence/20260531-robustness-hardening.md`

### 2026-06-02

- 快捷画笔粗细选择误切颜色问题修复
  - 证据：`docs/change-evidence/20260602-paint-toolbar-brush-size-selection.md`
- Photo / PDF 撤销运行态历史策略补齐
  - 证据：`docs/change-evidence/20260602-photo-undo-runtime-history.md`
- 学生照片窗口层级、首帧复显、倒计时底栏样式收口
  - 证据：`docs/change-evidence/20260602-window-layer-countdown.md`
- 画笔设置对话框构造期空引用防护
  - 证据：`docs/change-evidence/20260602-paint-settings-dialog-nre.md`

## 4. 当前建议的下一步

优先级建议如下：

1. 如果目标是发布，补课堂现场验证
   - 多显示器
   - DPI 缩放
   - PPT / WPS 放映
   - 学生照片悬浮层与工具条 / 点名窗口的层级
2. 如果继续开发，按 `build -> test -> contract/invariant -> hotspot` 保持小步闭环
3. 新增窗口层级、Interop 或持久化改动时，同步更新 `docs/change-evidence/`

## 5. 当前热点文件

这几类文件是本轮最值得先看的地方：

- `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs`
- `src/ClassroomToolkit.App/Windowing/WindowTopmostExecutor.cs`
- `src/ClassroomToolkit.App/RollCallWindow.Photo.cs`
- `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
- `src/ClassroomToolkit.App/Paint/QuickColorPaletteWindow.xaml.cs`
- `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.LayoutAndLabels.cs`
- `tests/ClassroomToolkit.Tests/PhotoOverlayLoadFailureBranchContractTests.cs`
- `tests/ClassroomToolkit.Tests/PhotoOverlayTopmostNoActivateContractTests.cs`
- `tests/ClassroomToolkit.Tests/PaintToolbarTouchSettingsContractTests.cs`

## 6. 接手时不要做的事

- 不要看到旧 handover 或旧阶段文档写着“阻断”就默认当前仍未收口。
- 不要把代码门禁全绿直接等同于课堂现场发布完成。
- 不要回滚与当前任务无关的工作区改动。
- 不要把图片 / PDF / 白板 / PPT-WPS 的窗口层级问题当成单纯样式问题；这类问题默认先看行为和焦点语义。

## 7. 文档同步要求

只要以下任一项变化，就同步更新 `README.md`、`README.en.md`、`docs/README.md` 和本文档：

- 用户可见能力
- 本地运行 / 发布入口
- 当前测试真相
- 发布阻断项
- 最新推荐阅读顺序
