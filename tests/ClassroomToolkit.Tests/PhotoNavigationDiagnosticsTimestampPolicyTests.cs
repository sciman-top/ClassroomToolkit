using ClassroomToolkit.App.Photos;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoNavigationDiagnosticsTimestampPolicyTests
{
    [Fact]
    public void Format_ShouldUseFixedTimePattern()
    {
        var localTimestamp = new DateTime(2026, 3, 7, 13, 14, 15, 123, DateTimeKind.Local);

        var formatted = PhotoNavigationDiagnosticsTimestampPolicy.Format(localTimestamp);

        formatted.Should().Be("13:14:15.123");
    }
}
