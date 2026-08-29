# Implementation Decisions — ClassroomToolkit UI 2.0

这是给开发者/Agent 的短版“不可争议决策”。

## 已定

- 默认主题：MidnightTeal
- 可选主题：Blackboard、Light
- 暂不首发 Aurora/蓝紫
- UI 主题与白板背景分离
- 主题切换运行时生效
- 默认不跟随 Windows
- 不引入第三方 UI Framework
- 不引入字体文件
- 使用 Semantic Token
- Theme color 使用 DynamicResource
- Metrics/Typography 使用统一资源
- 所有窗口共用控件样式
- Warning 和 Danger 分开
- Reset 使用 Warning，不使用 Danger
- Icon 使用 Fluent/统一 SVG Path 语言
- Toolbar 为重点组件
- 点名姓名和 Timer 数字是视觉主角
- 文件管理器遵循 Explorer 心智模型
- Dialog 减少 Card 套 Card
- 触控目标 ≥ 40 DIP
- Primary main action 建议 44 DIP
- 不使用持续 glow
- 不允许主题切换破坏运行状态
- 不改变现有数据兼容与安全写入策略
- 不改变 PPT/WPS/Overlay/Topmost/Click-through 行为

## 默认语义

```text
Primary = 青绿
Info = 蓝
Warning = 琥珀
Danger = 红
Neutral = 灰
```

三主题只替换 token，不改变语义。

## 允许例外

以下颜色可以不使用主题 token：

- 用户选择的画笔颜色
- 文档/图片内容颜色
- 业务数据自身颜色
- 系统必须要求的 Windows 颜色

例外必须注释说明。
