using WpfApplication = System.Windows.Application;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Threading;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Diagnostics;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Startup;
using ClassroomToolkit.Infra.Logging;
using ClassroomToolkit.App.UI.Themes;

namespace ClassroomToolkit.App;

public partial class App : WpfApplication
{
    internal const string StartupCompatibilityWarningShownPropertyKey = "StartupCompatibilityWarningShown";
    private static readonly object LogWriteLock = new();
    private static readonly ConfigurationService AppConfiguration = new();
    private static readonly string AppDataDirectory = ResolveAppDataDirectory(AppConfiguration);
    private static readonly LogRetentionOptions DefaultLogRetentionOptions = new();
    private int _criticalDialogShowing;
    private int _errorLogRetentionApplied;
    private int _errorLogRetentionSucceeded;
    private int _globalExceptionHandlersRegistered;
    private IServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 注册全局异常处理
        RegisterGlobalExceptionHandlers();

        // OnStartup 中途抛出的非致命异常会被全局处理器标记 Handled 并吞掉：
        // OnStartup 剩余步骤被中止但 Run() 继续泵消息，主窗口永不创建，
        // 进程以"无窗口僵尸"驻留。启动失败必须在此收口并显式退出。
        try
        {
            RunStartupSequence(e);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            LogException(ex, "App.OnStartup");
            try
            {
                System.Windows.MessageBox.Show(
                    $"程序启动失败：{ex.Message}\n\n详细错误已记录到日志文件。",
                    "启动错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception showEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(showEx))
            {
                // 连错误弹窗都无法显示时，直接退出仍优于僵尸驻留
            }
            Environment.Exit(-1);
        }
    }

    private void RunStartupSequence(StartupEventArgs e)
    {
        TryApplyErrorLogRetention();
        PhotoOverlayDiagnostics.InitializeSession(Path.Combine(AppDataDirectory, "logs"));
        ConfigureServices();

        base.OnStartup(e);

        // Startup may show modal warning dialogs before MainWindow exists.
        // Prevent WPF from treating that dialog as the last window and shutting down the app.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var services = _services ?? throw new InvalidOperationException("ServiceProvider is not configured.");

        var startupOrchestrator = new StartupOrchestrator(
            services,
            AppDataDirectory,
            Properties,
            StartupCompatibilityWarningShownPropertyKey,
            LogException);
        if (!startupOrchestrator.RunCompatibilityGate())
        {
            Shutdown(-1);
            return;
        }

        var settings = services.GetRequiredService<AppSettings>();
        var themeManager = services.GetRequiredService<ThemeManager>();
        themeManager.Apply(ThemePreferenceService.Parse(settings.UiTheme));

        if (services.GetService<MainWindow>() is not MainWindow mainWindow)
        {
            throw new InvalidOperationException("MainWindow service is not configured.");
        }
        MainWindow = mainWindow;
        mainWindow.Show();
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        AutoUpdateBootstrapper.Schedule(settings);

        // 注册全局 Border 修复（内部会立即修复已存在的主窗口，无需再单独全树遍历一次）
        BorderFixHelper.RegisterGlobalFix();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        UnregisterGlobalExceptionHandlers();
        (_services as IDisposable)?.Dispose();
        _services = null;
        base.OnExit(e);
    }

    private void ConfigureServices()
    {
        _services = AppCompositionRoot.Build(this, AppDataDirectory);
    }

    private void RegisterGlobalExceptionHandlers()
    {
        if (Interlocked.Exchange(ref _globalExceptionHandlersRegistered, 1) == 1)
        {
            return;
        }

        // 1. UI 线程未捕获异常
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // 2. 非 UI 线程（线程池、后台线程）未捕获异常
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

        // 3. Task（异步任务）未观察到的异常
        TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;
    }

    private void UnregisterGlobalExceptionHandlers()
    {
        if (Interlocked.Exchange(ref _globalExceptionHandlersRegistered, 0) == 0)
        {
            return;
        }

        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnTaskSchedulerUnobservedTaskException;
    }

    private void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception ex)
        {
            return;
        }

