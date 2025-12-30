# 照片闪现问题修复

## 问题描述
在点名功能中，当显示新的学生照片时，会先闪现一下上一个被点名学生的照片，然后再显示这一次点到名的学生照片。

## 问题根因分析
在 `PhotoOverlayWindow.ShowPhoto` 方法中，代码的执行顺序存在问题：

1. 加载新照片的 bitmap
2. 调用 `Show()` 显示窗口
3. 设置新的照片内容到 `PhotoImage.Source`

问题在于第2步 `Show()` 调用时，窗口还显示着上一个学生的照片，直到第3步新照片加载完成才会更新。这导致了短暂的闪现效果。

## 修复方案

### 1. 添加 ClearVisualContent 方法
创建一个专门的方法来清除视觉内容，但不触发业务事件：

```csharp
private void ClearVisualContent()
{
    // 只清除视觉内容，不触发 PhotoClosed 事件
    PhotoImage.Source = null;
    PhotoImage.Width = double.NaN;
    PhotoImage.Height = double.NaN;
    NameText.Text = string.Empty;
    NameText.Visibility = Visibility.Collapsed;
}
```

### 2. 修改 ShowPhoto 方法执行顺序
在显示窗口之前先清除旧的视觉内容：

```csharp
public void ShowPhoto(string path, string studentName, string studentId, int durationSeconds, Window? owner)
{
    var bitmap = LoadBitmap(path);
    if (bitmap == null)
    {
        Hide();
        return;
    }
    
    // 先清除之前的视觉内容，避免闪现上一个学生的照片
    ClearVisualContent();
    
    // 设置新的学生信息
    _currentPhotoPath = path;
    _currentStudentId = studentId?.Trim();
    NameText.Text = studentName ?? string.Empty;
    NameText.Visibility = string.IsNullOrWhiteSpace(NameText.Text) ? Visibility.Collapsed : Visibility.Visible;

    // ... 计算尺寸和位置 ...
    
    // 设置新的照片内容
    PhotoImage.Source = bitmap;
    PhotoImage.Width = targetWidth;
    PhotoImage.Height = targetHeight;

    // 设置窗口位置
    Width = targetWidth;
    Height = targetHeight;
    Left = screen.X + (screen.Width - targetWidth) / 2;
    Top = screen.Y + (screen.Height - targetHeight) / 2;
    WindowPlacementHelper.EnsureVisible(this);

    // 最后显示窗口
    Show();
    
    // 设置自动关闭定时器
    if (durationSeconds > 0)
    {
        _autoCloseTimer.Interval = TimeSpan.FromSeconds(durationSeconds);
        _autoCloseTimer.Start();
    }
    else
    {
        _autoCloseTimer.Stop();
    }
}
```

## 设计考虑

### 为什么不使用 ClearPhotoCache？
`ClearPhotoCache()` 方法会触发 `PhotoClosed` 事件，这会影响点名逻辑，可能导致：
- 错误地记录学生照片关闭事件
- 影响点名状态管理
- 干扰后续的点名流程

### ClearVisualContent 的优势
- 只清除视觉元素，不影响业务状态
- 不触发任何事件
- 确保窗口显示时是干净的状态
- 避免闪现上一个学生的照片

## 修复效果
- ✅ 完全消除照片闪现问题
- ✅ 直接显示当前学生的照片
- ✅ 不影响点名逻辑和状态管理
- ✅ 保持原有的自动关闭功能

## 测试验证
1. 连续点名多个学生
2. 确认每个学生照片显示时没有闪现上一个学生的照片
3. 验证点名功能正常工作
4. 检查自动关闭定时器功能正常

## 技术细节
- 修复位置：`src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs`
- 修复方法：调整 `ShowPhoto` 方法的执行顺序
- 新增方法：`ClearVisualContent()`
- 兼容性：不影响现有功能和接口
