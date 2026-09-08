using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Velopack;

namespace ClassroomToolkit.App;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Global\ClassroomToolkit.SingleInstance";
    private const string SessionSingleInstanceMutexName = @"Local\ClassroomToolkit.SingleInstance";

    // 互斥体必须存活整个进程生命周期；若被 GC 回收，句柄关闭会提前释放单实例锁。
    private static Mutex? _singleInstanceMutex;

    private enum SingleInstanceAcquireOutcome
    {
        Acquired,
        AlreadyRunning,
        AccessDenied
    }

    [STAThread]
    public static void Main()
    {
        // Velopack 更新钩子先于全局异常处理与日志器执行，钩子内异常不允许直接崩溃进程
        //（更新业务可降级，失败原因尽力落盘）。
        try
        {
            VelopackApp.Build().Run();
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            TryWriteStartupCrashLog("velopack-hook", ex);
        }

        if (!AcquireSingleInstance())
        {
            return;
        }

        // 全局异常处理器要到 OnStartup 才注册：OnStartup 之前的构造/XAML 解析/互斥阶段
        // 失败必须在此兜底，否则静默闪退且无任何日志。
        try
        {
            var application = new App();
            application.InitializeComponent();
            application.Run();
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            TryWriteStartupCrashLog("app-startup", ex);
            ShowTopmostNotice($"ClassroomToolkit 启动失败：{ex.Message}", isError: true);
            Environment.Exit(-1);
        }
    }

    private static bool AcquireSingleInstance()
    {
        // Global\ 提供跨登录会话互斥（快速用户切换/管理员第二会话），避免 settings/名册
        // 双写者互相静默覆写；ACL 拒绝时降级回会话级 Local\，至少保留同会话互斥。
        var global = TryAcquireMutex(SingleInstanceMutexName);
        if (global.outcome == SingleInstanceAcquireOutcome.Acquired)
        {
            _singleInstanceMutex = global.mutex;
            return true;
        }
        if (global.outcome == SingleInstanceAcquireOutcome.AlreadyRunning)
        {
            NoticeAlreadyRunning();
            return false;
        }

        var session = TryAcquireMutex(SessionSingleInstanceMutexName);
        if (session.outcome == SingleInstanceAcquireOutcome.Acquired)
        {
            _singleInstanceMutex = session.mutex;
            return true;
        }
        if (session.outcome == SingleInstanceAcquireOutcome.AlreadyRunning)
        {
            NoticeAlreadyRunning();
            return false;
        }

        // 两级互斥都被 ACL 拒绝（极罕见）：按课堂可用性优先继续启动。
        return true;
    }

    private static (SingleInstanceAcquireOutcome outcome, Mutex? mutex) TryAcquireMutex(string name)
    {
        try
        {
            var mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
            if (createdNew)
            {
                return (SingleInstanceAcquireOutcome.Acquired, mutex);
            }

            mutex.Dispose();
            return (SingleInstanceAcquireOutcome.AlreadyRunning, null);
        }
        catch (UnauthorizedAccessException)
        {
            return (SingleInstanceAcquireOutcome.AccessDenied, null);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            TryWriteStartupCrashLog($"mutex-acquire:{name}", ex);
            return (SingleInstanceAcquireOutcome.AccessDenied, null);
        }
    }

    private static void NoticeAlreadyRunning()
    {
        ShowTopmostNotice("ClassroomToolkit 已经在运行，请使用已打开的实例。", isError: false);
    }

    private static void ShowTopmostNotice(string text, bool isError)
    {
        try
        {
            const uint mbIconInformation = 0x40u;
            const uint mbIconError = 0x10u;
            const uint mbTopmost = 0x40000u;
            var icon = isError ? mbIconError : mbIconInformation;
            _ = MessageBox(IntPtr.Zero, text, "ClassroomToolkit", icon | mbTopmost);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            // 提示失败也不阻断退出
        }
    }

    private static void TryWriteStartupCrashLog(string stage, Exception ex)
    {
        try
        {
            var directory = Path.Combine(Path.GetTempPath(), "ClassroomToolkit");
            Directory.CreateDirectory(directory);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{stage}] {ex}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(directory, "startup-crash.log"), line);
        }
        catch (Exception logEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(logEx))
        {
            // 最后退路：连崩溃日志都无法落盘时只能放弃；致命异常直接终止进程。
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
