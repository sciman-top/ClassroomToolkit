using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDuplicateWindowReasonPolicyTests
{
    [Fact]
    public void ResolveDiagnosticTag_ShouldMapBackgroundAndInteraction()
    {
        CrossPageDuplicateWindowReasonPolicy.ResolveDiagnosticTag(
            CrossPageDuplicateWindowSkipReason.BackgroundRefresh)
            .Should().Be("background-duplicate-window");

        CrossPageDuplicateWindowReasonPolicy.ResolveDiagnosticTag(
            CrossPageDuplicateWindowSkipReason.Interaction)
            .Should().Be("interaction-duplicate-window");
    }

    [Fact]
    public void ResolveDiagnosticTag_ShouldFallbackToGenericTag()
    {
        CrossPageDuplicateWindowReasonPolicy.ResolveDiagnosticTag(
            CrossPageDuplicateWindowSkipReason.VisualSync)
            .Should().Be("duplicate-window");
    }
}
