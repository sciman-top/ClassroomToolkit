# Window.InitializedEvent 编译错误修复

## 问题描述
编译错误：`CS0117: "Window"未包含"InitializedEvent"的定义`

## 错误原因
`Window` 类没有 `InitializedEvent` 事件。需要使用其他可用的事件。

## 修复方法
使用 `SourceInitializedEvent` 替代 `InitializedEvent`：

```csharp
// ❌ 错误
EventManager.RegisterClassHandler(
    typeof(Window),
    Window.InitializedEvent,  // ← 不存在
    new EventHandler(OnWindowInitialized));

// ✅ 正确
EventManager.RegisterClassHandler(
    typeof(Window),
    Window.SourceInitializedEvent,  // ← 存在且更早触发
    new EventHandler(OnWindowSourceInitialized));
```

## 事件时机对比

| 事件 | 触发时机 | 适用场景 |
|------|----------|----------|
| **SourceInitialized** | 窗口句柄创建后 | 🚀 最早，适合预修复 |
| **Loaded** | 窗口完全加载后 | 🔧 补充修复 |
| **Constructor** | 构造函数中 | 🛡️ 立即修复 |

## 修复内容

### 1. 更换事件类型
```csharp
private static void OnWindowSourceInitialized(object? sender, EventArgs e)
{
    if (sender is Window window)
    {
        // 在窗口源初始化时立即修复
        FixAllBorders(window);
    }
}
```

### 2. 修复 Null 性警告
```csharp
// 添加 ? 标记以支持可空引用类型
private static void OnWindowSourceInitialized(object? sender, EventArgs e)
```

## 验证步骤
1. 重新构建应用程序
2. 确认编译成功，无错误
3. 启动应用程序
4. 打开诊断对话框
5. 查看调试输出中的修复记录

## 预期结果
- ✅ 编译成功，无错误
- ✅ 应用程序正常启动
- ✅ 更早的修复时机（SourceInitialized 比 Loaded 更早）
- ✅ 所有 BorderBrush 问题自动修复

## 技术要点
- `SourceInitializedEvent` 在窗口句柄创建后立即触发
- 比 `LoadedEvent` 更早，适合进行预修复
- 与构造函数中的修复形成三重保险

现在应该可以正常编译和运行了！
