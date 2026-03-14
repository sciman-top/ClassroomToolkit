using ClassroomToolkit.App;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class DialogShowResultStateUpdaterTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, false)]
    public void MarkFromDialogResult_ShouldMatchExpected(bool? dialogResult, bool expected)
    {
        var result = false;

        DialogShowResultStateUpdater.MarkFromDialogResult(ref result, dialogResult);

        result.Should().Be(expected);
    }
}
