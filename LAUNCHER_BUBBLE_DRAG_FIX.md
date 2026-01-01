# 悬浮球拖动卡顿问题修复

## 问题描述

拖动启动器缩小后的悬浮小球时，有时会出现卡顿，甚至卡住在屏幕中不动，无法靠边吸附。问题原因是焦点被抢走导致鼠标事件丢失。

## 根本原因

1. **鼠标事件丢失**：拖动过程中没有捕获鼠标，当鼠标快速移动或焦点被其他窗口抢走时，会丢失 MouseMove 和 MouseUp 事件
2. **边界检查不准确**：使用主屏幕尺寸而非当前屏幕工作区域，在多屏环境下可能导致位置计算错误
3. **焦点抢夺**：虽然设置了 `WS_EX_NOACTIVATE`，但在拖动过程中仍可能被其他窗口干扰

## 修复方案

### 修改文件：`src/ClassroomToolkit.App/LauncherBubbleWindow.xaml.cs`

#### 修复 1：添加拖动阈值

**添加字段**：
```csharp
private System.Windows.Point _dragStartPosition;
private const double DragThreshold = 5.0;  // 移动超过 5 像素才算拖动
```

**原因**：区分点击和拖动操作，避免微小的鼠标抖动被误判为拖动。

#### 修复 2：记录拖动起始位置

**修改前**：
```csharp
private void OnMouseDown(object sender, MouseButtonEventArgs e)
{
    if (e.ChangedButton != MouseButton.Left)
    {
        return;
    }
    _dragging = true;
    _moved = false;
    _dragOffset = e.GetPosition(this);
}
```

**修改后**：
```csharp
private void OnMouseDown(object sender, MouseButtonEventArgs e)
{
    if (e.ChangedButton != MouseButton.Left)
    {
        return;
    }
    _dragging = true;
    _moved = false;
    _dragOffset = e.GetPosition(this);
    _dragStartPosition = new System.Windows.Point(Left, Top);
    
    // 捕获鼠标，防止拖动时失去鼠标事件
    CaptureMouse();
}
```

**原因**：
- 记录窗口的起始位置，用于计算移动距离
- `CaptureMouse()` 确保即使鼠标移出窗口边界或焦点被抢走，窗口仍能接收到鼠标事件

#### 修复 3：基于距离判断拖动

**修改前**：
```csharp
private void OnMouseMove(object sender, MouseEventArgs e)
{
    // ...
    
    Left = newX;
    Top = newY;
    _moved = true;  // 任何移动都标记为拖动
    
    // ...
}
```

**修改后**：
```csharp
private void OnMouseMove(object sender, MouseEventArgs e)
{
    // ...
    
    // 计算移动距离，超过阈值才算拖动
    var deltaX = Math.Abs(newX - _dragStartPosition.X);
    var deltaY = Math.Abs(newY - _dragStartPosition.Y);
    var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
    
    if (distance > DragThreshold)
    {
        _moved = true;
    }
    
    Left = newX;
    Top = newY;
    
    // ...
}
```

**原因**：
- 只有移动距离超过 5 像素才标记为拖动
- 避免鼠标微小抖动或点击时的轻微移动被误判为拖动
- 确保点击操作能够正确触发 `RestoreRequested` 事件

#### 修复 4：释放鼠标捕获

**修改前**：
```csharp
private void OnMouseUp(object sender, MouseButtonEventArgs e)
{
    if (e.ChangedButton != MouseButton.Left)
    {
        return;
    }
    
    _dragging = false;
    
    if (!_moved)
    {
        RestoreRequested?.Invoke();
    }
    else
    {
        // 延迟吸附到边缘
        Dispatcher.BeginInvoke(new Action(() => { ... }));
    }
    
    _moved = false;
}
```

**修改后**：
```csharp
private void OnMouseUp(object sender, MouseButtonEventArgs e)
{
    if (e.ChangedButton != MouseButton.Left)
    {
        return;
    }
    
    _dragging = false;
    
    // 释放鼠标捕获
    ReleaseMouseCapture();
    
    if (!_moved)
    {
        RestoreRequested?.Invoke();
    }
    else
    {
        // 延迟吸附到边缘
        Dispatcher.BeginInvoke(new Action(() => { ... }));
    }
    
    _moved = false;
}
```

**原因**：拖动结束后必须释放鼠标捕获，否则会影响其他窗口的鼠标交互。

#### 修复 5：使用当前屏幕工作区域

**修改前**：
```csharp
private void OnMouseMove(object sender, MouseEventArgs e)
{
    // ...
    
    // 边界检查，防止窗口移出屏幕
    var screenWidth = SystemParameters.PrimaryScreenWidth;
    var screenHeight = SystemParameters.PrimaryScreenHeight;
    
    newX = Math.Max(0, Math.Min(newX, screenWidth - Width));
    newY = Math.Max(0, Math.Min(newY, screenHeight - Height));
    
    // ...
}
```

**修改后**：
```csharp
private void OnMouseMove(object sender, MouseEventArgs e)
{
    // ...
    
    // 使用当前屏幕的工作区域进行边界检查
    var screenPoint = new System.Drawing.Point((int)screen.X, (int)screen.Y);
    var currentScreen = System.Windows.Forms.Screen.FromPoint(screenPoint);
    var workingArea = currentScreen.WorkingArea;
    
    newX = Math.Max(workingArea.Left, Math.Min(newX, workingArea.Right - Width));
    newY = Math.Max(workingArea.Top, Math.Min(newY, workingArea.Bottom - Height));
    
    // ...
}
```

**原因**：
- 多屏环境下，主屏幕尺寸不适用于其他屏幕
- 工作区域排除了任务栏，避免悬浮球被任务栏遮挡
- 使用当前鼠标位置所在屏幕，而非主屏幕

