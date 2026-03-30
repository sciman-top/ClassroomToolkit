using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class StylusSampleTimestampStateUpdaterTests
{
    [Fact]
    public void Reset_ShouldClearState()
    {
        var state = new StylusSampleTimestampState(
            HasTimestamp: true,
            LastTimestampTicks: 123);

        StylusSampleTimestampStateUpdater.Reset(ref state);

        state.Should().Be(StylusSampleTimestampState.Default);
    }

    [Fact]
    public void Remember_ShouldIgnoreNonPositiveTimestamp()
    {
        var state = StylusSampleTimestampState.Default;

        StylusSampleTimestampStateUpdater.Remember(ref state, timestampTicks: 0);

        state.Should().Be(StylusSampleTimestampState.Default);
    }

    [Fact]
    public void Remember_ShouldStoreTimestamp()
    {
        var state = StylusSampleTimestampState.Default;

        StylusSampleTimestampStateUpdater.Remember(ref state, timestampTicks: 456);

        state.Should().Be(new StylusSampleTimestampState(
            HasTimestamp: true,
            LastTimestampTicks: 456));
    }
}
