using ClassroomToolkit.App.Paint;
using Xunit;

namespace ClassroomToolkit.Tests;

public class CrossPageUpdateRequestStateUpdaterTests
{
    [Fact]
    public void ApplyAcceptedRequest_RuntimeState_ShouldReplaceSnapshot()
    {
        var state = CrossPageUpdateRequestRuntimeState.Default;
        var nowUtc = new DateTime(2026, 3, 7, 12, 0, 0, DateTimeKind.Utc);
        var request = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.ManipulationDelta);

        CrossPageUpdateRequestStateUpdater.ApplyAcceptedRequest(
            ref state,
            request,
            nowUtc);

        Assert.Equal(request, state.LastRequest);
        Assert.Equal(nowUtc, state.LastRequestUtc);
    }

    [Fact]
    public void ApplyAcceptedRequest_ShouldReplaceLastRequestAndTimestamp()
    {
        CrossPageUpdateRequestContext? lastRequest = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.Unspecified);
        var lastUtc = DateTime.MinValue;
        var nowUtc = new DateTime(2026, 3, 7, 12, 0, 0, DateTimeKind.Utc);
        var request = CrossPageUpdateRequestContextFactory.Create(CrossPageUpdateSources.ManipulationDelta);

        CrossPageUpdateRequestStateUpdater.ApplyAcceptedRequest(
            ref lastRequest,
            ref lastUtc,
            request,
            nowUtc);

        Assert.Equal(request, lastRequest);
        Assert.Equal(nowUtc, lastUtc);
    }
}
