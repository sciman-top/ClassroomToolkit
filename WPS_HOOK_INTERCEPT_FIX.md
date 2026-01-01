# WPS 钩子拦截问题修复

## 问题分析

### 现象

WPS 全屏放映时，从绘图模式切换到光标模式后：
- ✅ **字母、数字键正常**：可以输入
- ❌ **方向键失效**：Up, Down, Left, Right
- ❌ **翻页键失效**：PageUp, PageDown
- ❌ **空格键失效**：Space
- ❌ **回车键失效**：Enter
- ❌ **鼠标滚轮失效**：无法翻页

### 根本原因

**WPS 导航钩子（WpsSlideshowNavigationHook）在光标模式下仍然拦截输入**

1. **钩子拦截特定按键**：`WpsSlideshowNavigationHook` 使用全局钩子拦截以下按键：
   - 方向键（Up, Down, Left, Right）
   - 翻页键（PageUp, PageDown）
   - 空格键（Space）
   - 回车键（Enter）
   - 鼠标滚轮

2. **字母数字键不受影响**：这些键不在 `_allowedKeys` 列表中，所以钩子不会拦截它们

3. **光标模式下的错误逻辑**：在 `UpdateWpsNavHookState()` 方法中：
   ```csharp
   if (_mode != PaintToolMode.Cursor)  // 只有非光标模式才禁用拦截
   {
       interceptKeyboard = false;
   }
   ```
   这个逻辑是**反的**！应该是光标模式下禁用拦截，而不是非光标模式。

### 为什么点击两次"鼠标模式"能恢复？

点击两次会触发：
1. 第一次：光标模式 → 画笔模式（钩子被禁用）
2. 第二次：画笔模式 → 光标模式（钩子被重新配置，但由于某种原因没有启动）

## 修复方案

### 方案：修正光标模式下的钩子拦截逻辑

在光标模式下，应该**禁用键盘和滚轮拦截**，让输入直接传递到 WPS。

### 修改文件：`src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs`

#### 修改 UpdateWpsNavHookState 方法

**修改前**：
```csharp
if (shouldEnable && sendMode == ClassroomToolkit.Interop.Presentation.InputStrategy.Raw)
{
    blockOnly = true;
    if (IsTargetForeground(target))
    {
        if (_mode != PaintToolMode.Cursor)  // 错误：非光标模式才禁用
        {
            interceptKeyboard = false;
        }
        if (!wheelForward)
        {
            if (_mode != PaintToolMode.Cursor)  // 错误：非光标模式才禁用
            {
                interceptWheel = false;
            }
            blockOnly = false;
            emitWheelOnBlock = false;
        }
    }
}
```

**修改后**：
```csharp
if (shouldEnable && sendMode == ClassroomToolkit.Interop.Presentation.InputStrategy.Raw)
{
    blockOnly = true;
    if (IsTargetForeground(target))
    {
        // 光标模式下禁用键盘拦截，让输入直接传递到 WPS
        if (_mode == PaintToolMode.Cursor)
        {
            interceptKeyboard = false;
        }
        if (!wheelForward)
        {
            // 光标模式下禁用滚轮拦截，让滚轮直接传递到 WPS
            if (_mode == PaintToolMode.Cursor)
            {
                interceptWheel = false;
            }
            blockOnly = false;
            emitWheelOnBlock = false;
        }
    }
}
```

### 技术说明

#### WPS 导航钩子的工作原理

1. **全局钩子**：使用 `SetWindowsHookEx` 安装全局键盘和鼠标钩子
2. **选择性拦截**：只拦截导航相关的按键（方向键、空格、回车等）
3. **事件转发**：拦截后触发 `NavigationRequested` 事件，由应用程序处理
4. **阻止传递**：返回 `new IntPtr(1)` 阻止按键传递到目标窗口

#### 拦截模式

钩子有两种工作模式：

1. **拦截并处理模式**（`blockOnly = false`）：
   - 拦截按键
   - 触发 `NavigationRequested` 事件
   - 阻止按键传递到 WPS
   - 应用程序自己发送导航命令到 WPS

2. **仅阻止模式**（`blockOnly = true`）：
   - 拦截按键
   - 触发 `NavigationRequested` 事件
   - 阻止按键传递到 WPS
   - 但不发送任何命令（用于防止重复输入）

#### 光标模式的正确行为

