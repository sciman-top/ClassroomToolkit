using WpfApplication = System.Windows.Application;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Linq;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.Application.Abstractions;
using ClassroomToolkit.Application.UseCases.RollCall;
using ClassroomToolkit.Infra.Settings;
using ClassroomToolkit.Infra.Storage;
using Microsoft.Extensions.Logging;
using ClassroomToolkit.Services.Compatibility;

namespace ClassroomToolkit.App;

public partial class App : WpfApplication
{
    private static readonly object LogWriteLock = new();
    private static readonly ConfigurationService AppConfiguration = new();
    private static readonly string AppRootDirectory = AppConfiguration.BaseDirectory;
    private static readonly string AppDataDirectory = ResolveAppDataDirectory(AppConfiguration);
    private int _criticalDialogShowing;
    private int _globalExceptionHandlersRegistered;
    private IServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 注册全局异常处理
        RegisterGlobalExceptionHandlers();
        ConfigureServices();

        base.OnStartup(e);

        var startupCompatibility = CollectStartupCompatibilityReport();
        var startupCompatibilityReportPath = PersistStartupCompatibilityReport(startupCompatibility);
        if (startupCompatibility.HasBlockingIssues)
        {
            var message = BuildStartupBlockingMessage(startupCompatibility, startupCompatibilityReportPath);
            System.Windows.MessageBox.Show(
                message,
                "启动环境不兼容",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }
        if (startupCompatibility.HasWarnings)
        {
            Debug.WriteLine($"[StartupCompatibility] {startupCompatibility.BuildMessage(includeWarnings: true)}");
            var warningMessage = BuildStartupWarningMessage(startupCompatibility, startupCompatibilityReportPath);
            System.Windows.MessageBox.Show(
                warningMessage,
                "启动兼容性提示",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

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
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            LogException(ex, "GlobalBorderFixer Initial Fix");
        }

        // 注册全局 Border 修复
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
        var services = new ServiceCollection();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<ISettingsDocumentStore>(provider =>
        {
            var configuration = provider.GetRequiredService<IConfigurationService>();
            var fallbackToIni = TryBootstrapSettingsDocumentMigration(configuration);
            if (fallbackToIni)
            {
                return new SettingsDocumentStoreAdapter(configuration.SettingsIniPath);
            }
            return configuration.SettingsDocumentFormat switch
            {
                SettingsDocumentFormat.Json => new JsonSettingsDocumentStoreAdapter(configuration.SettingsDocumentPath),
                _ => new SettingsDocumentStoreAdapter(configuration.SettingsDocumentPath)
            };
        });
        services.AddSingleton<IRollCallWorkbookStore>(_ =>
        {
            var store = RollCallWorkbookStoreResolver.Create(
                AppFlags.UseSqliteBusinessStore,
                AppFlags.EnableExperimentalSqliteBackend,
                out var selectedBackend);
            Debug.WriteLine(
                $"[Storage] StudentWorkbook backend selected={selectedBackend}, preferSqlite={AppFlags.UseSqliteBusinessStore}, experimentalSqlite={AppFlags.EnableExperimentalSqliteBackend}");
            return store;
        });
        services.AddSingleton<RollCallWorkbookUseCase>();
        services.AddSingleton<AppSettingsService>();
        services.AddSingleton(provider =>
        {
            var settingsService = provider.GetRequiredService<AppSettingsService>();
            var settings = settingsService.Load();
            var presetInitialization = Paint.PresetSchemeInitializationPolicy.Resolve(settings);
            var uiDefaultsInitialization = Settings.UiDefaultsBootstrapOptimizationPolicy.Resolve(settings);
            if (presetInitialization.ShouldPersist || uiDefaultsInitialization.ShouldPersist)
            {
                try
                {
                    settingsService.Save(settings);
                    Debug.WriteLine(
                        $"[PresetInit] persisted auto-init applied={presetInitialization.AppliedRecommendation} scheme={presetInitialization.FinalScheme} adaptiveSignal={presetInitialization.RecommendationHasAdaptiveSignal} reason={presetInitialization.RecommendationReason}");
                    Debug.WriteLine(
                        $"[UiDefaultsInit] persisted inkPathOptimized={uiDefaultsInitialization.InkPathOptimized} launcherReset={uiDefaultsInitialization.LauncherPositionReset} toolbarReset={uiDefaultsInitialization.PaintToolbarPositionReset} rollCallFontOptimized={uiDefaultsInitialization.RollCallFontOptimized}");
                }
                catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
                {
                    Debug.WriteLine(
                        $"[PresetInit] persist failed applied={presetInitialization.AppliedRecommendation} scheme={presetInitialization.FinalScheme} adaptiveSignal={presetInitialization.RecommendationHasAdaptiveSignal} reason={presetInitialization.RecommendationReason} error={ex.Message}");
                }
            }

            return settings;
        });
        services.AddSingleton<ClassroomToolkit.App.ViewModels.MainViewModel>();
        services.AddSingleton<IRollCallWindowFactory, RollCallWindowFactory>();
        services.AddSingleton<Paint.IPaintWindowFactory, Paint.PaintWindowFactory>();
        services.AddSingleton<Photos.IImageManagerWindowFactory, Photos.ImageManagerWindowFactory>();
        services.AddSingleton<Windowing.IWindowOrchestrator, Windowing.WindowOrchestrator>();
        services.AddSingleton<Services.IPaintWindowOrchestrator, Services.PaintWindowOrchestrator>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<ClassroomToolkit.Services.Input.GlobalHookService>();
        services.AddSingleton<ClassroomToolkit.Services.Speech.SpeechService>();
        services.AddSingleton<Ink.InkPersistenceService>();
        services.AddSingleton<Ink.InkExportOptions>();
        services.AddSingleton<Ink.InkExportService>();
        
        // Logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            // Console logger for development
            builder.AddConsole();
            
            // File logger for production/persistence
            var logPath = Path.Combine(AppDataDirectory, "logs");
            builder.AddProvider(new ClassroomToolkit.Infra.Logging.FileLoggerProvider(
                logPath,
                resetExistingLogsOnStartup: true));
        });

