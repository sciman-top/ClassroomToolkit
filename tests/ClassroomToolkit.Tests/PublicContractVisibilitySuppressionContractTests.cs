using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PublicContractVisibilitySuppressionContractTests
{
    [Fact]
    public void GlobalSuppressions_ShouldDocumentIntentionalPublicContracts_ForCa1515()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "GlobalSuppressions.cs"));

        source.Should().Contain("CA1515:Consider making public types internal");
        source.Should().Contain("~T:ClassroomToolkit.App.Settings.AppSettings");
        source.Should().Contain("~T:ClassroomToolkit.App.Settings.SettingsDocumentFormat");
        source.Should().Contain("~T:ClassroomToolkit.App.Ink.InkDocumentData");
        source.Should().Contain("~T:ClassroomToolkit.App.Ink.InkExportOptions");
        source.Should().Contain("~T:ClassroomToolkit.App.Ink.InkStrokeData");
        source.Should().Contain("~T:ClassroomToolkit.App.Ink.InkPageData");
        source.Should().Contain("~T:ClassroomToolkit.App.Presentation.PresentationForegroundSource");
        source.Should().Contain("~T:ClassroomToolkit.App.Windowing.FloatingZOrderRequest");
        source.Should().Contain("~T:ClassroomToolkit.App.Windowing.ZOrderSurface");
        source.Should().Contain("~T:ClassroomToolkit.App.Session.UiSessionEvent");
        source.Should().Contain("~T:ClassroomToolkit.App.Session.UiSessionState");
        source.Should().Contain("~T:ClassroomToolkit.App.Session.UiSessionTransition");
        source.Should().Contain("~T:ClassroomToolkit.App.MainWindow");
        source.Should().Contain("~T:ClassroomToolkit.App.RollCallWindow");
        source.Should().Contain("~T:ClassroomToolkit.App.Paint.PaintOverlayWindow");
        source.Should().Contain("~T:ClassroomToolkit.App.Paint.PaintToolbarWindow");
        source.Should().Contain("~T:ClassroomToolkit.App.Photos.ImageManagerWindow");
        source.Should().Contain("~T:ClassroomToolkit.App.Photos.PhotoOverlayWindow");
        source.Should().Contain("~T:ClassroomToolkit.App.Photos.VirtualizingWrapPanel");
        source.Should().Contain("~T:ClassroomToolkit.App.Behaviors.LongPressBehavior");
        source.Should().Contain("~T:ClassroomToolkit.App.Controls.SafeBorder");
        source.Should().Contain("~T:ClassroomToolkit.App.Converters.InverseBooleanToVisibilityConverter");
        source.Should().Contain("~T:ClassroomToolkit.App.Photos.MultiplyConverter");
        source.Should().Contain("~T:ClassroomToolkit.App.ViewModels.ViewModelBase");
        source.Should().Contain("~T:ClassroomToolkit.App.ViewModels.MainViewModel");
        source.Should().Contain("~T:ClassroomToolkit.App.ViewModels.RollCallViewModel");
        source.Should().Contain("~T:ClassroomToolkit.App.IRollCallWindowFactory");
        source.Should().Contain("~T:ClassroomToolkit.App.Paint.IPaintWindowFactory");
        source.Should().Contain("~T:ClassroomToolkit.App.Ink.InkExportService");
        source.Should().Contain("~T:ClassroomToolkit.App.Photos.IImageManagerWindowFactory");
        source.Should().Contain("~T:ClassroomToolkit.App.Windowing.IWindowOrchestrator");
        source.Should().Contain("~T:ClassroomToolkit.App.Services.IPaintWindowOrchestrator");
    }
}
