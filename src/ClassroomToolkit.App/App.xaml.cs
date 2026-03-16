using WpfApplication = System.Windows.Application;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Diagnostics;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.Application.Abstractions;
using ClassroomToolkit.Application.UseCases.RollCall;
using ClassroomToolkit.Infra.Settings;
using ClassroomToolkit.Infra.Storage;
using Microsoft.Extensions.Logging;

namespace ClassroomToolkit.App;

public partial class App : WpfApplication
{
    private static readonly object LogWriteLock = new();
    private static readonly string AppRootDirectory = new ConfigurationService().BaseDirectory;
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
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            LogException(ex, "GlobalBorderFixer Initial Fix");
        }

        // 注册全局 Border 修复
        BorderFixHelper.RegisterGlobalFix();
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
            var logPath = Path.Combine(AppRootDirectory, "logs");
            builder.AddProvider(new ClassroomToolkit.Infra.Logging.FileLoggerProvider(logPath));
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

    private void RegisterGlobalExceptionHandlers()
    {
        // 1. UI 线程未捕获异常
        this.DispatcherUnhandledException += OnDispatcherUnhandledException;

        // 2. 非 UI 线程（线程池、后台线程）未捕获异常
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                HandleGlobalException(
                    ex,
                    "AppDomain.UnhandledException",
                    AppGlobalExceptionHandlingPolicy.ResolveForBackground(ex));
            }
        };

        // 3. Task（异步任务）未观察到的异常
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            HandleGlobalException(
                e.Exception,
                "TaskScheduler.UnobservedTaskException",
                AppGlobalExceptionHandlingPolicy.ResolveForBackground(e.Exception));
            e.SetObserved(); // 标记为已观察，防止进程退出（在某些 .NET 版本行为不同）
        };
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

        // 弹窗提示用户（防止重入导致消息风暴）
        _ = Dispatcher.InvokeAsync(() =>
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
            var logPath = Path.Combine(AppRootDirectory, "logs");
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

}

