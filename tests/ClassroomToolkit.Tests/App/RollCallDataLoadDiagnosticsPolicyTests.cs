using ClassroomToolkit.App.ViewModels;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class RollCallDataLoadDiagnosticsPolicyTests
{
    [Fact]
    public void FormatFileWriteTimeReadFailure_ShouldContainKeyFields()
    {
        var message = RollCallDataLoadDiagnosticsPolicy.FormatFileWriteTimeReadFailure(
            @"C:\temp\students.xlsx",
            "IOException",
            "locked");

        message.Should().Contain("[RollCallDataLoad] file-write-time-read-failed");
        message.Should().Contain(@"path=C:\temp\students.xlsx");
        message.Should().Contain("ex=IOException");
        message.Should().Contain("msg=locked");
    }

    [Fact]
    public void FormatPreloadConsumeFailure_ShouldContainKeyFields()
    {
        var message = RollCallDataLoadDiagnosticsPolicy.FormatPreloadConsumeFailure(
            @"C:\temp\students.xlsx",
            "InvalidOperationException",
            "faulted");

        message.Should().Contain("[RollCallDataLoad] preload-consume-failed");
        message.Should().Contain(@"path=C:\temp\students.xlsx");
        message.Should().Contain("ex=InvalidOperationException");
        message.Should().Contain("msg=faulted");
    }
}
