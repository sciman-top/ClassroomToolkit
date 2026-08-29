# ClassroomToolkit Component Specification 2.0

## 1. Button

### 1.1 Primary

用途：开始、确定、开始点名、关键提交。

- Height：40（普通）/ 44（课堂主操作）
- MinWidth：88
- Radius：7
- Background：Primary
- Foreground：TextOnPrimary
- Hover：PrimaryHover
- Pressed：PrimaryPressed
- Disabled：SurfaceAlt + TextDisabled
- Focus：2 DIP BorderFocus 外圈

禁止大范围 Glow。

### 1.2 Secondary

用途：设置、名单、预设、次级操作。

- Background：SurfaceAlt
- Border：BorderDefault
- Hover：Hover
- Pressed：Pressed
- Foreground：TextPrimary

### 1.3 Ghost

用途：标题栏按钮、工具栏普通动作。

- 默认透明
- Hover：Hover
- Pressed：Pressed
- 不使用永久边框

### 1.4 Warning / Reset

用途：重置、重新设置、刷新。

- 默认推荐使用 `WarningSoft` 背景 + `Warning` 图标/文字
- 不等同于危险
- 只有不可恢复动作才使用 Danger

### 1.5 Danger

用途：删除、清空、不可恢复。

- Default：DangerSoft
- Hover：Danger
- Pressed：DangerPressed
- 确认对话框不得把 Enter 默认落在危险按钮上

---

## 2. IconButton

- 可见图标：18–20 DIP
- 点击区域：40×40
- 核心课堂工具：44×44
- Icon stroke 视觉统一
- 未选中：TextSecondary
- Hover：TextPrimary + Hover 背景
- Selected：Primary + PrimarySoft 背景
- Disabled：TextDisabled

---

## 3. 图标系统

推荐：

- Segoe Fluent Icons
- 自有 SVG Path，但必须遵守统一 20/24 网格

规范：

- 普通 icon：20×20
- Hero icon：28–32
- Stroke：视觉约 1.5–2 DIP
- 不混用 Emoji
- 不混用 3D/拟物和线性 icon
- 主入口 Hero Icon 可有双色/品牌色，但工具条必须统一单色线性风格

---

## 4. Tab

设置页推荐“紧凑 Segmented Tab”。

- Height：36
- Radius：6
- Tab Padding：16×8
- 未选中：TextSecondary
- Hover：Hover
- Active：
  - Background：PrimarySoft
  - Foreground：Primary
  - 可加 2 DIP 底部 indicator
- Tab 间距统一
- 不使用高饱和满屏色块

---

## 5. Slider

- 视觉轨道：4 DIP
- 命中高度：28–32 DIP
- Thumb：14 DIP
- Hover/Focused Thumb：16–18 DIP
- Filled：Primary
- Unfilled：BorderDefault / SurfaceAlt
- Disabled：TextDisabled 系
- 数值在右侧固定宽度显示，避免布局抖动
- px/%/秒单位与数值间保留 4 DIP

---

## 6. CheckBox / Toggle

CheckBox：

- Box：18×18
- 整行命中高度：32+
- Label：Body
- Checked：Primary
- Focus：BorderFocus

Toggle：

- 仅用于二元状态
- 不用 Toggle 替代多选
- 开启必须同时有位置+颜色变化

---

## 7. ComboBox / DropDown

- Height：36–40
- Radius：6
- Padding：12
- Background：SurfaceAlt
- Border：BorderDefault
- Focus：BorderFocus
- Popup item：36
- Popup radius：8
- Popup 阴影轻量
- Arrow icon 与文本对齐统一

---

## 8. ContextMenu / Menu

- Radius：8
- MinWidth：160
- ItemHeight：36
- Icon：18
- Horizontal Padding：12
- Separator margin：6
- Shortcut 文本右对齐
- Danger menu item 仅文字/icon 使用 Danger，默认不整块红色

---

## 9. Tooltip

- 延时：约 450 ms
- Padding：8×6
- Font：12
- Radius：6
- Background：SurfaceElevated
- Foreground：TextPrimary
- 不展示长篇帮助
- 触屏关键动作不能只依赖 Tooltip

---

## 10. Dialog

### 结构

```text
TitleBar 48

Content
  Section
  controls

  Section
  controls

Footer 64
```

### 尺寸

- 常规宽度：480–620
- 大设置页：≤ 720
- 外边距：20–24

### Footer

左侧：
- 恢复默认 / 重置本页（如需要）

右侧：
- 取消
- 确定

要求：

