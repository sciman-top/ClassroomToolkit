using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowInteropRetryDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        WindowInteropRetryDefaults.MaxRetryAttempts.Should().Be(2);
        WindowInteropRetryDefaults.ErrorInvalidWindowHandle.Should().Be(1400);
        WindowInteropRetryDefaults.ErrorInvalidHandle.Should().Be(6);
    }
}
