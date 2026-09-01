using ClassroomToolkit.App.Settings;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class UiDefaultsBootstrapOptimizationPolicyTests
{
    [Fact]
    public void Resolve_ShouldSkip_WhenAlreadyOptimized()
    {
        var settings = new AppSettings
        {
            UiDefaultsVersion = UiDefaultsBootstrapOptimizationPolicy.CurrentVersion
        };

        var result = UiDefaultsBootstrapOptimizationPolicy.Resolve(settings);

        result.ShouldPersist.Should().BeFalse();
        result.InkPathOptimized.Should().BeFalse();
        result.LauncherPositionReset.Should().BeFalse();
        result.PaintToolbarPositionReset.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldResetLegacyPositions_AndMarkOptimized()
    {
        var settings = new AppSettings
        {
            UiDefaultsVersion = 0,
            InkPhotoRootPath = "   ",
            LauncherX = 120,
            LauncherY = 120,
            LauncherBubbleX = 120,
            LauncherBubbleY = 120,
            PaintToolbarX = 260,
            PaintToolbarY = 260
        };

        var result = UiDefaultsBootstrapOptimizationPolicy.Resolve(settings);

        result.ShouldPersist.Should().BeTrue();
        result.InkPathOptimized.Should().BeTrue();
        result.LauncherPositionReset.Should().BeTrue();
        result.PaintToolbarPositionReset.Should().BeTrue();
        settings.LauncherX.Should().Be(AppSettings.UnsetPosition);
        settings.LauncherY.Should().Be(AppSettings.UnsetPosition);
        settings.LauncherBubbleX.Should().Be(AppSettings.UnsetPosition);
        settings.LauncherBubbleY.Should().Be(AppSettings.UnsetPosition);
        settings.PaintToolbarX.Should().Be(AppSettings.UnsetPosition);
        settings.PaintToolbarY.Should().Be(AppSettings.UnsetPosition);
        settings.UiDefaultsVersion.Should().Be(UiDefaultsBootstrapOptimizationPolicy.CurrentVersion);
    }
}
