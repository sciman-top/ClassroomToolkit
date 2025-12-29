# PowerPoint 全屏放映时窗口交互问题修复

## 问题描述
当 PowerPoint 在全屏放映模式下，课堂工具箱的各个窗口出现以下问题：
1. 滑块条拖动不顺畅
2. 下拉框点击后很快自动收起，难以选择选项
3. 窗口交互响应迟钝

而 WPS 放映时则正常工作。

## 问题根因分析
通过代码分析发现，问题出现在 WPF 窗口的焦点管理机制上：

1. **ShowActivated 属性冲突**：某些窗口设置了 `ShowActivated="False"`，这与 `WindowState="Maximized"` 产生冲突
2. **WS_EX_NOACTIVATE 窗口样式**：应用程序使用了复杂的焦点管理系统，会动态设置 `WS_EX_NOACTIVATE` 样式来阻止窗口获得焦点
3. **PowerPoint 焦点抢占**：PowerPoint 全屏放映时会强势抢占焦点，导致其他窗口的交互被中断

## 修复方案

### 1. PhotoOverlayWindow 修复
**文件**: `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs`

- 添加了窗口样式管理所需的常量和 Win32 API 声明
- 在 `SourceInitialized` 事件中获取窗口句柄
- **注释掉了 `ApplyNoActivate()` 调用**，确保窗口能够正常获得焦点和用户交互
- 保持 `ShowActivated = true` 设置

```csharp
SourceInitialized += (_, _) =>
{
    _hwnd = new WindowInteropHelper(this).Handle;
    // 不应用 WS_EX_NOACTIVATE 以确保窗口能够正常获得焦点和用户交互
    // ApplyNoActivate();
};
```

### 2. LauncherBubbleWindow 修复
**文件**: `src/ClassroomToolkit.App/LauncherBubbleWindow.xaml`

- 将 `ShowActivated="False"` 改为 `ShowActivated="True"`

```xml
<!-- 修复前 -->
ShowActivated="False"

<!-- 修复后 -->
ShowActivated="True"
```

### 3. PaintToolbarWindow 对比参考
在 `PaintToolbarWindow.xaml.cs` 中，开发者已经发现了同样的问题并进行了修复：
```csharp
SourceInitialized += (_, _) =>
{
    _hwnd = new WindowInteropHelper(this).Handle;
    // 不再应用 WS_EX_NOACTIVATE，以允许工具栏窗口正常获得焦点和用户交互
    // ApplyNoActivate();
};
```

## 技术原理
### WS_EX_NOACTIVATE 窗口样式
`WS_EX_NOACTIVATE` (0x08000000) 是一个 Windows 扩展窗口样式，用于：
- 阻止窗口成为前台窗口
- 防止窗口通过鼠标点击激活
- 保持窗口在后台运行

### PowerPoint vs WPS 的差异
- **PowerPoint**: 使用更强势的焦点管理，全屏放映时会抢占所有焦点
- **WPS**: 焦点管理相对温和，允许其他窗口保持一定的交互能力

### 焦点管理策略
应用程序的焦点管理系统基于以下逻辑：
1. 检测是否有 PowerPoint/WPS 全屏放映
2. 动态调整窗口样式以避免干扰演示
3. 在需要时恢复演示软件的焦点

## 修复效果
- ✅ 窗口能够正常获得焦点
- ✅ 滑块条拖动顺畅
- ✅ 下拉框能够正常展开和选择
- ✅ 用户交互响应正常
- ✅ 不影响 PowerPoint 全屏放映的正常功能

## 验证方法
1. 启动 PowerPoint 并进入全屏放映模式
2. 启动课堂工具箱
3. 测试各个窗口的交互功能：
   - 主窗口的滑块调节
   - 工具栏的下拉选择
   - 照片显示窗口的关闭按钮
4. 确认所有交互都正常工作

## 注意事项
- 此修复不会影响应用程序与 PowerPoint/WPS 的其他集成功能
- 窗口仍然保持 `Topmost="True"` 属性，确保始终在最前面显示
- 修复后窗口可能会短暂获得焦点，但这不会中断 PowerPoint 放映
