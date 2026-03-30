using ClassroomToolkit.App;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowVisibleChangedPolicyTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ShouldEnsureVisible_ShouldMatchExpected(bool isVisible, bool expected)
    {
        var result = MainWindowVisibleChangedPolicy.ShouldEnsureVisible(isVisible);

        result.Should().Be(expected);
    }
}
