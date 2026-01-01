# 工具条按钮点击失效问题分析与修复

## 问题描述

切换到光标模式后，再点击工具条上的颜色按钮（画笔按钮）失效，无法切换回绘图模式。

## 问题分析

### 根本原因

工具条窗口的 `UpdateWindowTransparency()` 方法在某些情况下可能错误地设置了 `WS_EX_TRANSPARENT` 样式，导致窗口变成穿透的，按钮无法接收鼠标点击事件。

### 问题场景

1. 打开画笔工具条 → 自动进入画笔模式（正常）
2. 点击"光标模式"按钮 → 切换到光标模式（正常）
3. 点击颜色按钮想切换回画笔模式 → **按钮无响应**（问题）

### 可能的原因

#### 原因 1：窗口穿透逻辑错误

`PaintModeManager.ShouldToolbarAllowTransparency()` 方法的逻辑：
```csharp
public bool ShouldToolbarAllowTransparency(bool mouseOver)
{
    // 工具条窗口在正在绘图时穿透，鼠标在窗口上时不穿透
    return !mouseOver && _currentMode == PaintToolMode.Brush && _isDrawing;
}
```

这个逻辑看起来是正确的：
- 只有在画笔模式 + 正在绘图 + 鼠标不在窗口上时才穿透
- 光标模式下应该返回 `false`，窗口不穿透

#### 原因 2：鼠标事件未正确触发

`OnToolbarMouseEnter` 和 `OnToolbarMouseLeave` 事件可能没有正确触发，导致 `_mouseOver` 状态不正确。

#### 原因 3：窗口样式更新时机问题

在模式切换时，`UpdateWindowTransparency()` 可能在错误的时机被调用，导致窗口样式不正确。

#### 原因 4：覆盖窗口影响

`PaintOverlayWindow` 在光标模式下设置了 `WS_EX_TRANSPARENT`，可能影响了工具条窗口的点击事件。

## 调试步骤

### 步骤 1：添加调试日志

在 `UpdateWindowTransparency()` 方法中添加日志：

```csharp
private void UpdateWindowTransparency()
{
    if (_hwnd == IntPtr.Zero)
    {
        return;
    }
    
    bool shouldBeTransparent = PaintModeManager.Instance.ShouldToolbarAllowTransparency(_mouseOver);
    
    // 添加调试日志
    System.Diagnostics.Debug.WriteLine($"[PaintToolbarWindow] UpdateWindowTransparency:");
    System.Diagnostics.Debug.WriteLine($"  CurrentMode: {PaintModeManager.Instance.CurrentMode}");
    System.Diagnostics.Debug.WriteLine($"  IsDrawing: {PaintModeManager.Instance.IsDrawing}");
    System.Diagnostics.Debug.WriteLine($"  MouseOver: {_mouseOver}");
    System.Diagnostics.Debug.WriteLine($"  ShouldBeTransparent: {shouldBeTransparent}");
    
    var exStyle = GetWindowLong(_hwnd, GwlExstyle);
    var oldTransparent = (exStyle & WsExTransparent) != 0;
    
    if (shouldBeTransparent)
    {
        exStyle |= WsExTransparent;
    }
    else
    {
        exStyle &= ~WsExTransparent;
    }
    
    SetWindowLong(_hwnd, GwlExstyle, exStyle);
    
    var newTransparent = (exStyle & WsExTransparent) != 0;
    System.Diagnostics.Debug.WriteLine($"  Transparent: {oldTransparent} -> {newTransparent}");
}
```

### 步骤 2：检查鼠标事件

在 `OnToolbarMouseEnter` 和 `OnToolbarMouseLeave` 中添加日志：

```csharp
private void OnToolbarMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
{
    System.Diagnostics.Debug.WriteLine("[PaintToolbarWindow] Mouse Enter");
    _mouseOver = true;
    UpdateWindowTransparency();
}

private void OnToolbarMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
{
    System.Diagnostics.Debug.WriteLine("[PaintToolbarWindow] Mouse Leave");
    _mouseOver = false;
    UpdateWindowTransparency();
}
```

### 步骤 3：检查按钮点击事件

在 `OnColorClick` 方法开始处添加日志：

