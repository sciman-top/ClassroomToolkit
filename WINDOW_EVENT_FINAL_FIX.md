# Window 事件编译错误最终修复

## 问题描述
编译错误：`CS0117: "Window"未包含"SourceInitializedEvent"的定义`

## 错误原因
`Window` 类既没有 `InitializedEvent` 也没有 `SourceInitializedEvent`。需要使用实际存在的事件。

## 最终解决方案

### 1. 使用 LoadedEvent
`Window.LoadedEvent` 是确实存在的事件，虽然时机稍晚，但配合构造函数中的修复仍然有效。

```csharp
// ✅ 正确的写法
EventManager.RegisterClassHandler(
    typeof(Window),
    Window.LoadedEvent,
    new RoutedEventHandler(OnWindowLoaded));
```

### 2. 双重修复策略
在 `OnWindowLoaded` 中实现双重修复：

#### 立即修复
```csharp
FixAllBorders(window);
```

#### 延迟修复
```csharp
window.Dispatcher.BeginInvoke(new Action(() =>
{
    FixAllBorders(window);
}), DispatcherPriority.Loaded);
```

## 完整的修复时机

| 时机 | 位置 | 作用 |
|------|------|------|
| **Constructor** | DiagnosticsDialog 构造函数 | 🚀 最早修复，预防异常 |
| **Loaded** | 全局事件监听 | 🔧 补充修复，覆盖遗漏 |
| **Delayed** | Dispatcher 延迟执行 | 🛡️ 确保动态控件也被修复 |

## 修复后的代码

### BorderFixHelper.cs
```csharp
public static void RegisterGlobalFix()
{
    // 监听窗口 Loaded 事件
    EventManager.RegisterClassHandler(
        typeof(Window),
        Window.LoadedEvent,
        new RoutedEventHandler(OnWindowLoaded));
}

private static void OnWindowLoaded(object sender, RoutedEventArgs e)
{
    if (sender is Window window)
    {
        // 立即修复
        FixAllBorders(window);
        
        // 延迟修复，确保动态创建的控件也被处理
        window.Dispatcher.BeginInvoke(new Action(() =>
        {
            FixAllBorders(window);
        }), DispatcherPriority.Loaded);
    }
}
```

### DiagnosticsDialog.xaml.cs
```csharp
public DiagnosticsDialog(DiagnosticsResult result)
{
    InitializeComponent();
    
    // 在构造函数中立即修复
    BorderFixHelper.FixAllBorders(this);
    
    // ... 其他初始化代码
}
```

## 验证步骤
1. 重新构建应用程序
2. 确认编译成功，无错误
3. 启动应用程序
4. 打开诊断对话框
5. 查看调试输出中的修复记录

## 预期调试输出
```
DiagnosticsDialog: 构造函数中修复完成
BorderFixHelper: 窗口 DiagnosticsDialog 加载时修复完成
BorderFixHelper: 窗口 DiagnosticsDialog 延迟修复完成
```

## 技术要点

### 1. 事件监听
使用 `EventManager.RegisterClassHandler` 监听所有窗口的 `LoadedEvent`。

### 2. 双重修复
- **立即修复**：在事件触发时立即执行
- **延迟修复**：通过 Dispatcher 延迟执行，确保动态创建的控件也被处理

### 3. 构造函数修复
在特定窗口（如 DiagnosticsDialog）的构造函数中立即修复，确保在窗口显示前就完成修复。

## 为什么这个方案能成功

1. **时机正确**：构造函数中的修复确保在窗口显示前就完成修复
2. **覆盖全面**：全局事件监听确保所有窗口都被处理
3. **容错性强**：双重修复机制确保即使某一层失败，其他层也能补救
4. **调试友好**：详细的调试输出帮助验证修复效果

这个最终解决方案应该能够彻底解决所有 BorderBrush 相关的问题！
