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
    }
}
