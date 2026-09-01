using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Settings;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

[Collection("WPF UI")]
public sealed class PaintSettingsDialogConstructionTests
{
    [Fact]
    public void Show_ShouldNotThrow_WithDefaultSettings()
    {
        PaintSettingsDialog? captured = null;

        WpfStaTestRunner.Run(() =>
        {
            WpfStaTestRunner.EnsureApplication();
            var dialog = new PaintSettingsDialog(new AppSettings());
            dialog.Show();
            dialog.UpdateLayout();
            dialog.Close();
            captured = dialog;
        });

        captured.Should().NotBeNull();
    }
}
