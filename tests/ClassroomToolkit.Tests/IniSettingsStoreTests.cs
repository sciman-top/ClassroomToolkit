using System.Text;
using ClassroomToolkit.Infra.Settings;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class IniSettingsStoreTests
{
    [Fact]
    public void TryLoad_ShouldDecodeLegacyGbAnsiFile()
    {
        var path = TestPathHelper.CreateFilePath("ctool_ini_gbk", ".ini");
        try
        {
            // 旧版 settings.ini 为 ANSI/GBK 编码且无 BOM；按 UTF-16 裸解会得到乱码，
            // 按本实现必须回退到 GB18030 正确解码。
            Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var content = "[General]\r\n教师姓名=张老师\r\n";
            File.WriteAllBytes(path, Encoding.GetEncoding("GB18030").GetBytes(content));
            var store = new IniSettingsStore(path);

            var loaded = store.TryLoad(out var data);

            loaded.Should().BeTrue();
            data["General"]["教师姓名"].Should().Be("张老师");
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
    public void Constructor_ShouldThrow_WhenPathIsBlank()
    {
        Action act = () => _ = new IniSettingsStore(" ");

        act.Should().Throw<ArgumentException>();
    }

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

    [Fact]
    public void TryLoad_ShouldFailForOversizedIniFile()
    {
        var path = TestPathHelper.CreateFilePath("ctool_ini_oversized", ".ini");
        try
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.SetLength(4L * 1024 * 1024 + 1);
            }
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

    [Fact]
    public void Save_ShouldPreserveUnknownAndMalformedNonEmptyLines()
    {
        var path = TestPathHelper.CreateFilePath("ctool_ini_preserve_unknown", ".ini");
        try
        {
            File.WriteAllText(
                path,
                "; user comment\n[Paint]\nbrush_base_size=8\nfuture-line-without-separator\n[Future]\nkey=value\n");
            var store = new IniSettingsStore(path);
            store.TryLoad(out var data).Should().BeTrue();
            data["Paint"]["brush_base_size"] = "9";

            store.Save(data);

            var saved = File.ReadAllText(path);
            saved.Should().Contain("; user comment");
            saved.Should().Contain("future-line-without-separator");
            saved.Should().Contain("[Future]");
            saved.Should().Contain("key=value");
            saved.Should().Contain("brush_base_size=9");
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
    public void Save_ShouldThrowArgumentNullException_WhenDataIsNull()
    {
        var path = TestPathHelper.CreateFilePath("ctool_ini_save_null", ".ini");
        var store = new IniSettingsStore(path);

        var act = () => store.Save(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Save_ShouldTreatNullSectionDictionary_AsEmptySection()
    {
        var path = TestPathHelper.CreateFilePath("ctool_ini_null_section", ".ini");
        try
        {
            var store = new IniSettingsStore(path);
            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Paint"] = null!
            };

            var act = () => store.Save(data);

            act.Should().NotThrow();
            var loaded = store.Load();
            loaded.Should().ContainKey("Paint");
            loaded["Paint"].Should().BeEmpty();
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
