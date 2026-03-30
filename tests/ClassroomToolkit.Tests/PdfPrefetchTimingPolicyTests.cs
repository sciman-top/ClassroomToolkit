using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PdfPrefetchTimingPolicyTests
{
    [Fact]
    public void ResolveInitialDelayMs_ShouldReturnZero_WhenCrossPageEnabled()
    {
        PdfPrefetchTimingPolicy.ResolveInitialDelayMs(crossPageDisplayEnabled: true).Should().Be(0);
    }

    [Fact]
    public void ResolveInitialDelayMs_ShouldReturnConfiguredDelay_WhenCrossPageDisabled()
    {
        PdfPrefetchTimingPolicy.ResolveInitialDelayMs(crossPageDisplayEnabled: false)
            .Should().Be(PhotoDocumentRuntimeDefaults.PdfPrefetchDelayMs);
    }
}
