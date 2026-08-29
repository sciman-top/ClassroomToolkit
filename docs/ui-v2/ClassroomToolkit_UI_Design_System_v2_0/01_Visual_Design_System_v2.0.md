# ClassroomToolkit Visual Design System 2.0

## 1. 产品视觉定位

关键词：

> 专业教学 / 低干扰 / 高辨识 / 触控友好 / Windows 原生感 / 克制现代 / 长时间舒适

默认视觉基线：

> **Midnight Teal：深石墨 + 青绿品牌色 + 琥珀操作语义**

避免以下方向：

- Gaming / RGB / 赛博朋克
- 大量霓虹发光
- 大面积玻璃模糊
- 手机 App 式超大圆角和卡片堆叠
- 每个功能使用不同风格的图标
- 把“好看”置于课堂读屏和触控效率之上

---

## 2. 设计优先级

1. 课堂辨识度
2. 操作效率
3. 投影和大屏可读性
4. 触控命中率
5. 长时间视觉舒适
6. Windows 10/11 一致性
7. 品牌感
8. 装饰性

---

## 3. 主题策略

### 3.1 默认：MidnightTeal / 课堂深色（推荐）

用途：

- 默认安装
- GitHub 截图
- README
- 课堂投影
- 悬浮工具
- PPT/WPS 叠加
- 图片/PDF 批注

风格：

- 深蓝黑/石墨背景
- 青绿色为 Primary
- 琥珀色表示重置/提醒
- 红色只用于危险/删除
- 少量蓝色用于信息状态

### 3.2 Blackboard / 黑板护眼

用途：

- 电子黑板
- 暗教室
- 长时间板书
- 偏好低刺激配色的教师

要求：

- 现代 Fluent 结构
- 不做仿木框/粉笔字体/复古黑板纹理
- 只改变色彩气质，不改变布局和组件尺寸

### 3.3 Light / 明亮专业

用途：

- 白天办公室
- 备课
- 明亮环境
- 文件/图片管理

要求：

- 白色/浅灰 Surface
- 青绿 Primary
- 保持足够边界和阴影
- 悬浮工具在白色 PPT 上仍需明显可见

---

## 4. 色彩语义

所有颜色必须按“语义”命名，禁止按视觉色名直接引用。

### 必备语义

- Canvas
- Window
- Surface
- SurfaceAlt
- SurfaceElevated
- Hover
- Pressed
- BorderSubtle
- BorderDefault
- BorderFocus
- TextPrimary
- TextSecondary
- TextTertiary
- TextDisabled
- Primary
- PrimaryHover
- PrimaryPressed
- PrimarySoft
- Info
- Success
- Warning
- Danger
- Selection
- Overlay
- ToolActive
- ToolActiveSoft

### 颜色语义约定

| 颜色语义 | 用途 |
|---|---|
| Primary / 青绿 | 主操作、当前选择、重点状态 |
| Info / 蓝 | 信息、普通提示 |
| Warning / 琥珀 | 重置、注意、可恢复动作 |
| Danger / 红 | 删除、清空、危险、错误 |
| Success / 绿 | 成功、已连接、已完成 |
| 中性灰 | 普通按钮、次级信息、未选中工具 |

**主题切换时语义不变。**

---

## 5. 对比度要求

- 普通正文：至少 4.5:1
- 大字号文本（≥ 24 DIP 或粗体 ≥ 18 DIP）：至少 3:1
- 图标/边框/焦点态：至少 3:1
- Disabled 可以低于以上值，但必须仍可识别为“存在但不可操作”
- 投影环境下，禁止使用只依赖细微灰度差异的状态变化

---

## 6. 字体系统

### 6.1 字体族

Windows 中文 UI：

```text
Microsoft YaHei UI
Segoe UI Variable
Segoe UI
Microsoft YaHei
sans-serif
```

不引入额外字体文件，不增加部署依赖。

数字/计时优先：

```text
Segoe UI Variable Display
Segoe UI
```

### 6.2 字重

| 用途 | FontWeight |
|---|---|
| 学生姓名 | 700 |
| 大计时数字 | 600–700 |
| Window Title | 600 |
| Dialog Title | 600 |
| Section Title | 600 |
| Button | 500 |
| Tab | 500 |
| Body | 400 |
| Helper / Caption | 400 |

禁止全局 SemiBold/Bold。

### 6.3 字号（DIP）

