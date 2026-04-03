# 全屏互切与前台焦点人工回归矩阵（2026-04-03）

适用范围：`PowerPoint/WPS 全屏`、`PDF/图片全屏`、`白板`、`光标/绘图模式`、`键盘/滚轮翻页`、`工具条/点名/启动器置顶`

## 1. 执行约束

- 环境：Windows + 双屏/投影，PowerPoint 与 WPS 可全屏放映。
- 必须打开调试输出，采集 `Debug.WriteLine` 日志。
- 每个场景执行后记录：`结果(PASS/FAIL)`、`日志片段`、`截图/录屏路径`、`备注`。

## 2. 回归矩阵

| ID | 场景 | 操作步骤 | 预期结果 | 日志锚点 | 代码锚点 | 自动化覆盖 |
|---|---|---|---|---|---|---|
| S01 | PPT 全屏进入/退出 | 启动 PPT 放映 -> 打开画笔工具 -> 退出放映 | 进入时识别为 `PresentationFullscreen`；退出后会话回到 `Idle` 或其他真实场景 | `[UiSession] evt=EnterPresentationFullscreenEvent/ExitPresentationFullscreenEvent`、`[PresentationState]` | `PaintOverlayWindow.Presentation.cs` `UpdatePresentationFullscreenState` | `CrossPageDisplayLifecycleContractTests` + `Presentation*PolicyTests` |
| S02 | WPS 全屏进入/退出 | 启动 WPS 放映 -> 切换光标/绘图 -> 退出放映 | WPS 导航钩子按策略启停；退出后不残留拦截 | `[WpsNavHook]`、`[PresentationState] reason=wps-hook-enabled/wps-hook-disabled` | `UpdateWpsNavHookStateCoreAsync`、`WpsHookEnableGatePolicy` | `WpsHookEnableGatePolicyTests`、`PresentationNavigationAdmissionPolicyTests` |
| S03 | 图片全屏进入/退出 | 从图片管理器打开图片 -> Esc 退出全屏 -> 再次进入同图 | 全屏状态与用户上次操作一致，不应被硬重置 | `[PhotoNav]`、`[UiSession] evt=EnterPhotoFullscreenEvent/ExitPhotoFullscreenEvent` | `PaintOverlayWindow.Photo.Navigation.cs` `EnterPhotoMode/SetPhotoWindowMode` | `PhotoOverlayReentryPolicyTests`、`PhotoFullscreenBoundsEnforcementContractTests` |
| S04 | PDF 全屏进入/退出 | 打开 PDF -> 翻页 -> Esc 退出全屏 | PDF 页码切换、笔迹加载、退出过程一致 | `[PhotoNav][Overlay.Nav]`、`[InkCache]` | `PaintOverlayWindow.Pdf.cs`、`PaintOverlayWindow.Photo.Navigation.cs` | `CrossPagePdfVisiblePrefetchUpdatePolicyTests`、`PhotoNavigation*Tests` |
| S05 | 白板进入/退出 | 在图片/PPT场景下开启白板 -> 退出白板 | 白板进入时清理/挂起策略正确；退出后恢复到真实来源场景 | `[UiSession] evt=EnterWhiteboardEvent/ExitWhiteboardEvent` | `PaintOverlayWindow.Board.cs`、`WhiteboardResumeSceneResolver.cs` | `BoardTransitionCrossPagePolicyTests`、`UiSession*Tests` |
| S06 | 三者互切（PPT/WPS ↔ 图片/PDF ↔ 白板） | 依次执行：PPT->白板->PPT，PPT->图片->白板->图片，PDF->白板->退出 | 场景状态机与焦点所有者一致，不出现“假全屏”或卡死 | `[UiSession]`、`[UiSession][Violation]`（应为空） | `UiSessionReducer.cs`、`SessionTransitionWindowingPolicy.cs` | `SessionTransition*Tests`、`UiSessionInvariants` |
| S07 | 光标/绘图模式切换 | 在 PPT/WPS/图片全屏中反复切换 Cursor/Brush/Eraser | Cursor 模式可透传，绘图模式可书写；切换后钩子/焦点策略联动 | `[PresentationState]`、`[PhotoInputTelemetry]` | `SetMode`、`OverlayInputPassthroughPolicy`、`OverlayFocusAcceptancePolicy` | `OverlayInputPassthroughPolicyTests`、`OverlayFocusAcceptancePolicyTests` |
| S08 | 键盘翻页（Overlay） | Overlay 前台时按 PgUp/PgDn/方向键 | 仅在允许场景发送上一页/下一页；不误吞 | `[PresentationState]`、`[WpsNavHook]` | `OnKeyDown`、`TryHandlePresentationKey` | `PresentationKeyCommandPolicyTests` |
| S09 | 键盘翻页（工具条/点名窗口） | 工具条或点名窗口前台时按 PgUp/PgDn | 只有“实际发送成功”才 `Handled=true`；失败时不上锁输入 | 日志不应出现“有处理但无翻页” | `AuxWindowKeyRoutingHandler.cs`、`MainWindow.Photo.cs` | `AuxWindowKeyRoutingHandlerTests` |
| S10 | 滚轮翻页（工具条/点名窗口） | 工具条或点名窗口前台滚轮滚动 | 按演示策略路由到上一页/下一页；近期墨迹冲突时受抑制 | `[WpsNavHook]`、`[PresentationState]` | `AuxWindowWheelRoutingHandler.cs`、`ForwardWheelToPresentation` | `AuxWindowWheelRoutingHandlerTests`、`OverlayWheelPresentationExecutionPolicyTests` |
| S11 | 前台/置顶关系 | 在三类全屏场景下显示工具条、点名、启动器 | 三者持续可见且 Topmost，不被全屏窗口压住；交互后可自动补位 | `FloatingZOrderRequested`、`[UiSession][Surface]` | `FloatingWindowCoordinator`、`FloatingTopmostExecutionExecutor` | `Floating*PolicyTests`、`Launcher*PolicyTests` |
| S12 | 焦点恢复 | Cursor 模式下从工具条点击回演示 | 允许条件满足时自动恢复演示焦点 | `[PresentationState] reason=focus-restored` | `PresentationFocusRestorePolicy`、`MonitorPresentationFocus` | `PresentationFocusRestorePolicyTests`、`PresentationFocusMonitorPolicyTests` |
| S13 | 笔迹清空/保存/显示 | 在图片/PDF/白板中写笔迹 -> 清空 -> 切页 -> 返回 | 清空后无残影；保存与显示开关语义一致 | `[InkPersist]`、`[InkCache]` | `ClearAll` 相关、`InkShowTransitionCoordinator`、`InkSaveUpdateTransitionPolicy` | `PaintOverlayClearAllCrossPageRecoveryContractTests`、`Ink*Tests` |

## 3. 对照结论（2026-04-03 代码状态）

- 已补齐：
- `辅助窗口键盘路由误吞`：已改为布尔回传成功才标记 handled。
- `辅助窗口滚轮翻页缺口`：已新增路由链路并接入工具条、点名窗口。
- `图片模式重入全屏硬重置`：已改为保留当前会话全屏状态。
- `演示焦点恢复开关未启用`：已开启恢复逻辑。
- `白板开启强退照片`：已移除 orchestrator 强制 `ExitPhotoMode`，改为由 overlay 白板场景机制负责挂起/恢复。

- 仍需人工确认的高价值盲区：
- `真实 Office/WPS 全屏窗口差异`（不同版本窗口类名、多显示器 DPI 缩放、系统权限干扰）。
- `启动器气泡与主窗口切换瞬间的置顶抖动`（已做策略去抖，仍建议录屏验证）。
- `长课时运行后的输入钩子退化`（建议至少 30 分钟持续切换压测）。

## 4. 执行记录模板

每条场景记录一行：

`[ID] PASS/FAIL | 环境 | 日志片段路径 | 截图/录屏路径 | 备注`
