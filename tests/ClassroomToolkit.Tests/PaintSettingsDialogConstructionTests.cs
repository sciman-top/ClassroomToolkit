using System.Threading;
using System.Windows;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Settings;
using FluentAssertions;
using WpfApplication = System.Windows.Application;

namespace ClassroomToolkit.Tests;

public sealed class PaintSettingsDialogConstructionTests
{
    [Fact]
    public void Constructor_ShouldNotThrow_WithDefaultSettings()
    {
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            WpfApplication? app = null;
            PaintSettingsDialog? dialog = null;
            try
            {
                app = new WpfApplication
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };
                AddThemeResources(app.Resources);
                dialog = new PaintSettingsDialog(new AppSettings());
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                dialog?.Close();
                app?.Shutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        exception.Should().BeNull();
    }

    private static void AddThemeResources(ResourceDictionary resources)
    {
        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/sciman Classroom Toolkit;component/Assets/Styles/Colors.xaml", UriKind.Absolute)
        });
        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/sciman Classroom Toolkit;component/Assets/Styles/Icons.xaml", UriKind.Absolute)
        });
        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/sciman Classroom Toolkit;component/Assets/Styles/WidgetStyles.xaml", UriKind.Absolute)
        });
    }
}
