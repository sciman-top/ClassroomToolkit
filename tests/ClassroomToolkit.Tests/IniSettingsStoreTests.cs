using System.Text;
using ClassroomToolkit.Infra.Settings;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class IniSettingsStoreTests
{
    [Fact]
    public void TryLoad_ShouldReadUtf16LeIniFile()
    {
        var path = TestPathHelper.CreateFilePath("ctool_ini_utf16", ".ini");
        try
        {
            var content = "[Paint]\r\ncontrol_ms_ppt=True\r\n";
            File.WriteAllText(path, content, Encoding.Unicode);
            var store = new IniSettingsStore(path);

            var loaded = store.TryLoad(out var data);

            loaded.Should().BeTrue();
            data.Should().ContainKey("Paint");
            data["Paint"]["control_ms_ppt"].Should().Be("True");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void TryLoad_ShouldFailForBinaryContentContainingNullByte()
    {
        var path = TestPathHelper.CreateFilePath("ctool_ini_binary", ".ini");
        try
        {
            File.WriteAllBytes(path, new byte[] { 0x5B, 0x50, 0x00, 0x61, 0x69, 0x6E, 0x74, 0x5D });
            var store = new IniSettingsStore(path);

            var loaded = store.TryLoad(out var data);

            loaded.Should().BeFalse();
            data.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