        HandleGlobalException(
            ex,
            "AppDomain.UnhandledException",
            AppGlobalExceptionHandlingPolicy.ResolveForBackground(ex));
    }

    private void OnTaskSchedulerUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        HandleGlobalException(
            e.Exception,
            "TaskScheduler.UnobservedTaskException",
            AppGlobalExceptionHandlingPolicy.ResolveForBackground(e.Exception));
        e.SetObserved(); // 标记为已观察，防止进程退出（在某些 .NET 版本行为不同）
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var decision = AppGlobalExceptionHandlingPolicy.ResolveForDispatcher(e.Exception);
        e.Handled = decision.ShouldMarkDispatcherHandled;
        HandleGlobalException(
            e.Exception,
            decision.IsFatal
                ? "Dispatcher.UnhandledException.Fatal"
                : "Dispatcher.UnhandledException",
            decision);
    }

    private void HandleGlobalException(
        Exception ex,
        string source,
        AppGlobalExceptionHandlingDecision decision)
    {
        LogException(ex, source);
        if (decision.Action != AppGlobalExceptionAction.NotifyUser)
        {
            return;
        }

        if (Dispatcher == null || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        void ShowGlobalErrorDialog()
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
        }

        var scheduled = false;
        // 弹窗提示用户（防止重入导致消息风暴）
        try
        {
            _ = Dispatcher.InvokeAsync(ShowGlobalErrorDialog);
            scheduled = true;
        }
        catch (Exception caughtEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(caughtEx))
        {
            // Keep fallback path below; no-op here.
        }
        if (!scheduled && Dispatcher.CheckAccess())
        {
            ShowGlobalErrorDialog();
        }
    }

    private void LogException(Exception ex, string source)
    {
        try
        {
            var logPath = Path.Combine(AppDataDirectory, "logs");
            if (!Directory.Exists(logPath)) Directory.CreateDirectory(logPath);
            TryApplyErrorLogRetention(logPath);

            var logFile = Path.Combine(logPath, $"error_{DateTime.Now:yyyyMMdd}.log");
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            var logContent = $"[{timestamp}] [{source}] {ex}\n" +
                             $"--------------------------------------------------------------------------------\n";

            lock (LogWriteLock)
            {
                File.AppendAllText(logFile, logContent);
            }
            System.Diagnostics.Debug.WriteLine($"[Exception][{source}] {ex.Message}");
        }
        catch (Exception caughtEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(caughtEx))
        {
            // 如果写日志也失败了，最后退路只有 Debug
            System.Diagnostics.Debug.WriteLine($"致命错误记录失败: {ex.Message}");
        }
    }

    private void TryApplyErrorLogRetention(string? logPath = null)
    {
        if (Volatile.Read(ref _errorLogRetentionSucceeded) == 1)
        {
            return;
        }

        if (Interlocked.Exchange(ref _errorLogRetentionApplied, 1) == 1)
        {
            return;
        }

        try
        {
            var resolvedLogPath = logPath ?? Path.Combine(AppDataDirectory, "logs");
            if (!Directory.Exists(resolvedLogPath))
            {
                Directory.CreateDirectory(resolvedLogPath);
            }

            LogRetentionPolicy.TryApply(
                resolvedLogPath,
                "error_",
                DateTime.Now,
                DefaultLogRetentionOptions);
            Volatile.Write(ref _errorLogRetentionSucceeded, 1);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine($"日志保留清理失败: {ex.Message}");
            Volatile.Write(ref _errorLogRetentionSucceeded, 0);
        }
        finally
        {
            Interlocked.Exchange(ref _errorLogRetentionApplied, 0);
        }
    }

    private static string ResolveAppDataDirectory(ConfigurationService configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var settingsPath = configuration.SettingsDocumentPath;
        if (!string.IsNullOrWhiteSpace(settingsPath))
        {
            var parent = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                return parent;
            }
        }

        var iniPath = configuration.SettingsIniPath;
        if (!string.IsNullOrWhiteSpace(iniPath))
        {
            var parent = Path.GetDirectoryName(iniPath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                return parent;
            }
        }

        return configuration.BaseDirectory;
    }

}
