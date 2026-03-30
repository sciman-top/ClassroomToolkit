using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoViewportStepPolicyTests
{
    [Fact]
    public void ResolveStep_ShouldUseMinStep_ForSmallViewport()
    {
        var value = PhotoViewportStepPolicy.ResolveStep(10);

        value.Should().Be(PhotoViewportStepPolicy.MinStepDip);
    }

    [Fact]
    public void ResolveStep_ShouldUseViewportBasedStep_ForNormalViewport()
    {
        var value = PhotoViewportStepPolicy.ResolveStep(1000);

        value.Should().Be(880);
    }
}
