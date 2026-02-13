using WpfApplication = System.Windows.Application;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App;

public partial class App : WpfApplication
{
    private static readonly object LogWriteLock = new();
    private int _criticalDialogShowing;
    private IServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 注册全局异常处理
        RegisterGlobalExceptionHandlers();
        ConfigureServices();

        base.OnStartup(e);

        if (_services?.GetService<MainWindow>() is not MainWindow mainWindow)
        {
            throw new InvalidOperationException("MainWindow service is not configured.");
        }
        MainWindow = mainWindow;
        mainWindow.Show();

        // 在启动时立即修复所有 BorderBrush 问题
        try
        {
            GlobalBorderFixer.FixAllBordersImmediately();
        }
        catch (Exception ex)
        {
            LogException(ex, "GlobalBorderFixer Initial Fix");
        }

        // 注册全局 Border 修复
        BorderFixHelper.RegisterGlobalFix();
    }

    private void ConfigureServices()
    {
        var services = new ServiceCollection();
        var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");

        services.AddSingleton(_ => new AppSettingsService(settingsPath));
        services.AddSingleton(provider => provider.GetRequiredService<AppSettingsService>().Load());
        services.AddSingleton<ClassroomToolkit.App.ViewModels.MainViewModel>();
        services.AddSingleton<IRollCallWindowFactory, RollCallWindowFactory>();
        services.AddSingleton<Paint.IPaintWindowFactory, Paint.PaintWindowFactory>();
        services.AddSingleton<Photos.IImageManagerWindowFactory, Photos.ImageManagerWindowFactory>();
        services.AddSingleton<MainWindow>();

        _services = services.BuildServiceProvider();
    }

    private void RegisterGlobalExceptionHandlers()
    {
        // 1. UI 线程未捕获异常
        this.DispatcherUnhandledException += OnDispatcherUnhandledException;

        // 2. 非 UI 线程（线程池、后台线程）未捕获异常
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                HandleCriticalException(ex, "AppDomain.UnhandledException");
            }
        };

        // 3. Task（异步任务）未观察到的异常
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            HandleCriticalException(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved(); // 标记为已观察，防止进程退出（在某些 .NET 版本行为不同）
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (IsFatalException(e.Exception))
        {
            LogException(e.Exception, "Dispatcher.UnhandledException.Fatal");
            e.Handled = false;
            return;
        }

        e.Handled = true; // 非致命异常优先降级处理，防止应用直接崩溃
        HandleCriticalException(e.Exception, "Dispatcher.UnhandledException");
    }

    private void HandleCriticalException(Exception ex, string source)
    {
        LogException(ex, source);

        if (Dispatcher == null || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        // 弹窗提示用户（防止重入导致消息风暴）
        Dispatcher.BeginInvoke(() =>
        {
            if (Interlocked.Exchange(ref _criticalDialogShowing, 1) == 1)
            {
                return;
            }

            var message = $"程序遇到了未预期的错误 ({source}):\n\n{ex.Message}\n\n详细错误已记录到日志文件。";
            try
            {
                System.Windows.MessageBox.Show(message, "系统错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Interlocked.Exchange(ref _criticalDialogShowing, 0);
            }
        });
    }

    private void LogException(Exception ex, string source)
    {
        try
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(logPath)) Directory.CreateDirectory(logPath);

            var logFile = Path.Combine(logPath, $"error_{DateTime.Now:yyyyMMdd}.log");
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logContent = $"[{timestamp}] [{source}] {ex}\n" +
                             $"--------------------------------------------------------------------------------\n";

            lock (LogWriteLock)
            {
                File.AppendAllText(logFile, logContent);
            }
            System.Diagnostics.Debug.WriteLine($"[Exception][{source}] {ex.Message}");
        }
        catch
        {
            // 如果写日志也失败了，最后退路只有 Debug
            System.Diagnostics.Debug.WriteLine($"致命错误记录失败: {ex.Message}");
        }
    }

    private static bool IsFatalException(Exception ex)
    {
        return ex is OutOfMemoryException
            or AppDomainUnloadedException
            or BadImageFormatException
            or CannotUnloadAppDomainException
            or InvalidProgramException
            or StackOverflowException;
    }
}
