# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 输出语言策略

- 所有分析、代码解释、文档**必须使用中文**
- 技术术语保持英文（如 WPF, MVVM, Win32, P/Invoke）
- 代码注释可使用中文

## 项目概述

**ClassroomToolkit** 是一个基于 .NET 8.0 的 WPF 教学辅助工具，主要功能包括：
- 随机点名系统（支持分组、照片显示）
- 计时器/秒表（倒计时、秒表、时钟模式）
- 屏幕画笔（全屏标注、形状绘制、白板模式）
- 演示文稿控制（PowerPoint/WPS 翻页）

## 构建和开发命令

```bash
# 构建解决方案（需要在 Windows 环境下）
dotnet build ClassroomToolkit.sln

# 运行应用
dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj

# 运行测试
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj

# 恢复依赖
dotnet restore
```

**重要**：这是一个 WPF 应用程序，必须在 Windows 环境下构建和运行。

## 架构总览

项目采用**分层架构**，遵循依赖倒置原则：

```
ClassroomToolkit.App (UI 层)
    ↓ 依赖
ClassroomToolkit.Services (应用服务层)
    ↓ 依赖
ClassroomToolkit.Domain (领域层)
    ↓ 依赖
ClassroomToolkit.Interop (互操作层)
ClassroomToolkit.Infra (基础设施层)
```

### 各层职责

#### `ClassroomToolkit.App` - UI 层
- **技术栈**：WPF + Windows Forms
- **模式**：MVVM（ViewModel 在 `ViewModels/`，View 为 XAML）
- **关键窗口**：
  - `MainWindow` - 主窗口，管理其他窗口的生命周期
  - `PaintOverlayWindow` - 全屏透明覆盖窗口，用于绘图
  - `PaintToolbarWindow` - 浮动工具栏窗口
  - `RollCallWindow` - 点名和计时器窗口
  - `LauncherBubbleWindow` - 最小化状态的启动气泡

#### `ClassroomToolkit.Domain` - 领域层
- **职责**：核心业务逻辑，无外部依赖
- **核心组件**：
  - `RollCallEngine` - 随机点名算法、状态管理
  - `TimerEngine` - 计时器逻辑（倒计时、秒表、时钟）
  - `StudentWorkbook` / `ClassRoster` - 学生数据模型
  - `IdentityUtils` - 学号/姓名文本规范化

#### `ClassroomToolkit.Services` - 应用服务层
- **职责**：协调领域层和互操作层
- **核心组件**：
  - `PresentationControlService` - 演示文稿控制服务
  - `PresentationControlPlanner` - 控制策略规划
  - `PresentationCommandMapper` - 命令映射

#### `ClassroomToolkit.Interop` - 互操作层
- **职责**：Win32 API 调用、外部进程通信
- **核心组件**：
  - `PresentationClassifier` - 识别 PowerPoint/WPS 窗口
  - `Win32InputSender` - 发送键盘/鼠标输入
  - `WpsSlideshowNavigationHook` - WPS 全局钩子
  - `Win32PresentationResolver` - 查找演示窗口

#### `ClassroomToolkit.Infra` - 基础设施层
- **职责**：数据持久化、外部系统集成
- **核心组件**：
  - `StudentWorkbookStore` - Excel 工作簿管理（使用 ClosedXML）
  - `SettingsRepository` - 设置持久化（INI 格式）
  - `SettingsMigrator` - 设置版本迁移

## 核心系统架构

### 1. 窗口管理系统

**多窗口协调模式**：
- 所有窗口均为 `Topmost="True"`（始终置顶）
- 无标题栏窗口使用 `WindowStyle="None"` + 自定义标题栏
- 支持透明度 `AllowsTransparency="True"`
- 窗口位置持久化在 `WindowPlacementHelper`

**焦点管理**（重要）：
- `PaintOverlayWindow` 使用 `WS_EX_NOACTIVATE` 和 `WS_EX_TRANSPARENT` 样式实现点击穿透
- 动态管理焦点接受状态，根据演示文稿存在与否调整
- 焦点恢复定时器每 500ms 检查是否需要将焦点归还给演示窗口
- `PaintToolbarWindow` 需要 `WS_EX_NOACTIVATE` 被移除以允许用户交互

### 2. 画笔系统架构

**双窗口协调**：
```
PaintToolbarWindow (工具栏)
    ↓ 事件通信
PaintOverlayWindow (覆盖层)
    ↓ 调用
PaintBrushRenderer (笔刷渲染器)
```

**关键特性**：
- **输入穿透**：在 `Cursor` 模式下，覆盖窗口允许点击穿透到底层应用
- **白板模式**：半透明背景覆盖屏幕
- **形状绘制**：支持直线、矩形、椭圆、虚线等
- **区域擦除**：框选区域批量擦除
- **历史记录**：最多 30 步撤销

**焦点管理注意事项**：
- 当检测到 PowerPoint/WPS 全屏演示时，覆盖窗口会阻止焦点
- 工具栏窗口需要保持可获得焦点以支持滑块和下拉框交互
- 光标模式会尝试将焦点归还给演示窗口

### 3. 演示文稿控制系统

**三层策略模式**：
1. **识别层** (`PresentationClassifier`)：通过窗口类名识别 PowerPoint/WPS
2. **规划层** (`PresentationControlPlanner`)：选择最佳输入策略
3. **执行层** (`Win32InputSender` / `WpsSlideshowNavigationHook`)：发送输入

