# App.xaml.cs 编译错误修复

## 问题描述
编译错误：`CS0246: 未能找到类型或命名空间名"StartupEventArgs"`

## 错误原因
`StartupEventArgs` 类型位于 `System.Windows` 命名空间，但缺少相应的 using 指令。

## 修复方法
添加 `using System.Windows;` 指令：

```csharp
using WpfApplication = System.Windows.Application;
using System.Windows;  // ← 新增
using ClassroomToolkit.App.Helpers;

namespace ClassroomToolkit.App;

public partial class App : WpfApplication
{
    protected override void OnStartup(StartupEventArgs e)  // ← 现在可以找到这个类型
    {
        base.OnStartup(e);
        
        // 注册全局 Border 修复
        BorderFixHelper.RegisterGlobalFix();
    }
}
```

## 验证步骤
1. 重新构建应用程序
2. 确认编译成功
3. 启动应用程序
4. 查看调试输出中的 Border 修复记录

## 预期结果
- ✅ 编译成功，无错误
- ✅ 应用程序正常启动
- ✅ 全局 Border 修复系统正常工作
- ✅ 所有 BorderBrush 问题自动修复

现在应该可以正常构建和运行应用程序了！
