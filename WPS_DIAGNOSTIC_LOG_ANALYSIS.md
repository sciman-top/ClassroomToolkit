# WPS 诊断日志分析

## 📊 诊断日志信息

### 前台窗口（WPS）
```
当前前台窗口句柄: 0x00470B82
前台窗口标题: WPS Presentation Slide Show - [新建 PPTX 演示文稿 (2).pptx]
前台窗口进程ID: 20728
前台窗口扩展样式: 0x00000000
前台窗口 WS_EX_NOACTIVATE: False
前台窗口 WS_EX_TRANSPARENT: False
是否为演示软件窗口: True
```

### 应用程序窗口（MainWindow - 启动器）
```
窗口句柄: 0x00341464
窗口标题: Classroom Toolkit
窗口可见: True
窗口激活: True
窗口焦点: False
窗口状态: Normal
ShowActivated: False
Focusable: False
窗口扩展样式: 0x08080008
窗口 WS_EX_NOACTIVATE: True  ← 问题所在
窗口 WS_EX_TRANSPARENT: False
```

---

## 🔍 关键发现

### 1. WPS 窗口状态正常
- ✅ 前台窗口是 WPS
- ✅ 没有设置 WS_EX_NOACTIVATE
- ✅ 没有设置 WS_EX_TRANSPARENT
- ✅ 扩展样式为 0x00000000（正常）

### 2. MainWindow 设置了 WS_EX_NOACTIVATE
- ⚠️ 启动器窗口设置了 WS_EX_NOACTIVATE
- ⚠️ 窗口激活：True，但焦点：False
- ⚠️ Focusable：False

### 3. 缺少关键信息
- ❓ **没有 PaintOverlayWindow 的诊断信息**
- ❓ **没有 PaintToolbarWindow 的诊断信息**
- ❓ 不知道覆盖窗口的当前状态

---

## 🎯 问题分析

### MainWindow 的 WS_EX_NOACTIVATE

**代码位置**：`src/ClassroomToolkit.App/MainWindow.xaml.cs:830-838`

```csharp
private void ApplyNoActivate()
{
    var hwnd = new WindowInteropHelper(this).Handle;
    if (hwnd != IntPtr.Zero)
    {
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);
    }
}
```

**调用位置**：`MainWindow` 构造函数的 `SourceInitialized` 事件

```csharp
SourceInitialized += (_, _) =>
{
    var hwnd = new WindowInteropHelper(this).Handle;
    
    // 应用 NoActivate 样式防止焦点问题
    ApplyNoActivate();
};
```

**目的**：防止启动器窗口在点击时获得焦点，避免干扰演示文稿。

**影响**：
- 启动器窗口不会获得焦点
- 这是**正常的**，不应该影响 WPS 的输入

### 真正需要诊断的窗口

**关键问题**：诊断日志中**缺少覆盖窗口（PaintOverlayWindow）的信息**！

我们需要知道：
1. PaintOverlayWindow 是否可见？
2. PaintOverlayWindow 的扩展样式是什么？
3. PaintOverlayWindow 是否设置了 WS_EX_NOACTIVATE？
4. 当前是什么模式（Cursor/Brush/Eraser）？

---

## 🔧 诊断建议

### 需要添加的诊断信息

在诊断按钮的代码中，需要添加对以下窗口的诊断：

1. **PaintOverlayWindow**（绘图覆盖窗口）
   - 窗口句柄
   - 窗口可见性
   - 扩展样式
   - WS_EX_NOACTIVATE 状态
   - WS_EX_TRANSPARENT 状态
   - 当前模式（_mode 字段）
   - _focusBlocked 字段值
   - _inputPassthroughEnabled 字段值

2. **PaintToolbarWindow**（绘图工具条窗口）
   - 窗口句柄
   - 窗口可见性
   - 扩展样式
   - WS_EX_NOACTIVATE 状态
   - 当前模式（_currentMode 字段）

### 诊断时机

**重要**：诊断应该在以下时机进行：

1. **拖动工具条后**
2. **切换到光标模式后**
3. **键盘失效时**

这样才能捕获到问题发生时的窗口状态。

---

## 💡 初步判断

基于当前的诊断信息：

### 可能的情况 1：覆盖窗口未显示
- 如果 PaintOverlayWindow 没有显示，说明绘图模式未启动
- 这种情况下不应该有键盘失效问题

### 可能的情况 2：覆盖窗口设置了 WS_EX_NOACTIVATE
- 如果 PaintOverlayWindow 设置了 WS_EX_NOACTIVATE
- 即使我们修复了 `ShouldBlockFocus()`，可能还有其他地方设置了这个样式
- 需要检查覆盖窗口的实际状态

### 可能的情况 3：修复未生效
- 如果代码修改后没有重新编译
- 或者运行的是旧版本
- 修复不会生效

---

## 🧪 验证步骤

### 步骤 1：确认运行的是修复后的版本

```bash
# 重新构建
dotnet build ClassroomToolkit.sln

# 运行
dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj
```

