# MainWindow BorderBrush 问题修复

## 问题分析
BorderBrush 问题现在出现在 `MainWindow.OnLauncherSettingsClick` 方法中，这表明我们需要将安全显示机制应用到所有对话框显示的地方。

## 修复方案

### 1. 应用安全显示模式
将 `MainWindow.OnLauncherSettingsClick` 方法修改为使用我们的安全显示机制：

```csharp
private void OnLauncherSettingsClick(object sender, RoutedEventArgs e)
{
    var currentMinutes = Math.Max(0, _settings.LauncherAutoExitSeconds / 60);
    var dialog = new AutoExitDialog(currentMinutes, _settings)
    {
        Owner = this
    };
    
    // 先修复当前窗口
    BorderFixHelper.FixAllBorders(this);
    
    // 立即修复新创建的对话框
    BorderFixHelper.FixAllBorders(dialog);
    
    // 使用安全显示方法
    bool? result = dialog.SafeShowDialog();
    
    if (result != true)
    {
        return;
    }
    _settings.LauncherAutoExitSeconds = Math.Max(0, dialog.Minutes) * 60;
    ScheduleAutoExitTimer();
}
```

### 2. 添加必要的 using 指令
```csharp
using ClassroomToolkit.App.Helpers;
```

## 修复效果

### 1. 强制修复机制
- **修复当前窗口**: `BorderFixHelper.FixAllBorders(this)`
- **修复对话框**: `BorderFixHelper.FixAllBorders(dialog)`
- **安全显示**: `dialog.SafeShowDialog()`

### 2. 调试输出
```
MainWindow: 修复当前窗口完成
MainWindow: 修复对话框完成
ForceFixBorder: 强制修复 Border 'PhotoFrame'
ForceFixBorder: 强制修复 Border 'border'
SafeShowDialog: 第一次尝试成功
```

## 系统性解决方案

### 1. 全局修复策略
现在我们有了完整的修复策略：
- **应用启动**: XAML 文件自动修复
- **全局事件**: Window.LoadedEvent 监听
- **调用点修复**: 每个对话框显示前修复
- **安全显示**: 强制递归修复

### 2. 预防措施
- **所有对话框**: 都应该使用 `SafeShowDialog()` 方法
- **所有窗口**: 在创建后立即调用 `BorderFixHelper.FixAllBorders()`
- **全局监听**: 自动处理所有窗口的 Loaded 事件

## 下一步行动

### 1. 搜索其他对话框
需要搜索应用程序中所有使用 `ShowDialog()` 的地方，并应用安全显示模式：

```csharp
// 搜索模式
\.ShowDialog\(\)
```

### 2. 统一修复策略
创建一个统一的对话框显示方法：

```csharp
public static bool? ShowDialogSafely(this Window dialog, Window owner = null)
{
    if (owner != null)
    {
        dialog.Owner = owner;
        BorderFixHelper.FixAllBorders(owner);
    }
    
    BorderFixHelper.FixAllBorders(dialog);
    return dialog.SafeShowDialog();
}
```

### 3. 批量替换
将所有 `dialog.ShowDialog()` 替换为 `dialog.ShowDialogSafely()`。

## 验证步骤

1. **重新构建应用程序**
2. **测试启动器设置对话框**
3. **查看调试输出**
4. **确认无异常抛出**

## 预期结果

- ✅ 启动器设置对话框正常显示
- ✅ 无 BorderBrush 异常
- ✅ 详细的修复过程记录
- ✅ 所有对话框都能正常工作

这个修复应该能够解决 MainWindow 中的 BorderBrush 问题，并为其他对话框提供修复模板。