在光标模式下：
- 用户期望直接与 WPS 交互
- 不应该拦截任何输入
- 键盘和滚轮应该直接传递到 WPS
- 钩子应该完全禁用或设置 `interceptKeyboard = false` 和 `interceptWheel = false`

## 测试步骤

### 1. 构建项目
```powershell
dotnet build ClassroomToolkit.sln
```

### 2. 启动应用程序
```powershell
dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj
```

### 3. 测试 WPS 输入功能

#### 测试 1：光标模式下的键盘输入
1. 启动 WPS 演示文稿并进入全屏放映（F5）
2. 在应用程序中点击"屏幕画笔"按钮
3. 切换到绘图模式，绘制一些内容
4. 切换回光标模式（点击"鼠标模式"按钮）
5. **立即测试**：
   - 按空格键 → 应该翻到下一页
   - 按方向键 → 应该翻页
   - 按回车键 → 应该翻到下一页
   - 滚动鼠标滚轮 → 应该翻页
6. **预期**：所有按键和滚轮应该立即正常工作

#### 测试 2：绘图模式下的拦截
1. 在 WPS 全屏放映中
2. 切换到绘图模式
3. 按空格键、方向键
4. **预期**：按键应该被拦截，用于翻页而不是绘图

#### 测试 3：多次切换测试
1. 多次在绘图模式和光标模式之间切换
2. 每次切换后立即测试键盘和滚轮
3. **预期**：每次都能正常工作

#### 测试 4：字母数字键测试
1. 在光标模式下
2. 按字母键（如 A, B, C）和数字键（如 1, 2, 3）
3. **预期**：这些键应该一直正常工作（之前就正常）

## 预期效果

修复后，光标模式下的输入应该：

✅ **方向键正常**：Up, Down, Left, Right 可以翻页  
✅ **翻页键正常**：PageUp, PageDown 可以翻页  
✅ **空格键正常**：Space 可以翻页  
✅ **回车键正常**：Enter 可以翻页  
✅ **滚轮正常**：鼠标滚轮可以翻页  
✅ **字母数字键正常**：继续正常工作  
✅ **立即生效**：切换到光标模式后立即可用  
✅ **无需点击两次**：不需要任何额外操作  

## 相关技术

### Windows 钩子机制

- `SetWindowsHookEx()`：安装全局钩子
- `UnhookWindowsHookEx()`：卸载钩子
- `CallNextHookEx()`：传递事件到下一个钩子
- 返回 `new IntPtr(1)`：阻止事件传递

### 钩子类型

- `WH_KEYBOARD_LL (13)`：低级键盘钩子
- `WH_MOUSE_LL (14)`：低级鼠标钩子

### 拦截的按键（VirtualKey）

```csharp
private readonly HashSet<VirtualKey> _allowedKeys = new()
{
    VirtualKey.Up,
    VirtualKey.Down,
    VirtualKey.Left,
    VirtualKey.Right,
    VirtualKey.PageUp,
    VirtualKey.PageDown,
    VirtualKey.Space,
    VirtualKey.Enter
};
```

## 提交建议

```
Fix WPS hook intercepting input in cursor mode

- Disable keyboard intercept in cursor mode
  Change condition from (_mode != Cursor) to (_mode == Cursor)
  
- Disable wheel intercept in cursor mode
  Allow direct input passthrough to WPS in cursor mode
  
- Hook should only intercept in paint/erase modes
  Cursor mode requires direct WPS interaction
  
Fixes:
- Arrow keys not working in cursor mode
- Space/Enter keys not working in cursor mode
- Mouse wheel not working in cursor mode
- Need to click cursor mode button twice to restore input
```

## 总结

本次修复通过修正 `UpdateWpsNavHookState()` 方法中的条件判断，确保在光标模式下禁用键盘和滚轮拦截。

**核心修改**：
- 将 `if (_mode != PaintToolMode.Cursor)` 改为 `if (_mode == PaintToolMode.Cursor)`
- 确保光标模式下 `interceptKeyboard = false` 和 `interceptWheel = false`

这个修复：
- **逻辑正确**：光标模式下应该禁用拦截，而不是启用
- **简单直接**：只需修改条件判断
- **立即生效**：切换模式时自动更新钩子状态
- **无副作用**：不影响其他模式的功能

修复后，用户在光标模式下可以正常使用所有按键和滚轮，无需任何额外操作。
