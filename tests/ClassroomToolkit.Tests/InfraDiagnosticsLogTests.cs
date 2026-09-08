using AwesomeAssertions;
using ClassroomToolkit.Infra.Logging;

namespace ClassroomToolkit.Tests;

public sealed class InfraDiagnosticsLogTests
{
    [Fact]
    public void Write_ShouldForwardToRegisteredSink()
    {
        string? received = null;
        InfraDiagnosticsLog.SetSink(message => received = message);
        try
        {
            InfraDiagnosticsLog.Write("[StudentWorkbookStore] normalized backup failed");

            received.Should().Be("[StudentWorkbookStore] normalized backup failed");
        }
        finally
        {
            InfraDiagnosticsLog.SetSink(null);
        }
    }

    [Fact]
    public void Write_ShouldSwallowSinkFailures()
    {
        InfraDiagnosticsLog.SetSink(_ => throw new InvalidOperationException("sink-boom"));
        try
        {
            var act = () => InfraDiagnosticsLog.Write("[IniSettingsStore] load failed");

            act.Should().NotThrow("日志转发失败不得影响数据主链");
        }
        finally
        {
            InfraDiagnosticsLog.SetSink(null);
        }
    }

    [Fact]
    public void Write_ShouldNotThrow_WhenSinkUnset()
    {
        InfraDiagnosticsLog.SetSink(null);
        var act = () => InfraDiagnosticsLog.Write("[StudentWorkbookSqlite] snapshot write failed");

        act.Should().NotThrow();
    }
}
