using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using ClassroomToolkit.App.Helpers;

namespace ClassroomToolkit.App.Startup;

internal static class PortableUpdateBootstrapper
{
    private const string UpdateStateFileName = "portable-update-state.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    internal static void Schedule()
    {
        if (!TryLoadConfiguration(out var configuration)
            || !ShouldCheck(configuration.CheckIntervalHours))
        {
            return;
        }

        TryMarkCheckStarted();
        _ = Task.Run(() => CheckAndNotifyAsync(configuration));
    }

    private static async Task CheckAndNotifyAsync(PortableReleaseConfiguration configuration)
    {
        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ClassroomToolkit-Portable", configuration.Version));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            var json = await client.GetStringAsync(new Uri(configuration.LatestReleaseApiUrl, UriKind.Absolute)).ConfigureAwait(false);
            var release = JsonSerializer.Deserialize<GithubLatestRelease>(json, JsonOptions);
            if (release is null
                || release.Draft
                || release.Prerelease
                || !PortableReleaseVersion.IsNewer(configuration.Version, release.TagName))
            {
                return;
            }

            if (System.Windows.Application.Current is { } application)
            {
                await application.Dispatcher.InvokeAsync(() => NotifyNewRelease(configuration, release.TagName));
            }
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[PortableUpdate] check skipped: {ex.Message}");
        }
    }

    private static void NotifyNewRelease(PortableReleaseConfiguration configuration, string tagName)
    {
        // 主窗口与启动器气泡都是 Topmost；无 owner 的消息框会被压在下面，
        // 表现为“点了没反应”，必须经 TopmostMessageBox 抑制父窗置顶。
        var owner = System.Windows.Application.Current?.MainWindow;
        var result = owner != null
            ? Windowing.TopmostMessageBox.Show(
                owner,
                $"发现绿色便携版新版本 {tagName}。\n\n是否打开 GitHub 下载页面？",
                "课堂工具箱更新",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Information)
            : System.Windows.MessageBox.Show(
                $"发现绿色便携版新版本 {tagName}。\n\n是否打开 GitHub 下载页面？",
                "课堂工具箱更新",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Information);
        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = configuration.ReleasesPageUrl,
            UseShellExecute = true
        });
    }

    private static bool TryLoadConfiguration(out PortableReleaseConfiguration configuration)
    {
        configuration = PortableReleaseConfiguration.Empty;
        var root = PortableRuntimeContext.RootDirectory;
        if (string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        var path = Path.Combine(root, PortableRuntimeContext.ReleaseMetadataFileName);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var loaded = JsonSerializer.Deserialize<PortableReleaseConfiguration>(File.ReadAllText(path), JsonOptions);
            if (loaded?.IsValid != true)
            {
                return false;
            }

            configuration = loaded;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool ShouldCheck(int intervalHours)
    {
        var statePath = GetStatePath();
        if (string.IsNullOrWhiteSpace(statePath) || !File.Exists(statePath))
        {
            return true;
        }

        return DateTime.UtcNow - File.GetLastWriteTimeUtc(statePath) >= TimeSpan.FromHours(intervalHours);
    }

    private static void TryMarkCheckStarted()
    {
        var statePath = GetStatePath();
        if (string.IsNullOrWhiteSpace(statePath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
            File.WriteAllText(
                statePath,
                JsonSerializer.Serialize(new PortableUpdateState(DateTime.UtcNow), JsonOptions));
        }
        catch (IOException)
        {
            // A read-only portable drive must not block application startup.
        }
        catch (UnauthorizedAccessException)
        {
            // A read-only portable drive must not block application startup.
        }
    }

    private static string? GetStatePath()
    {
        return PortableRuntimeContext.DataDirectory is { } dataRoot
            ? Path.Combine(dataRoot, UpdateStateFileName)
            : null;
    }
}

internal static class PortableReleaseVersion
{
    internal static bool IsNewer(string currentVersion, string candidateTag)
    {
        return TryParse(currentVersion, out var current)
            && TryParse(candidateTag, out var candidate)
            && candidate > current;
    }

    internal static bool TryParse(string value, out Version version)
    {
        version = new Version();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var suffixIndex = normalized.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0)
        {
            normalized = normalized[..suffixIndex];
        }

        return Version.TryParse(normalized, out version!);
    }
}

internal sealed record PortableReleaseConfiguration(
    string Version,
    string LatestReleaseApiUrl,
    string ReleasesPageUrl,
    int CheckIntervalHours)
{
    internal static PortableReleaseConfiguration Empty { get; } = new(string.Empty, string.Empty, string.Empty, 0);

    internal bool IsValid => PortableReleaseVersion.TryParse(Version, out _)
        && IsHttpsUrl(LatestReleaseApiUrl)
        && IsHttpsUrl(ReleasesPageUrl)
        && CheckIntervalHours is >= 1 and <= 168;

    private static bool IsHttpsUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps;
    }
}

internal sealed record GithubLatestRelease(
    [property: JsonPropertyName("tag_name")] string TagName,
    [property: JsonPropertyName("draft")] bool Draft,
    [property: JsonPropertyName("prerelease")] bool Prerelease);

internal sealed record PortableUpdateState(DateTime LastCheckUtc);
