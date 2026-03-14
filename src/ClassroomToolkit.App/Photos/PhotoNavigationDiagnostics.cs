using System;
using System.Diagnostics;

namespace ClassroomToolkit.App.Photos;

public static class PhotoNavigationDiagnostics
{
    // Enable via env var: CTK_PHOTO_NAV_TRACE=1
    private static readonly bool Enabled = string.Equals(
        Environment.GetEnvironmentVariable("CTK_PHOTO_NAV_TRACE"),
        "1",
        StringComparison.Ordinal);
    private static Func<DateTime> _nowProvider = static () => DateTime.Now;

    public static bool IsEnabled => Enabled;

    internal static IDisposable PushNowProviderForTest(Func<DateTime> nowProvider)
    {
        ArgumentNullException.ThrowIfNull(nowProvider);
        var previous = _nowProvider;
        _nowProvider = nowProvider;
        return new Scope(() => _nowProvider = previous);
    }

    public static void Log(string category, string message)
    {
        if (!Enabled)
        {
            return;
        }

        var timestamp = PhotoNavigationDiagnosticsTimestampPolicy.Format(_nowProvider());
        Debug.WriteLine($"[PhotoNav][{category}] {timestamp} {message}");
    }

    private sealed class Scope(Action onDispose) : IDisposable
    {
        private Action? _onDispose = onDispose;

        public void Dispose()
        {
            _onDispose?.Invoke();
            _onDispose = null;
        }
    }
}
