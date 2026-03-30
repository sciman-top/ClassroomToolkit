using ClassroomToolkit.App;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class AboutDialogTests
{
    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("http://example.com", true)]
    [InlineData("mailto:teacher@example.com", true)]
    [InlineData("file:///C:/Windows/notepad.exe", false)]
    [InlineData("javascript:alert('x')", false)]
    [InlineData("ms-settings:display", false)]
    public void IsAllowedExternalUri_ShouldRespectSchemeAllowList(string rawUri, bool expected)
    {
        var uri = new Uri(rawUri, UriKind.Absolute);

        var result = AboutDialog.IsAllowedExternalUri(uri);

        result.Should().Be(expected);
    }

    [Fact]
    public void IsAllowedExternalUri_ShouldRejectNull()
    {
        var result = AboutDialog.IsAllowedExternalUri(null);

        result.Should().BeFalse();
    }
}