        _services = services.BuildServiceProvider();
    }

    private static bool TryBootstrapSettingsDocumentMigration(IConfigurationService configuration)
    {
        var decision = SettingsDocumentBootstrapMigrationPolicy.Resolve(
            configuration.SettingsDocumentFormat,
            File.Exists(configuration.SettingsDocumentPath),
            File.Exists(configuration.SettingsIniPath));
        var migrated = SettingsDocumentBootstrapMigrationExecutor.TryMigrate(
            decision,
            configuration.SettingsIniPath,
            configuration.SettingsDocumentPath,
            (iniPath, jsonPath, overwriteJson) =>
                new SettingsDocumentMigrationService().MigrateIniToJson(iniPath, jsonPath, overwriteJson).Migrated,
            message => Debug.WriteLine(message));

        var fallbackToIni = decision.ShouldMigrate && !migrated;
        if (fallbackToIni)
        {
            Debug.WriteLine(
                $"[SettingsMigration] bootstrap migration failed; fallback to INI source={configuration.SettingsIniPath}");
        }

        return fallbackToIni;
    }

    private StartupCompatibilityReport CollectStartupCompatibilityReport()
    {
        try
        {
            var configuration = _services?.GetService<IConfigurationService>();
            var settings = _services?.GetService<AppSettings>();
            var settingsPath = configuration?.SettingsDocumentPath
                ?? configuration?.SettingsIniPath
                ?? Path.Combine(AppDataDirectory, "settings.json");
            return StartupCompatibilityProbe.Collect(
                settingsPath,
                settings?.PresentationClassifierOverridesJson);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            LogException(ex, "StartupCompatibilityProbe");
            return new StartupCompatibilityReport(Array.Empty<StartupCompatibilityIssue>());
        }
    }

    private static string BuildStartupBlockingMessage(
        StartupCompatibilityReport report,
        string? reportPath)
    {
        var baseMessage = report.BuildMessage(includeWarnings: false);
        var hasPrivilegeMismatch =
            report.Issues.Any(static issue => issue.Code == "presentation-privilege-mismatch");
        if (!hasPrivilegeMismatch && string.IsNullOrWhiteSpace(reportPath))
        {
            return baseMessage;
        }

        var lines = new List<string> { baseMessage };
        if (hasPrivilegeMismatch)
        {
            lines.Add(string.Empty);
            lines.Add("快速修复：");
            lines.Add("1. 关闭本程序、PPT、WPS。");
            lines.Add("2. 统一以相同权限重启（都管理员或都非管理员）。");
            lines.Add("3. 若仍失败，优先使用离线标准包重装后重试。");
        }
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            lines.Add(string.Empty);
            lines.Add($"诊断报告：{reportPath}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildStartupWarningMessage(
        StartupCompatibilityReport report,
        string? reportPath)
    {
        var lines = new List<string>
        {
            "检测到可降级运行的兼容性风险：",
            report.BuildMessage(includeWarnings: true),
            string.Empty,
            "程序将继续启动。建议尽快按提示修复，避免课堂中断。"
        };
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            lines.Add($"诊断报告：{reportPath}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string? PersistStartupCompatibilityReport(StartupCompatibilityReport report)
    {
        try
        {
            var logPath = Path.Combine(AppDataDirectory, "logs");
            Directory.CreateDirectory(logPath);
            var filePath = Path.Combine(logPath, "startup-compatibility-latest.json");
            File.WriteAllText(filePath, report.ToJson());
            return filePath;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            LogException(ex, "StartupCompatibilityReportPersist");
            return null;
        }
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
        catch (Exception caughtEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(caughtEx))
        {
            // 如果写日志也失败了，最后退路只有 Debug
            System.Diagnostics.Debug.WriteLine($"致命错误记录失败: {ex.Message}");
        }
    }

    private static string ResolveAppDataDirectory(IConfigurationService configuration)
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
