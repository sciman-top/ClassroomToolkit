using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

[Collection("WPF UI")]
public sealed class AppearanceDialogConstructionTests
{
    [Fact]
    public void Constructor_ShouldNotThrow_WithDefaultTheme()
    {
        ClassroomToolkit.App.Dialogs.AppearanceDialog? captured = null;

        WpfStaTestRunner.Run(() =>
        {
            WpfStaTestRunner.EnsureApplication();
            var dialog = new ClassroomToolkit.App.Dialogs.AppearanceDialog();
            dialog.Close();
            captured = dialog;
        });

        captured.Should().NotBeNull();
    }
}
