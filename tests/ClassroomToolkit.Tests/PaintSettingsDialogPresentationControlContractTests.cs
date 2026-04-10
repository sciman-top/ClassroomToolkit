using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintSettingsDialogPresentationControlContractTests
{
    [Fact]
    public void ConstructorAndConfirm_ShouldNotForcePresentationControlFlagsToTrue()
    {
        var source = ReadPaintSettingsDialogSources();

        source.Should().Contain("ControlMsPpt = settings.ControlMsPpt;");
        source.Should().Contain("ControlWpsPpt = settings.ControlWpsPpt;");
        source.Should().NotContain("ControlMsPpt = true;");
        source.Should().NotContain("ControlWpsPpt = true;");
    }

    private static string ReadPaintSettingsDialogSources()
    {
        return ContractSourceAggregationHelper.ReadSourcesInDirectory(
            ["src", "ClassroomToolkit.App", "Paint"],
            "PaintSettingsDialog*.cs");
    }
}