## 技术细节

### 拖动阈值机制

为了区分点击和拖动操作，引入了 5 像素的拖动阈值：

```csharp
private const double DragThreshold = 5.0;

// 在 OnMouseMove 中计算移动距离
var deltaX = Math.Abs(newX - _dragStartPosition.X);
var deltaY = Math.Abs(newY - _dragStartPosition.Y);
var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

if (distance > DragThreshold)
{
    _moved = true;  // 只有移动超过阈值才标记为拖动
}
```

这样可以：
- **避免误判**：鼠标微小抖动不会被当作拖动
- **保证点击**：点击操作能够正确触发窗口恢复
- **用户体验**：符合用户对点击和拖动的直觉预期

### 鼠标捕获机制

WPF 的 `CaptureMouse()` 方法提供以下保障：

1. **事件独占**：捕获后，所有鼠标事件都会发送到该窗口，即使鼠标移出窗口边界
2. **焦点保护**：即使其他窗口尝试获取焦点，鼠标事件仍会发送到捕获窗口
3. **拖动连续性**：确保拖动操作不会因为鼠标快速移动而中断

### 多屏幕支持

使用 `Screen.FromPoint()` 动态获取当前屏幕：

```csharp
var screenPoint = new System.Drawing.Point((int)screen.X, (int)screen.Y);
var currentScreen = System.Windows.Forms.Screen.FromPoint(screenPoint);
var workingArea = currentScreen.WorkingArea;
```

这样可以：
- 支持多屏幕环境
- 自动适应不同分辨率
- 避免任务栏遮挡

## 测试步骤

### 1. 重新构建项目
```powershell
dotnet build ClassroomToolkit.sln
```

### 2. 启动应用程序
```powershell
dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj
```

### 3. 测试拖动功能

#### 测试 1：正常拖动
1. 点击"最小化"按钮，启动器缩小为悬浮球
2. 按住悬浮球拖动到屏幕不同位置
3. **预期**：拖动流畅，无卡顿
4. **预期**：松开鼠标后自动吸附到最近的边缘

#### 测试 2：快速拖动
1. 快速拖动悬浮球到屏幕各个角落
2. **预期**：即使快速移动，拖动仍然连续
3. **预期**：不会出现"卡住"现象

#### 测试 3：多屏幕测试（如果有多个显示器）
1. 将悬浮球拖动到第二个显示器
2. **预期**：能够正常拖动
3. **预期**：吸附到第二个显示器的边缘，而非主显示器

#### 测试 4：焦点干扰测试
1. 开始拖动悬浮球
2. 拖动过程中，其他应用程序弹出窗口或通知
3. **预期**：拖动不受影响，继续跟随鼠标
4. **预期**：松开鼠标后正常吸附

#### 测试 5：边界测试
1. 尝试将悬浮球拖动到屏幕边缘外
2. **预期**：悬浮球被限制在屏幕工作区域内
3. **预期**：不会被任务栏遮挡

### 4. 验证点击功能
1. 点击悬浮球（不拖动）
2. **预期**：主窗口恢复显示
3. **预期**：悬浮球消失

## 预期效果

修复后，悬浮球应该：

✅ **流畅无卡顿**：鼠标捕获确保事件连续性  
✅ **不会卡住**：即使焦点被抢走也能正常拖动  
✅ **点击正常**：点击悬浮球能够恢复主窗口  
✅ **拖动准确**：只有真正拖动才会吸附边缘  
✅ **正确吸附**：使用当前屏幕工作区域计算吸附位置  
✅ **多屏支持**：在多显示器环境下正常工作  
✅ **避免遮挡**：不会被任务栏遮挡  

## 相关技术

### WPF 鼠标捕获 API

- `CaptureMouse()`：捕获鼠标事件
- `ReleaseMouseCapture()`：释放鼠标捕获
- `IsMouseCaptured`：检查是否已捕获鼠标

### Windows Forms Screen API

- `Screen.FromPoint()`：获取指定点所在的屏幕
- `Screen.WorkingArea`：获取屏幕工作区域（排除任务栏）
- `Screen.Bounds`：获取屏幕完整区域

## 提交建议

```
Fix launcher bubble drag stuttering and click detection

- Add drag threshold (5px) to distinguish click from drag
  Prevents mouse jitter from being misinterpreted as drag
  
- Record drag start position for distance calculation
  Enables accurate drag detection based on movement distance
  
- Add mouse capture in OnMouseDown to prevent event loss
  Ensures continuous mouse events even when focus is stolen
  
- Release mouse capture in OnMouseUp
  Prevents interference with other windows after drag ends
  
- Use current screen working area for boundary check
  Fixes multi-monitor support and taskbar occlusion
  
Fixes:
- Bubble stuttering during drag
- Bubble stuck in screen when focus is stolen
- Click not working after adding mouse capture
- Incorrect snap position in multi-monitor setup
- Bubble hidden behind taskbar
```

## 总结

本次修复通过五个关键改进解决了悬浮球拖动卡顿和点击失效问题：

1. **拖动阈值**：引入 5 像素阈值，准确区分点击和拖动操作
2. **起始位置记录**：记录拖动起始位置，用于计算移动距离
3. **鼠标捕获**：确保拖动过程中事件连续性，防止焦点抢夺导致的卡顿
4. **正确释放**：拖动结束后释放捕获，避免影响其他窗口
5. **多屏支持**：使用当前屏幕工作区域，支持多显示器环境

这些改进使悬浮球既能流畅拖动，又能正确响应点击，即使在复杂的多窗口、多屏幕环境下也能正常工作。
