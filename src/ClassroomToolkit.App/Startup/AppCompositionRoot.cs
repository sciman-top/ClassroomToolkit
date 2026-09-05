using System.Diagnostics;
using System.IO;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.UI.Themes;
using ClassroomToolkit.Application.Abstractions;
using ClassroomToolkit.Application.UseCases.RollCall;
using ClassroomToolkit.Infra.Logging;
using ClassroomToolkit.Infra.Settings;
using ClassroomToolkit.Infra.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WpfApplication = System.Windows.Application;

namespace ClassroomToolkit.App.Startup;

/// <summary>
/// Owns the application's composition root. Feature registration stays here so the WPF
/// lifecycle does not need to know how a feature is assembled or which adapter it selects.
/// </summary>
internal static class AppCompositionRoot
{
    internal static IServiceProvider Build(WpfApplication application, string appDataDirectory)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentException.ThrowIfNullOrWhiteSpace(appDataDirectory);

        var services = new ServiceCollection();
        AddConfigurationAndSettings(services);
        AddBusinessStorage(services);
        AddApplicationSettings(services, application);
        AddWindows(services);
        AddRuntimeServices(services);
        AddLogging(services, appDataDirectory);
        return services.BuildServiceProvider();
    }

    private static void AddConfigurationAndSettings(IServiceCollection services)
    {
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
    }

    private static void AddBusinessStorage(IServiceCollection services)
    {
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
    }

    private static void AddApplicationSettings(IServiceCollection services, WpfApplication application)
    {
        services.AddSingleton<RollCallWorkbookUseCase>();
        services.AddSingleton<AppSettingsService>();
        services.AddSingleton<ThemeManager>(_ => new ThemeManager(application));
        services.AddSingleton(provider =>
        {
            var settingsService = provider.GetRequiredService<AppSettingsService>();
            var settings = settingsService.Load();
            var presetInitialization = PresetSchemeInitializationPolicy.Resolve(settings);
            var uiDefaultsInitialization = UiDefaultsBootstrapOptimizationPolicy.Resolve(settings);
            if (presetInitialization.ShouldPersist || uiDefaultsInitialization.ShouldPersist)
            {
                try
                {
                    settingsService.Save(settings);
                    Debug.WriteLine(
                        $"[PresetInit] persisted auto-init applied={presetInitialization.AppliedRecommendation} scheme={presetInitialization.FinalScheme} adaptiveSignal={presetInitialization.RecommendationHasAdaptiveSignal} reason={presetInitialization.RecommendationReason}");
                    Debug.WriteLine(
                        $"[UiDefaultsInit] persisted inkPathOptimized={uiDefaultsInitialization.InkPathOptimized} launcherReset={uiDefaultsInitialization.LauncherPositionReset} toolbarReset={uiDefaultsInitialization.PaintToolbarPositionReset}");
                }
                catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
                {
                    Debug.WriteLine(
                        $"[PresetInit] persist failed applied={presetInitialization.AppliedRecommendation} scheme={presetInitialization.FinalScheme} adaptiveSignal={presetInitialization.RecommendationHasAdaptiveSignal} reason={presetInitialization.RecommendationReason} error={ex.Message}");
                }
            }

            return settings;
        });
    }

    private static void AddWindows(IServiceCollection services)
    {
        services.AddSingleton<ClassroomToolkit.App.ViewModels.MainViewModel>();
        services.AddSingleton<IRollCallWindowFactory, RollCallWindowFactory>();
        services.AddSingleton<IPaintWindowFactory, PaintWindowFactory>();
        services.AddSingleton<Photos.IImageManagerWindowFactory, Photos.ImageManagerWindowFactory>();
        services.AddSingleton<Windowing.IWindowOrchestrator, Windowing.WindowOrchestrator>();
        services.AddSingleton<Services.IPaintWindowOrchestrator, Services.PaintWindowOrchestrator>();
        services.AddSingleton<MainWindow>();
    }

    private static void AddRuntimeServices(IServiceCollection services)
    {
        services.AddSingleton<ClassroomToolkit.Services.Input.GlobalHookService>();
        services.AddSingleton<ClassroomToolkit.Services.Speech.SpeechService>();
        services.AddSingleton<Ink.InkPersistenceService>();

        var useInkHistorySqlite = AppFlags.UseSqliteBusinessStore
            && BusinessStorageBackendCapabilityPolicy.IsSqliteAvailable(AppFlags.EnableExperimentalSqliteBackend);
        Debug.WriteLine(
            $"[Storage] InkHistory backend selected={(useInkHistorySqlite ? "Sqlite" : "Sidecar")}, preferSqlite={AppFlags.UseSqliteBusinessStore}, experimentalSqlite={AppFlags.EnableExperimentalSqliteBackend}");
        if (useInkHistorySqlite)
        {
            services.AddSingleton<IInkHistorySnapshotStore>(provider =>
            {
                var persistence = provider.GetRequiredService<Ink.InkPersistenceService>();
                var bridge = new Ink.InkHistoryPersistenceBridge(persistence);
                var sqliteAdapter = new InkHistorySqliteStoreAdapter(bridge);
                return new InkHistorySnapshotStoreAdapter(sqliteAdapter);
            });
        }

        services.AddSingleton<Ink.InkExportOptions>();
        services.AddSingleton<Ink.InkExportService>();
    }

    private static void AddLogging(IServiceCollection services, string appDataDirectory)
    {
        services.AddLogging(builder =>
        {
#if DEBUG
            builder.SetMinimumLevel(LogLevel.Debug);
#else
            builder.SetMinimumLevel(LogLevel.Information);
#endif
            builder.AddConsole();
            builder.AddProvider(new FileLoggerProvider(
                Path.Combine(appDataDirectory, "logs"),
                resetExistingLogsOnStartup: false));
        });
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
}
