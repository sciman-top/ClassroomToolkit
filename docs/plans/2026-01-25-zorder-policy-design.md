# Z-Order/前台裁决统一设计（2026-01-25）

## 背景与目标
现有窗口前后台与 Topmost 逻辑分散，导致 PPT/WPS、图片/PDF、白板之间切换时出现前台抢占、遮挡与焦点跳转。目标是统一裁决入口，确保：
- 图片/PDF 全屏与 PPT/WPS 全屏可共存（不互斥），仅按前台顺序裁决。
- 白板与 PPT/WPS 可共存（不互斥）。
- 工具条/点名窗口始终置顶。
- 图片管理保持前台（但不盖住工具条/点名）。
- “最近启用”决定主前台窗口（在互斥约束下）。

## 状态模型
- Active flags：PresentationFullscreenActive / PhotoFullscreenActive / WhiteboardActive。
- LastActivatedSurface：PresentationFullscreen / PhotoFullscreen / Whiteboard / ImageManager。
- 前台抑制锁：进入图片/PDF 全屏时短时阻止 SetForegroundWindow 抢前台。

## 统一裁决入口
新增 ApplyZOrderPolicy()：
1) 读取当前状态与最近启用顺序（栈），仅裁决前台顺序，不做互斥退出。
2) 解析前台窗口：按“最近启用”栈 → 现存状态回退；ImageManager 始终 Topmost，但仅在被激活时抢焦点。
3) 处理 Z-Order：
   - ImageManager SyncTopmost(true)，必要时 Activate。
   - 仅当前台为 Photo/Whiteboard 时激活 Overlay。
   - 工具条/点名窗口最后 SyncTopmost(true)，保证始终最前。

## 切换流程
- 进入图片/PDF 全屏：BeginForegroundSuppression → EnterPhotoMode → 更新最近启用 → ApplyZOrderPolicy。
- 进入 PPT/WPS 全屏：检测到全屏事件时仅刷新裁决，不退出图片/PDF。
- 白板开关：不退出 PPT；按 active 更新最近启用 → ApplyZOrderPolicy。

## 风险与回滚
- 风险：前台抑制期过长可能影响 PPT/WPS 激活；需控制在 0.5~0.8s。
- 回滚：仅需撤回 MainWindow 裁决方法与 PresentationWindowFocus 抑制逻辑。

## 验证建议
- PPT 全屏→白板→图片管理→进入图片/PDF，全程无前台抢占。
- WPS 全屏→白板→图片管理→进入图片/PDF，行为一致。
- 关闭/最小化图片管理后，前台回落到最近可用窗口。