| Token | 建议值 | 用途 |
|---|---:|---|
| Font.Caption | 12 | 辅助说明 |
| Font.BodyCompact | 13 | 紧凑信息 |
| Font.Body | 14 | 默认正文 |
| Font.Button | 14 | 普通按钮 |
| Font.ButtonLarge | 15 | 主按钮 |
| Font.Section | 16 | Section |
| Font.DialogTitle | 18 | 对话框标题 |
| Font.WindowTitle | 18 | 窗口标题 |
| Font.HeroSmall | 28 | 次级大数字 |
| Font.StudentName | 72–112 | 点名姓名，自适应 |
| Font.Timer | 88–128 | 计时，自适应 |

姓名与计时必须根据窗口大小自适应，不允许固定大字号导致 DPI/缩放截断。

---

## 7. 间距系统

采用 4 DIP 基础网格：

```text
4 / 8 / 12 / 16 / 20 / 24 / 32
```

推荐：

- 控件内部水平 Padding：12–16
- 普通按钮：12×8
- 大按钮：20×10
- Section 间距：20–24
- 同组控件间距：8–12
- Dialog 外边距：20–24
- Toolbar icon gap：4
- 标题与内容：16

禁止“靠画框建立层级”。优先使用：

- 留白
- 标题
- 字重
- 分割线
- Surface 轻微变化

---

## 8. 圆角

| Token | Radius |
|---|---:|
| Radius.Small | 4 |
| Radius.Control | 6 |
| Radius.Button | 7 |
| Radius.Panel | 8 |
| Radius.Window | 10 |
| Radius.Dialog | 10 |
| Radius.Large | 12 |
| Radius.Toolbar | 16 |
| Radius.Pill | 999 |

注意：不要所有元素都 16–24 px 圆角，否则会产生移动端风格。

---

## 9. 阴影与层级

### 原则

- 阴影用于区分“层级”，不是装饰
- 透明悬浮窗口、批注工具、频繁重绘区域避免高成本 DropShadowEffect
- 优先使用 Border + 轻量外阴影
- 不使用持续动态发光

### 建议层级

- Level 0：Canvas，无阴影
- Level 1：Panel，0–2 DIP 轻阴影
- Level 2：Window/Dialog，4–12 DIP 柔和阴影
- Level 3：Popup/ContextMenu/Tooltip，6–16 DIP 阴影
- Overlay Toolbar：深色背景 + 1 DIP 边框，可无实时模糊

---

## 10. 动效

允许：

- Hover：120–160 ms
- Press：80–120 ms
- Tab/Selection：120–180 ms
- Dialog Fade/Scale：150–180 ms
- Tool active indicator：120 ms

禁止：

- 无限发光动画
- 频繁粒子
- 长时间呼吸灯
- 影响课堂注意力的装饰动画
- 对点名姓名和计时数字做复杂位移动画

“点名滚动/抽取动画”应优先保证停止位置明确、不卡顿、不延迟实际结果。

---

## 11. 窗口 Chrome

统一要求：

- TitleBar 高度：44–48 DIP
- 左侧：图标 + 标题
- 右侧：功能按钮（如设置）与系统按钮分组
- Minimize/Close 命中区域 ≥ 36×36
- Close Hover 可使用 DangerSoft
- 标题栏不使用重渐变
- 所有窗口边框、标题栏高度、系统按钮风格必须一致

---

## 12. 触控规范

ClassroomToolkit 支持触屏/手写屏/数位板，因此：

- 主要按钮最小 44×40
- IconButton 最小可点击区域 40×40
- 悬浮 Toolbar 工具建议 40×40，核心工具 44×44
- ComboBox/Tab/Toggle 高度 ≥ 36
- Slider 可视轨道 4 DIP，但交互命中区 ≥ 28–32
- Checkbox 命中区不只包含方框，必须包含文字区域
- 不依赖 Hover 才能发现关键功能

---

## 13. 核心页面视觉优先级

### 随机点名

页面 70–80% 的视觉权重给：

1. 学生姓名
2. 学号
3. 开始/停止

其他组别、名单、设置必须弱化。

### 倒计时

页面 70–80% 的视觉权重给：

1. 时间
2. 开始/暂停
3. 重置/重新设置

装饰圆环必须极简，不得抢占主数字。

### 文件/图片管理

优先遵循 Windows Explorer 的已知模式，不追求强品牌化。

### 设置

优先信息清晰，不做 Card 套 Card。靠 Section、间距和分割线分组。

### 悬浮工具栏

必须是全软件视觉与交互质量最高的组件之一：

- 在白色 PPT、黑色图片、绿色白板上都可辨认
- 轻量
- 不遮内容
- 触控准确
- 工具状态一眼可见
