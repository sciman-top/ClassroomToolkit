using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDuplicateWindowCorePolicyTests
{
    [Fact]
    public void TryGetLastRequest_ShouldReturnFalse_WhenTimestampNotInitialized()
    {
        var current = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.PhotoPan);

        var canEvaluate = CrossPageDuplicateWindowCorePolicy.TryGetLastRequest(
            current,
            CrossPageRuntimeDefaults.UnsetTimestampUtc,
            out _);

        canEvaluate.Should().BeFalse();
    }

    [Fact]
    public void HasSameBaseSource_ShouldReturnTrue_WhenBaseSourceMatches()
    {
        var current = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.PhotoPan);
        var previous = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.WithDelayed(CrossPageUpdateSources.PhotoPan));

        CrossPageDuplicateWindowCorePolicy.HasSameBaseSource(current, previous).Should().BeTrue();
    }

    [Fact]
    public void IsWithinWindow_ShouldReturnTrue_WhenElapsedIsLessThanInterval()
    {
        var nowUtc = new DateTime(2026, 3, 7, 6, 0, 0, DateTimeKind.Utc);
        var within = CrossPageDuplicateWindowCorePolicy.IsWithinWindow(
            nowUtc,
            nowUtc.AddMilliseconds(-10),
            intervalMs: 24);

        within.Should().BeTrue();
    }
}
