# UI Refactor Implementation Plan

## 总原则

可以让 Agent 在一个任务中完成完整 UI 改版，但执行必须分阶段。

禁止：

> 直接全仓替换颜色 + 全窗口改 XAML + 最后一起编译。

正确顺序：

> 审计 → 设计系统基础 → 公共控件 → 核心窗口 → 主题切换 → 全量迁移 → 验证 → 视觉复核。

---

# Phase 0：基线与审计

Agent 必须先输出/记录：

- WPF App 入口
- 当前 ResourceDictionary 列表
- 全局 Style/ControlTemplate
- 所有 Window/UserControl/Popup/ContextMenu
- 自绘 TitleBar
- Topmost / AllowsTransparency / WindowStyle 特殊窗口
- 画笔/白板/图片PDF/PPTWPS相关 Overlay
- 当前硬编码颜色、字号、CornerRadius、Margin
- 当前 icon 来源
- 当前主题/设置持久化路径

不得在完成审计前大规模编辑。

---

# Phase 1：建立 Design System Foundation

创建/整理：

- Metrics
- Typography
- Theme colors × 3
- Semantic brushes
- Shared component styles
- ThemeManager

先让一个简单非关键窗口验证主题切换。

---

# Phase 2：公共组件

优先迁移：

1. Button
2. IconButton
3. CheckBox
4. ComboBox
5. Slider
6. Tab
7. ContextMenu
8. Tooltip
9. Dialog footer
10. Window chrome

目标：

> Feature XAML 尽量只声明布局和语义 Style Key，不声明颜色和模板。

---

# Phase 3：核心课堂链路

按风险和价值顺序：

1. 悬浮启动器
2. 随机点名
3. 倒计时/正计时
4. 悬浮标注 Toolbar
5. 画笔设置
6. 点名设置
7. 图片/PDF 浏览与批注
8. 文件/图片管理
9. 白板
10. PPT/WPS 叠加和导航

---

# Phase 4：主题切换与设置

- 外观设置页
- 3 套主题
- 即时切换
- 持久化
- 默认 MidnightTeal

禁止让主题切换重新初始化课堂状态。

---

# Phase 5：清理硬编码

全仓扫描：

- `#RRGGBB`
- `Brushes.`
- `new SolidColorBrush`
- XAML 本地 `CornerRadius=`
- 重复 Button Template
- 重复 Tab Template
- 局部 FontFamily
- Emoji / 不统一 icon

允许保留的硬编码必须写注释说明理由，例如：

- 用户自由选择的画笔颜色
- 图片本身颜色
- 业务数据颜色
- Windows 系统颜色交互

---

# Phase 6：兼容性验证

UI 改版不得破坏：

- students.xlsx
- student_photos
- settings.ini / 现有配置兼容
- 窗口 Topmost
- 点击穿透
- 首帧层级
- 点名运行状态
- 计时状态
- 画笔撤销/重做
- 图片/PDF
- PPT/WPS hook
- 多窗口焦点行为
- 自定义 DPI
- 多显示器

---

# Phase 7：构建与自动化测试

至少：

```powershell
dotnet build ClassroomToolkit.sln -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "Gate!=CoreContract&Gate!=Performance"
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "Gate=CoreContract"
powershell -File scripts/quality/check-hotspot-line-budgets.ps1
```

如果 UI 改动触及性能、Overlay、画笔、窗口层级，则补跑相关 focused tests / full gates。

不要为了通过测试删除或弱化测试。

---

# Phase 8：人工视觉验收矩阵

至少检查：

### DPI
- 100%
- 125%
- 150%
- 200%

### 分辨率
- 1366×768
- 1920×1080
- 2560×1440
- 4K（缩放）

### 主题
- MidnightTeal
- Blackboard
- Light

### 背景
- 白色 PPT
- 深色 PPT
- 图片
- PDF
- 黑板
- 白板
- Windows 桌面

### 输入
- 鼠标
- 触控
- 笔

---

# Phase 9：最终交付

Agent 最终必须提供：

1. 改动摘要
2. Design System 文件路径
3. Theme 文件路径
4. 已迁移窗口清单
5. 尚存硬编码清单
6. 测试结果
7. 视觉验收建议
8. 可能的兼容风险
9. `git diff --stat`
10. 不得声称“已现场验证投影/多屏”，除非真的执行
