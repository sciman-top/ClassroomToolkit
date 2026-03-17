using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowInteropRuntimeDefaultsTests
{
    [Fact]
    public void RetrySleepMs_ShouldMatchStabilizedValue()
    {
        WindowInteropRuntimeDefaults.RetrySleepMs.Should().Be(0);
    }
}
