using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPagePointerUpRefreshSourcePolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnInkSource_WhenInkOperationEnded()
    {
        CrossPagePointerUpRefreshSourcePolicy.Resolve(hadInkOperation: true)
            .Should().Be(CrossPagePointerUpRefreshSourcePolicy.PointerUpInk);
    }

    [Fact]
    public void Resolve_ShouldReturnDefaultSource_WhenNoInkOperation()
    {
        CrossPagePointerUpRefreshSourcePolicy.Resolve(hadInkOperation: false)
            .Should().Be(CrossPagePointerUpRefreshSourcePolicy.PointerUp);
    }
}
