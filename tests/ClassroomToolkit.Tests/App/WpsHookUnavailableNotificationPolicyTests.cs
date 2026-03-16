using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class WpsHookUnavailableNotificationPolicyTests
{
    [Fact]
    public void ShouldNotify_ShouldReturnTrueOnlyOnce_WithoutReset()
    {
        var state = 0;

        var first = WpsHookUnavailableNotificationPolicy.ShouldNotify(ref state);
        var second = WpsHookUnavailableNotificationPolicy.ShouldNotify(ref state);

        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldNotify_ShouldAllowSingleWinner_UnderConcurrency()
    {
        var state = 0;
        var results = new List<bool>();
        var gate = new SemaphoreSlim(1, 1);

        await Task.WhenAll(
            Enumerable.Range(0, 16).Select(async _ =>
            {
                var result = WpsHookUnavailableNotificationPolicy.ShouldNotify(ref state);
                await gate.WaitAsync();
                try
                {
                    results.Add(result);
                }
                finally
                {
                    gate.Release();
                }
            }));

        results.Count(x => x).Should().Be(1);
    }

    [Fact]
    public void Reset_ShouldRearmNotificationGate()
    {
        var state = 0;

        WpsHookUnavailableNotificationPolicy.ShouldNotify(ref state).Should().BeTrue();
        WpsHookUnavailableNotificationPolicy.ShouldNotify(ref state).Should().BeFalse();

        WpsHookUnavailableNotificationPolicy.Reset(ref state);

        WpsHookUnavailableNotificationPolicy.ShouldNotify(ref state).Should().BeTrue();
    }
}
