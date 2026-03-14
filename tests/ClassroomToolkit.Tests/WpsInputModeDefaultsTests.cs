using ClassroomToolkit.App.Settings;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class WpsInputModeDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        WpsInputModeDefaults.Auto.Should().Be("auto");
        WpsInputModeDefaults.Raw.Should().Be("raw");
        WpsInputModeDefaults.Message.Should().Be("message");
    }
}
