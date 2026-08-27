using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace ClassroomToolkit.App.Startup;

internal static class AutoUpdateBootstrapper
{
    private const string UpdateFeedFileName = "update-feed.json";
    private const string UpdateStateFileName = "last-update-check-utc.txt";
    private const string AppDataFolderName = "ClassroomToolkit";
    private static readonly JsonSerializerOptions UpdateFeedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static void Schedule()
    {
        if (Helpers.PortableRuntimeContext.IsEnabled)
        {
            PortableUpdateBootstrapper.Schedule();
            return;
        }

        var configuration = TryLoadConfiguration();
        if (configuration is null || !configuration.Enabled || string.IsNullOrWhiteSpace(configuration.RepositoryUrl))
        {
            return;
        }

        _ = Task.Run(() => CheckAndDownloadAsync(configuration));
    }

    private static async Task CheckAndDownloadAsync(UpdateFeedConfiguration configuration)
    {
        try
        {
            if (!IsInstalledByVelopack() || !ShouldCheck(configuration.CheckIntervalHours))
            {
                return;
            }

            MarkCheckStarted();
            var source = new GithubSource(configuration.RepositoryUrl, null, prerelease: false, downloader: null);
            var updateManager = new UpdateManager(source);
            var update = await updateManager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (update is not null)
            {
                await updateManager.DownloadUpdatesAsync(update).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[AutoUpdate] check skipped: {ex.Message}");
        }
    }

    private static UpdateFeedConfiguration? TryLoadConfiguration()
    {
        var path = Path.Combine(AppContext.BaseDirectory, UpdateFeedFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var configuration = JsonSerializer.Deserialize<UpdateFeedConfiguration>(
                File.ReadAllText(path),
                UpdateFeedJsonOptions);
            return configuration?.IsValid == true ? configuration : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsInstalledByVelopack()
    {
        return Velopack.Locators.VelopackLocator.IsCurrentSet;
    }

    private static bool ShouldCheck(int intervalHours)
    {
        var statePath = GetStatePath();
        if (!File.Exists(statePath))
        {
            return true;
        }

        return DateTime.UtcNow - File.GetLastWriteTimeUtc(statePath) >= TimeSpan.FromHours(intervalHours);
    }

    private static void MarkCheckStarted()
    {
        var statePath = GetStatePath();
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        File.WriteAllText(statePath, DateTime.UtcNow.ToString("O"));
    }

    private static string GetStatePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, AppDataFolderName, UpdateStateFileName);
    }

}

internal sealed record UpdateFeedConfiguration(bool Enabled, string RepositoryUrl, int CheckIntervalHours)
{
    public bool IsValid => Uri.TryCreate(RepositoryUrl, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && CheckIntervalHours is >= 1 and <= 168;
}
