# ClassroomToolkit UI 2.0 — Independent Review & Repair Prompt

你现在不是第一轮实现者，而是第二轮 UI 审查者。

目标：检查现有 UI 2.0 是否真正遵守 Design System，而不是只“看起来差不多”。

必须阅读：

- 01_Visual_Design_System_v2.0.md
- 02_Component_Specification_v2.0.md
- 03_Theme_Tokens_v2.json
- 05_Theme_Architecture_and_Switching.md
- 07_UI_QA_Acceptance_Checklist.md

然后检查当前 git diff 和全部 UI 代码。

---

## 重点审查

### 1. 主题架构

- 是否真的有 semantic tokens
- 是否用 DynamicResource
- 是否仍有窗口私有颜色
- 是否三主题只是复制三份整套 Style
- ThemeManager 是否只替换主题相关字典
- 切换是否会重新创建业务状态

### 2. 组件一致性

逐项比较：

- Button
- Tab
- Slider
- ComboBox
- CheckBox
- Dialog
- Menu
- Tooltip
- Window chrome
- Toolbar

找出同类控件的高度、圆角、字体、Hover、Focus 不一致。

### 3. 硬编码

全仓搜索：

- HEX colors
- Brushes.
- SolidColorBrush
- local templates
- local font size
- local CornerRadius

分类：

- A. 应迁移
- B. 合理业务例外
- C. 暂不能迁移

只修 A。

### 4. 课堂场景

重点看：

- 点名姓名是否足够突出
- Timer 是否过度装饰
- Toolbar 是否过亮/过厚
- Light theme Toolbar 在白色 PPT 上是否有边界
- Blackboard 是否变成复古黑板
- 触控命中区是否 ≥ 40
- 200% DPI 是否存在截断风险

### 5. 行为回归

严禁因为 UI 清理破坏：

- Topmost
- AllowsTransparency
- click-through
- focus
- first-frame z-order
- PPT/WPS
- pen history
- timer state
- roll call state
- settings safe-write

---

## 修复原则

- 优先小范围修复
- 不重构无关业务
- 不为了完美改动高风险 Win32/Interop
- 不新增第三方 UI framework
- 不删除测试
- 不改用户数据格式

---

## 验证

完成后：

```powershell
dotnet build ClassroomToolkit.sln -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "Gate!=CoreContract&Gate!=Performance"
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "Gate=CoreContract"
powershell -File scripts/quality/check-hotspot-line-budgets.ps1
git diff --check
```

---

## 最终报告

输出：

- Design System 合规度
- 发现的问题
- 已修复问题
- 合理保留的 hardcode
- 三主题风险
- DPI/触控/投影人工验收建议
- build/test 结果
- diff stat