```csharp
private void OnColorClick(object sender, RoutedEventArgs e)
{
    System.Diagnostics.Debug.WriteLine($"[PaintToolbarWindow] OnColorClick - Initializing: {_initializing}");
    
    if (sender is not ToggleButton button || _initializing)
    {
        System.Diagnostics.Debug.WriteLine($"[PaintToolbarWindow] OnColorClick - Ignored (sender: {sender?.GetType().Name}, initializing: {_initializing})");
        return;
    }
    
    // ... 其余代码
}
```

## 可能的修复方案

### 方案 1：确保光标模式下窗口不穿透

在 `UpdateToolButtons` 方法中，切换到光标模式时强制更新窗口透明度：

```csharp
private void UpdateToolButtons(PaintToolMode mode)
{
    // ... 现有代码 ...
    
    // 更新窗口穿透状态
    UpdateWindowTransparency();
    
    // 光标模式下，强制确保窗口不穿透
    if (mode == PaintToolMode.Cursor && _hwnd != IntPtr.Zero)
    {
        var exStyle = GetWindowLong(_hwnd, GwlExstyle);
        exStyle &= ~WsExTransparent;  // 移除穿透样式
        SetWindowLong(_hwnd, GwlExstyle, exStyle);
        System.Diagnostics.Debug.WriteLine("[PaintToolbarWindow] 光标模式：强制移除窗口穿透");
    }
    
    // ... 其余代码 ...
}
```

### 方案 2：修复鼠标事件绑定

检查 XAML 文件，确保鼠标事件正确绑定到工具条容器：

```xaml
<Border MouseEnter="OnToolbarMouseEnter" 
        MouseLeave="OnToolbarMouseLeave"
        ...>
    <!-- 工具条内容 -->
</Border>
```

### 方案 3：延迟更新窗口样式

在模式切换后延迟更新窗口样式，确保所有状态都已更新：

```csharp
private void UpdateToolButtons(PaintToolMode mode)
{
    // ... 现有代码 ...
    
    // 延迟更新窗口透明度，确保状态已同步
    Dispatcher.BeginInvoke(new Action(() =>
    {
        UpdateWindowTransparency();
    }), System.Windows.Threading.DispatcherPriority.Background);
}
```

### 方案 4：简化穿透逻辑

修改 `ShouldToolbarAllowTransparency` 方法，添加更严格的条件：

```csharp
public bool ShouldToolbarAllowTransparency(bool mouseOver)
{
    // 只有在画笔模式、正在绘图、鼠标不在窗口上时才穿透
    // 其他任何情况都不穿透，确保按钮可点击
    if (mouseOver)
    {
        return false;  // 鼠标在窗口上，绝对不穿透
    }
    
    if (_currentMode != PaintToolMode.Brush)
    {
        return false;  // 非画笔模式，不穿透
    }
    
    if (!_isDrawing)
    {
        return false;  // 没有在绘图，不穿透
    }
    
    return true;  // 只有满足所有条件才穿透
}
```

## 推荐修复方案

**组合方案 1 + 方案 4**：

1. 修改 `PaintModeManager.ShouldToolbarAllowTransparency()` 方法，使用更严格的逻辑
2. 在 `UpdateToolButtons()` 方法中，切换到光标模式时强制移除窗口穿透样式
3. 添加调试日志，便于后续排查问题

## 测试步骤

1. 启动应用程序
2. 启动 WPS 全屏放映
3. 点击"画笔"按钮打开工具条
4. 确认自动进入画笔模式（可以绘图）
5. 点击"光标模式"按钮
6. 确认切换到光标模式（不能绘图）
7. **点击任意颜色按钮**
8. **确认能够切换回画笔模式**
9. **确认可以正常绘图**

## 预期结果

- 光标模式下，工具条窗口不应该有 `WS_EX_TRANSPARENT` 样式
- 点击颜色按钮应该能够正常触发 `OnColorClick` 事件
- 应该能够顺利切换回画笔模式
- 切换后应该能够正常绘图

## 注意事项

1. 窗口穿透功能是为了让笔画能够在工具条下层显示
2. 但这个功能只应该在**正在绘图时**启用
3. 其他任何时候，工具条都应该是可点击的
4. 鼠标在工具条上时，绝对不应该穿透
