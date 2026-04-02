using ClassroomToolkit.Services.Presentation;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationDiagnosticsProbeVersionTests
{
    [Fact]
    public void BuildProcessVersionDiagnosticLine_ShouldReturnReadableError_WhenProcessIdInvalid()
    {
        var line = PresentationDiagnosticsProbe.BuildProcessVersionDiagnosticLine("演示窗口版本", 0);

        line.Should().Be("演示窗口版本：无法读取（invalid-process-id）");
    }

    [Fact]
    public void BuildProcessVersionDiagnosticLine_ShouldFallbackLabel_WhenLabelEmpty()
    {
        var line = PresentationDiagnosticsProbe.BuildProcessVersionDiagnosticLine(string.Empty, 0);

        line.Should().StartWith("进程版本：无法读取（");
    }
}
