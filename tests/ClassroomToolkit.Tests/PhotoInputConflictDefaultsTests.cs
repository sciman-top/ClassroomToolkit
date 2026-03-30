using System;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoInputConflictDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        PhotoInputConflictDefaults.SuppressWindowMinMs.Should().Be(0);
        PhotoInputConflictDefaults.UnsetTimestampUtc.Should().Be(DateTime.MinValue);
    }
}
