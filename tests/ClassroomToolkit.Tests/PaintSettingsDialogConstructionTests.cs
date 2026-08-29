using System.Threading;
using System.Windows;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Settings;
using FluentAssertions;
using WpfApplication = System.Windows.Application;

namespace ClassroomToolkit.Tests;

[Collection("WPF UI")]
public sealed class PaintSettingsDialogConstructionTests
{
    [Fact]
    public void Constructor_ShouldNotThrow_WithDefaultSettings()
    {
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                var app = WpfApplication.Current as ClassroomToolkit.App.App;
                if (app is null)
                {
                    app = new ClassroomToolkit.App.App
                    {
                        ShutdownMode = ShutdownMode.OnExplicitShutdown
                    };
                    app.InitializeComponent();
                }

                var dialog = new PaintSettingsDialog(new AppSettings());
                dialog.Close();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        exception.Should().BeNull();
    }

}
