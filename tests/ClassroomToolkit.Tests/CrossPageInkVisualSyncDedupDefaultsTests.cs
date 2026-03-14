using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInkVisualSyncDedupDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        CrossPageInkVisualSyncDedupDefaults.DuplicateWindowMs.Should().Be(64);
    }
}