- Enter → 确定（仅非危险对话框）
- Esc → 取消
- 关闭 X 与取消行为一致
- 不在 Footer 放 4 个同等视觉权重按钮
- “全部重置”应降为 Warning/Ghost，不作为 Primary

---

## 11. Window

- Radius：10
- Border：1 DIP
- TitleBar：46
- Content 与标题栏分区清楚
- 不在窗口内部再套一个同尺寸“外框 Card”
- ResizeGrip 只在可调整窗口出现
- 透明/Topmost 窗口优先保证点击穿透、焦点、首帧层级稳定，不为了阴影破坏 Win32 行为

---

## 12. Random Name Picker

### 信息层级

1. 姓名
2. 学号
3. 开始/停止
4. 组别
5. 名单/重置
6. 设置

### 建议布局

- Name Display 占主窗口高度约 55–65%
- 姓名 72–112 DIP，自适应
- 学号 28–36
- 分组按钮 Height 36
- 主按钮 Height 44
- “重置”使用 Warning
- “名单”使用 Secondary
- 当前分组使用 PrimarySoft

禁止：
- 大面积粒子动画
- 复杂渐变背景
- 姓名后面有强烈图案干扰

---

## 13. Countdown

- 数字 88–128 DIP
- 使用 tabular/稳定数字宽度
- 中心区域尽量留白
- 环形进度可选，但 Stroke ≤ 3–4 DIP
- 不能出现大量刻度
- Start/Pause 为 Primary
- Reset 为 Warning
- 设置为 Secondary/Ghost

运行状态：

- 未开始：Primary “开始”
- 运行中：Primary/Secondary “暂停”
- 暂停：Primary “继续”
- 结束：Warning/Primary 根据现有业务语义，不得改变原有逻辑

---

## 14. File / Image Manager

遵循 Windows Explorer 心智模型：

- 左侧 Navigation：180–220
- 顶部工具条：40–44
- Breadcrumb 明确
- Grid/List 切换统一
- Thumbnail 卡片不要过度圆角
- Selected：PrimarySoft + BorderFocus
- Hover：Hover
- 文件名最多 2 行，超长 Ellipsis
- 支持键盘选择时必须有 Focus
- 不把大量品牌色涂在文件区

---

## 15. Floating Annotation Toolbar

这是重点组件。

### 尺寸

- Toolbar Height：52
- Radius：16
- Outer Padding：6
- Drag Handle：24–28 宽
- Tool Slot：40×40
- 核心 Pen/Eraser：44×40 可选

### 视觉

Default：

- Background：OverlayToolbar
- Border：BorderDefault
- Icon：TextSecondary

Hover：

- Background：Hover
- Icon：TextPrimary

Active：

- Background：PrimarySoft
- Icon：Primary
- 下方 2 DIP active indicator，可选
- 禁止持续 glow

Danger：

- Trash 使用 Danger icon
- Hover 才出现 DangerSoft

### 分组

建议：

1. 拖动 / 指针
2. 画笔 / 荧光笔 / 橡皮
3. 形状 / 文本
4. 撤销 / 重做
5. 删除 / 截图 / 图像
6. 更多 / 设置

组间 1 DIP Separator。

### 溢出

当空间不足：

- 保留最常用工具
- 低频工具进入 `…`
- 不缩小触控区域到 < 36

---

## 16. Launcher

- 避免“Dashboard 感过强”
- 核心入口：点名、计时
- 画笔工具可作为横向强调项
- 设置/文件/更多为次级
- 入口卡片可使用 Hero Icon
- 不使用多个不同渐变背景
- 默认窗口应紧凑、置顶但低干扰

---

## 17. 白板/图片/PDF/PPT/WPS 叠加

UI 必须尽量退后：

- 浮层深色、半透明但不依赖 Acrylic/Mica
- 任何背景下保证边界可见
- 激活工具颜色明确
- 禁止大面积遮盖课件
- 图片/PDF 控制按钮与批注工具风格一致
- PPT/WPS 模式下不可改变原有 hook/焦点/穿透行为

---

## 18. ScrollBar

- 默认宽度 8
- Hover 10–12
- Thumb：TextTertiary/BorderDefault
- Track 尽量透明
- 不使用粗大传统滚动条
- 触控场景必须保持可滚动区域足够

---

## 19. Empty / Error / Warning 状态

Empty：

- 线性图标 32
- 标题 16
- 描述 13
- 仅 1 个 Primary CTA

Error：

- Danger icon
- 错误信息清晰
- 技术详情放“查看详情”
- 不用整页红色

Warning：

- WarningSoft
- 可恢复行为说明清楚