**支持的应用**：
- **Microsoft PowerPoint**：窗口类名包含 `PPTViewWndClass`、`screenClass` 等
- **WPS 演示**：窗口类名包含 `KWPPShowFrameClass` 等

**输入策略**：
- `Raw` - 直接输入（适用于前台演示窗口）
- `Message` - Windows 消息投递（更可靠但速度较慢）
- `Auto` - 自动选择（默认）

**WPS 特殊处理**：
- 使用全局钩子 `WpsSlideshowNavigationHook` 拦截键盘/滚轮事件
- 支持防抖（200ms）和事件去重

### 4. 点名系统架构

**数据流**：
```
students.xlsx (Excel)
    ↓ StudentWorkbookStore
StudentWorkbook / ClassRoster
    ↓ RollCallEngine
RollCallViewModel (UI)
```

**RollCallEngine 核心算法**：
- 基于分组的学生池管理
- 随机抽取算法（基于 Fisher-Yates shuffle）
- 状态持久化（每个班级独立状态）
- 支持重置和分组切换

**TimerEngine 特性**：
- 三种模式：Countdown（倒计时）、Stopwatch（秒表）、Clock（时钟）
- 提醒功能：可配置间隔提醒
- 声音通知：多种声音变体（bell、digital、buzz 等）

## 开发规范

### 命名约定
- **类/方法**：`PascalCase`
- **字段/局部变量**：`camelCase`
- **常量**：`UPPER_SNAKE_CASE` 或 `PascalCase`（私有常量）
- **事件**：`PascalCase`，使用 `Action` 或 `EventHandler<T>`

### MVVM 模式
- ViewModel 位于 `ViewModels/` 目录
- 业务逻辑应在 ViewModel 中，而非 code-behind
- 使用 `RelayCommand` 实现命令绑定
- 使用 `INotifyPropertyChanged` 通知属性变化

### 资源管理
- XAML 样式在 `Assets/Styles/` 中集中管理
- 颜色：`Assets/Styles/Colors.xaml`
- 组件样式：`Assets/Styles/WidgetStyles.xaml`
- 图标：`Assets/Styles/Icons.xaml`

### 错误处理
- 使用 try-catch 捕获异常，避免崩溃
- 关键操作失败时显示 MessageBox 提示
- 外部集成（如 Excel、语音）需要降级处理

### 窗口样式规范
```csharp
// 典型无边框窗口设置
WindowStyle = "None"
AllowsTransparency = "True"
Background = "Transparent"
Topmost = "True"
ResizeMode = "CanResize" 或 "NoResize"
```

### Win32 互操作注意事项
- P/Invoke 声明在需要使用的类中
- 窗口样式常量使用 `const int` 定义
- 使用 `WindowInteropHelper` 获取 HWND
- 注意 `WS_EX_NOACTIVATE` 和 `WS_EX_TRANSPARENT` 的组合使用

## 重要配置文件

### 运行时数据
- `students.xlsx` - 学生数据（Excel 格式）
- `student_photos/` - 学生照片目录
- `settings.ini` - 应用设置（自动生成）

### 关键设置项
- `RollCallCurrentClass` - 当前班级
- `PaintToolbarScale` - 工具栏缩放比例
- `BrushSize` / `EraserSize` - 笔刷/橡皮大小
- `AllowOffice` / `AllowWps` - 是否允许控制演示文稿

## 常见开发场景

### 添加新的画笔模式
1. 在 `PaintToolMode` 枚举中添加新模式
2. 在 `PaintOverlayWindow.xaml.cs` 中添加处理逻辑
3. 更新 `UpdateInputPassthrough()` 和 `UpdateFocusAcceptance()`
4. 在工具栏 UI 中添加对应按钮

### 添加新的演示文稿支持
1. 在 `PresentationClassifier` 中添加窗口类名识别
2. 在 `Win32PresentationResolver` 中添加查找逻辑
3. 更新 `PresentationControlService` 的策略映射
4. 测试翻页和焦点管理

### 修改窗口焦点行为
- 检查 `ApplyWindowStyles()` 中的样式逻辑
- 确认 `ShouldBlockFocus()` 的条件判断
- 注意 `WS_EX_NOACTIVATE` 会阻止窗口获得焦点
- 测试与演示文稿的交互

### 添加新的设置项
1. 在 `AppSettings.cs` 中添加属性
2. 在 `AppSettingsService.cs` 中添加映射逻辑
3. 在 UI 中添加绑定和保存逻辑
4. 考虑是否需要设置迁移（`SettingsMigrator`）

## 调试技巧

### 焦点问题调试
- 使用 `Spy++` 或类似工具查看窗口样式
- 检查 `GetForegroundWindow()` 返回的窗口句柄
- 确认 `WS_EX_NOACTIVATE` 是否被正确应用/移除

### 演示文稿控制调试
- 启用诊断日志查看窗口识别结果
- 检查 `PresentationClassifier` 的分类输出
- 验证 `Win32InputSender` 的消息发送

### 性能优化
- 使用 `WriteableBitmap` 进行高性能绘图
- 避免在 `MouseMove` 等高频事件中进行重量操作
- 使用 `Freeze()` 冻结不变的 Brush 和 Pen 对象
