# Theme Architecture and Switching

## 1. 目标

实现：

- `MidnightTeal`（默认）
- `Blackboard`
- `Light`
- 运行时即时切换
- 主题偏好持久化
- 不改变任何业务逻辑
- 不改变白板背景语义
- 不改变颜色语义

---

## 2. 推荐目录

```text
src/ClassroomToolkit.App/
└─ UI/
   ├─ Themes/
   │  ├─ Metrics.xaml
   │  ├─ Typography.xaml
   │  ├─ Colors.MidnightTeal.xaml
   │  ├─ Colors.Blackboard.xaml
   │  ├─ Colors.Light.xaml
   │  ├─ Brushes.Semantic.xaml
   │  └─ ThemeResources.xaml
   │
   ├─ Styles/
   │  ├─ Buttons.xaml
   │  ├─ IconButtons.xaml
   │  ├─ Tabs.xaml
   │  ├─ Sliders.xaml
   │  ├─ CheckBoxes.xaml
   │  ├─ ComboBoxes.xaml
   │  ├─ Menus.xaml
   │  ├─ Tooltips.xaml
   │  ├─ Dialogs.xaml
   │  ├─ WindowChrome.xaml
   │  └─ FloatingToolbar.xaml
   │
   └─ Theming/
      ├─ AppTheme.cs
      ├─ ThemeManager.cs
      └─ ThemePreferenceService.cs
```

实际目录可按仓库现状调整，但必须保持“Theme / Component Style / Feature View”分层。

---

## 3. 资源命名

必须使用语义 key：

```text
CTK.Brush.Window
CTK.Brush.Surface
CTK.Brush.Text.Primary
CTK.Brush.Primary
CTK.Brush.Warning
CTK.Brush.Danger
CTK.Radius.Button
CTK.Size.Toolbar
```

禁止：

```text
TealBrush
DarkGrayBackground
GreenButton
OrangeResetBrush
```

因为这些名称会把主题写死。

---

## 4. DynamicResource vs StaticResource

### DynamicResource

用于运行时主题切换：

- Color
- Brush
- Border brush
- Theme-dependent shadow color
- Theme-dependent icon foreground

### StaticResource

用于跨主题不变的视觉指标：

- CornerRadius
- Thickness
- Font size
- Control height
- Icon size
- Spacing

---

## 5. ThemeManager 行为

伪代码：

```csharp
public enum AppTheme
{
    MidnightTeal,
    Blackboard,
    Light
}

public sealed class ThemeManager
{
    public AppTheme CurrentTheme { get; private set; }

    public void Apply(AppTheme theme)
    {
        // 1. 找到当前颜色字典
        // 2. 替换为目标颜色字典
        // 3. 保留 Metrics / Typography / Component styles
        // 4. 通过 DynamicResource 自动刷新
        // 5. 更新 CurrentTheme
    }
}
```

要求：

- 不重启
- 不重新创建业务窗口作为主题切换手段
- 不打断点名/计时状态
- 不影响 Topmost、Owner、ShowActivated、WindowStyle、AllowsTransparency 等窗口行为
- 不修改 PPT/WPS hook 状态

---

## 6. 持久化

推荐配置值：

```text
ui.theme = MidnightTeal
```

兼容策略：

- 缺失 → MidnightTeal
- 未知值 → MidnightTeal，并保留未知配置 section/key 的现有兼容原则
- 主题保存失败不得覆盖损坏配置文件
- 遵循项目既有 settings/json/ini 安全写入策略，不自行创建第二套配置系统

---

## 7. 设置 UI

建议：

```text
设置 → 外观

主题
[ ● 课堂深色（推荐） ]
[ ○ 黑板护眼       ]
[ ○ 明亮           ]

□ 跟随 Windows
```

第一版可以不提供“跟随 Windows”。

如果提供：

- 默认仍为 `MidnightTeal`
- 仅用户明确开启后跟随
- 系统主题变化时不得导致课堂中的悬浮工具突然不可见

---

## 8. 白板背景与应用主题分离

应用主题：

```text
MidnightTeal / Blackboard / Light
```

白板背景：

```text
Whiteboard / Black / DeepGreen / Custom
```

二者必须独立。

---

## 9. Theme Contract

任何新组件合入前必须满足：

1. 不包含硬编码 `#RRGGBB`
2. 不引用 `Teal/Green/Blue` 视觉色名资源
3. 所有状态颜色均来自 semantic tokens
4. 三套主题均可运行
5. High DPI 不截断
6. 键盘 Focus 可见
7. Disabled 与 Active 状态可辨认
