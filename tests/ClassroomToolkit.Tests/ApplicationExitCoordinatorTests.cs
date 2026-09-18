using AwesomeAssertions;
using ClassroomToolkit.App;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.ViewModels;
using ClassroomToolkit.Application.UseCases.RollCall;
using ClassroomToolkit.Infra.Settings;
using ClassroomToolkit.Infra.Storage;

namespace ClassroomToolkit.Tests;

public sealed class ApplicationExitCoordinatorTests
{
    [Fact]
    public void TryExit_ShouldKeepSessionAliveAndAllowRetry_WhenWorkbookSaveFails()
    {
        var root = TestPathHelper.CreateDirectory("exit-workbook");
        var path = Path.Combine(root, "students.xlsx");
        using var viewModel = new RollCallViewModel(path,
            new RollCallWorkbookUseCase(new RollCallWorkbookStoreAdapter()));
        viewModel.LoadData();
        var coordinator = new ApplicationExitCoordinator();
        var shutdown = false;
        using (var fileLock = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            coordinator.TryExit(() => true, () => throw new InvalidOperationException(),
                _ => viewModel.SaveState(), () => shutdown = true).Should().BeFalse();
            shutdown.Should().BeFalse();
            viewModel.IsDataReady.Should().BeTrue();
        }

        coordinator.TryExit(() => true, () => false, _ => viewModel.SaveState(),
            () => shutdown = true).Should().BeTrue();
        shutdown.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void TryExit_ShouldRequireConsentAndChildSave_WhenSettingsAreCorrupt(
        bool consent, bool childSave, bool expectedExit)
    {
        var path = TestPathHelper.CreateFilePath("exit-corrupt-settings", ".json");
        const string original = "{broken";
        File.WriteAllText(path, original);
        var service = new AppSettingsService(new JsonSettingsDocumentStoreAdapter(path));
        var settings = service.Load();
        service.IsOverwriteBlocked.Should().BeTrue();
        var shutdown = false;
        var prepareCalled = false;

        bool Save()
        {
            try { service.Save(settings); return true; }
            catch (InvalidOperationException) { return false; }
        }

        var result = new ApplicationExitCoordinator().TryExit(Save, () => consent,
            discard =>
            {
                prepareCalled = true;
                discard.Should().BeTrue();
                return childSave;
            }, () => shutdown = true);

        result.Should().Be(expectedExit);
        shutdown.Should().Be(expectedExit);
        prepareCalled.Should().Be(consent);
        File.ReadAllText(path).Should().Be(original);
    }

    [Fact]
    public void TryExit_ShouldRejectReentrantRequestDuringSaveDialog()
    {
        var coordinator = new ApplicationExitCoordinator();
        var shutdownCount = 0;
        coordinator.TryExit(() => false, () =>
        {
            coordinator.TryExit(() => true, () => true, _ => true,
                () => shutdownCount++).Should().BeFalse();
            return true;
        }, _ => true, () => shutdownCount++).Should().BeTrue();
        shutdownCount.Should().Be(1);
    }

    [Fact]
    public void SettingsOverwriteStatus_ShouldRecoverAfterExplicitSuccessfulReload()
    {
        var path = TestPathHelper.CreateFilePath("exit-settings-reload", ".json");
        File.WriteAllText(path, "{broken");
        var service = new AppSettingsService(new JsonSettingsDocumentStoreAdapter(path));
        service.Load();
        service.IsOverwriteBlocked.Should().BeTrue();
        File.WriteAllText(path, "{}");
        var settings = service.Load();
        service.IsOverwriteBlocked.Should().BeFalse();
        service.Save(settings);
    }
}