### 步骤 2：在问题发生时进行诊断

1. 启动 WPS 全屏放映
2. 启动绘图模式
3. 拖动工具条
4. 切换到光标模式
5. **立即按诊断快捷键**（在键盘失效时）
6. 查看诊断日志

### 步骤 3：检查覆盖窗口状态

在诊断日志中查找：
- `PaintOverlayWindow` 的信息
- 扩展样式值
- WS_EX_NOACTIVATE 状态
- 当前模式

---

## 📝 需要补充的诊断代码

### 在 MainWindow 中添加覆盖窗口诊断

```csharp
private void OnMainWindowKeyDown(object sender, KeyEventArgs e)
{
    if (e.Key == Key.D && 
        Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
    {
        var diagnostics = new System.Text.StringBuilder();
        
        // ... 现有诊断代码 ...
        
        // 添加覆盖窗口诊断
        if (_overlayWindow != null)
        {
            diagnostics.AppendLine("\n--- 窗口诊断: PaintOverlayWindow ---");
            var hwnd = new WindowInteropHelper(_overlayWindow).Handle;
            diagnostics.AppendLine($"窗口句柄: 0x{hwnd:X8}");
            diagnostics.AppendLine($"窗口可见: {_overlayWindow.IsVisible}");
            diagnostics.AppendLine($"窗口激活: {_overlayWindow.IsActive}");
            
            if (hwnd != IntPtr.Zero)
            {
                var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                diagnostics.AppendLine($"窗口扩展样式: 0x{exStyle:X8}");
                diagnostics.AppendLine($"窗口 WS_EX_NOACTIVATE: {(exStyle & WS_EX_NOACTIVATE) != 0}");
                diagnostics.AppendLine($"窗口 WS_EX_TRANSPARENT: {(exStyle & 0x20) != 0}");
            }
            
            // 需要通过反射或添加公共属性来获取这些信息
            // diagnostics.AppendLine($"当前模式: {_overlayWindow.CurrentMode}");
            // diagnostics.AppendLine($"焦点阻止: {_overlayWindow.FocusBlocked}");
        }
        else
        {
            diagnostics.AppendLine("\n--- PaintOverlayWindow 未创建 ---");
        }
        
        // 添加工具条窗口诊断
        if (_toolbarWindow != null)
        {
            diagnostics.AppendLine("\n--- 窗口诊断: PaintToolbarWindow ---");
            var hwnd = new WindowInteropHelper(_toolbarWindow).Handle;
            diagnostics.AppendLine($"窗口句柄: 0x{hwnd:X8}");
            diagnostics.AppendLine($"窗口可见: {_toolbarWindow.IsVisible}");
            
            if (hwnd != IntPtr.Zero)
            {
                var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                diagnostics.AppendLine($"窗口扩展样式: 0x{exStyle:X8}");
                diagnostics.AppendLine($"窗口 WS_EX_NOACTIVATE: {(exStyle & WS_EX_NOACTIVATE) != 0}");
                diagnostics.AppendLine($"窗口 WS_EX_TRANSPARENT: {(exStyle & 0x20) != 0}");
            }
        }
        else
        {
            diagnostics.AppendLine("\n--- PaintToolbarWindow 未创建 ---");
        }
        
        MessageBox.Show(diagnostics.ToString(), "诊断信息", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
```

---

## 🎯 下一步行动

### 立即执行

1. **确认运行的是修复后的版本**
   - 重新构建项目
   - 确认修改已编译

2. **在问题发生时进行诊断**
   - 拖动工具条后
   - 切换到光标模式
   - 键盘失效时立即按诊断快捷键

3. **查看覆盖窗口状态**
   - 检查 PaintOverlayWindow 的扩展样式
   - 确认 WS_EX_NOACTIVATE 状态
   - 确认当前模式

### 如果覆盖窗口仍然有 WS_EX_NOACTIVATE

说明我们的修复可能有问题，需要：
1. 检查 `ShouldBlockFocus()` 的返回值
2. 检查 `ApplyWindowStyles()` 的执行
3. 添加调试日志跟踪状态变化

### 如果覆盖窗口没有 WS_EX_NOACTIVATE

说明修复生效了，但仍然有问题，需要：
1. 检查其他可能阻止输入的因素
2. 检查 WPS 的焦点状态
3. 考虑其他解决方案

---

## 📌 总结

当前的诊断信息显示：
- ✅ WPS 窗口状态正常
- ✅ MainWindow 的 WS_EX_NOACTIVATE 是预期的
- ❓ **缺少 PaintOverlayWindow 的诊断信息**

**关键**：需要在问题发生时（键盘失效时）诊断覆盖窗口的状态，才能确定修复是否生效。

**建议**：
1. 重新构建并运行修复后的版本
2. 在键盘失效时立即进行诊断
3. 查看覆盖窗口的 WS_EX_NOACTIVATE 状态
4. 根据诊断结果决定下一步行动
