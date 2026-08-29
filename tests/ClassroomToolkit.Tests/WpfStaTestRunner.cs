using System.Threading;
using FluentAssertions;
using WpfApplication = System.Windows.Application;
using WpfApp = ClassroomToolkit.App.App;

namespace ClassroomToolkit.Tests;

/// <summary>
/// Runs WPF UI test bodies on a single dedicated STA thread that owns the shared
/// <see cref="WpfApplication"/> instance. Unfrozen shared resources (e.g. DynamicResource-backed
/// DropShadowEffects) must never be touched from a second STA thread, so every test in the
/// "WPF UI" collection must marshal through this runner instead of creating its own thread.
/// </summary>
internal static class WpfStaTestRunner
{
    private static readonly object Gate = new();
    private static Thread? _thread;
    private static System.Windows.Threading.Dispatcher? _dispatcher;

    public static void Run(Action action)
    {
        lock (Gate)
        {
            EnsureStarted();
            Exception? failure = null;
            _dispatcher!.Invoke(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            failure.Should().BeNull();
        }
    }

    public static void EnsureApplication()
    {
        if (WpfApplication.Current is not WpfApp)
        {
            var app = new WpfApp
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
            app.InitializeComponent();
        }
    }

    private static void EnsureStarted()
    {
        if (_thread != null)
        {
            return;
        }

        using var ready = new ManualResetEventSlim(false);
        _thread = new Thread(() =>
        {
            try
            {
                EnsureApplication();
            }
            finally
            {
                _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                ready.Set();
            }
            System.Windows.Threading.Dispatcher.Run();
        });
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.IsBackground = true;
        _thread.Start();
        ready.Wait();
    }
}
