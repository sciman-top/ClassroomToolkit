using ClassroomToolkit.Infra.Settings;
using FluentAssertions;
using System.Text;

namespace ClassroomToolkit.Tests;

public sealed class JsonSettingsDocumentStoreAdapterTests
{
    private const long MaxSettingsFileBytes = 4L * 1024 * 1024;

    [Fact]
    public void Constructor_ShouldThrow_WhenPathIsBlank()
    {
        Action act = () => _ = new JsonSettingsDocumentStoreAdapter(" ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Load_ShouldReturnEmpty_WhenFileMissing()
    {
        var path = TestPathHelper.CreateFilePath("ctool_json_store", ".json");
        var adapter = new JsonSettingsDocumentStoreAdapter(path);

        var data = adapter.Load();

        data.Should().BeEmpty();
    }

    [Fact]
    public void SaveAndLoad_ShouldRoundtripSections()
    {
        var tempDir = CreateTempDirectory();
        var path = Path.Combine(tempDir, "settings.json");
        try
        {
            var adapter = new JsonSettingsDocumentStoreAdapter(path);
            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["brush_base_size"] = "12",
                    ["brush_color"] = "#FF000000"
                },
                ["Launcher"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["x"] = "100",
                    ["y"] = "200"
                }
            };

            adapter.Save(data);
            var loaded = adapter.Load();

            loaded["Paint"]["brush_base_size"].Should().Be("12");
            loaded["Paint"]["brush_color"].Should().Be("#FF000000");
            loaded["Launcher"]["x"].Should().Be("100");
            loaded["Launcher"]["y"].Should().Be("200");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_ShouldNormalizeBooleanAndNumberValues_ToString()
    {
        var tempDir = CreateTempDirectory();
        var path = Path.Combine(tempDir, "settings.json");
        try
        {
            File.WriteAllText(
                path,
                """
                {
                  "Paint": {
                    "ink_cache_enabled": true,
                    "brush_base_size": 12.5
                  }
                }
                """);

            var adapter = new JsonSettingsDocumentStoreAdapter(path);
            var loaded = adapter.Load();

            loaded["Paint"]["ink_cache_enabled"].Should().Be("True");
            loaded["Paint"]["brush_base_size"].Should().Be("12.5");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Save_ShouldThrow_WhenPreviousLoadFailedAndTargetExists()
    {
        var tempDir = CreateTempDirectory();
        var path = Path.Combine(tempDir, "settings.json");
        try
        {
            File.WriteAllText(path, "{invalid-json");
            var adapter = new JsonSettingsDocumentStoreAdapter(path);

            var loaded = adapter.Load();
            loaded.Should().BeEmpty();

            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["brush_base_size"] = "12"
                }
            };

            Action act = () => adapter.Save(data);

            act.Should().Throw<InvalidOperationException>();
            File.ReadAllText(path).Should().Be("{invalid-json");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Save_ShouldThrow_WhenExistingJsonIsCorrupt_WithoutPriorLoad()
    {
        var tempDir = CreateTempDirectory();
        var path = Path.Combine(tempDir, "settings.json");
        try
        {
            File.WriteAllText(path, "{invalid-json");
            var original = File.ReadAllText(path);
            var adapter = new JsonSettingsDocumentStoreAdapter(path);
            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["brush_base_size"] = "15"
                }
            };

            Action act = () => adapter.Save(data);

            act.Should().Throw<InvalidOperationException>();
            File.ReadAllText(path).Should().Be(original);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Save_ShouldThrow_WhenValidatedJsonBecomesCorrupt_BeforeSave()
    {
        var tempDir = CreateTempDirectory();
        var path = Path.Combine(tempDir, "settings.json");
        try
        {
            File.WriteAllText(path, "{\"Paint\":{\"brush_base_size\":\"12\"}}");
            var adapter = new JsonSettingsDocumentStoreAdapter(path);

            var loaded = adapter.Load();
            loaded["Paint"]["brush_base_size"].Should().Be("12");

            File.WriteAllText(path, "{invalid-json");
            var original = File.ReadAllText(path);
            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["brush_base_size"] = "21"
                }
            };

            Action act = () => adapter.Save(data);

            act.Should().Throw<InvalidOperationException>();
            File.ReadAllText(path).Should().Be(original);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Save_ShouldThrow_WhenValidatedJsonChangesWithoutTimestampDrift()
    {
        var tempDir = CreateTempDirectory();
        var path = Path.Combine(tempDir, "settings.json");
        try
        {
            File.WriteAllText(path, "{\"Paint\":{\"brush_base_size\":\"12\"}}");
            var adapter = new JsonSettingsDocumentStoreAdapter(path);

            var loaded = adapter.Load();
            loaded["Paint"]["brush_base_size"].Should().Be("12");

            var validatedWriteTimeUtc = File.GetLastWriteTimeUtc(path);
            File.WriteAllText(path, "{invalid-json");
            File.SetLastWriteTimeUtc(path, validatedWriteTimeUtc);
            var original = File.ReadAllText(path);
            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["brush_base_size"] = "22"
                }
            };

            Action act = () => adapter.Save(data);

            act.Should().Throw<InvalidOperationException>();
            File.ReadAllText(path).Should().Be(original);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Save_ShouldNotThrow_WhenPreviousLoadFailedDueToTransientIoError()
    {
        var tempDir = CreateTempDirectory();
        var path = Path.Combine(tempDir, "settings.json");
        try
        {
            File.WriteAllText(path, "{\"Paint\":{\"brush_base_size\":\"12\"}}");
            var adapter = new JsonSettingsDocumentStoreAdapter(path);
            using var lockStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

            var loaded = adapter.Load();
            loaded.Should().BeEmpty();

            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["brush_base_size"] = "18"
                }
            };

            lockStream.Dispose();
            Action act = () => adapter.Save(data);

            act.Should().NotThrow();
            var saved = adapter.Load();
            saved["Paint"]["brush_base_size"].Should().Be("18");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Save_ShouldRecoverAfterNonObjectJsonLoad()
    {
        var tempDir = CreateTempDirectory();
        var path = Path.Combine(tempDir, "settings.json");
        try
        {
            var adapter = new JsonSettingsDocumentStoreAdapter(path);
            File.WriteAllText(path, "{invalid-json");
            _ = adapter.Load();

            File.WriteAllText(path, "[]");
            var loaded = adapter.Load();
            loaded.Should().BeEmpty();

            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["brush_base_size"] = "20"
                }
            };

            Action act = () => adapter.Save(data);

            act.Should().NotThrow();
            var saved = adapter.Load();
            saved["Paint"]["brush_base_size"].Should().Be("20");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_ShouldReturnEmpty_AndBlockOverwrite_WhenJsonExceedsSizeLimit()
    {
        var tempDir = CreateTempDirectory();
        var path = Path.Combine(tempDir, "settings.json");
        try
        {
            WriteOversizedJson(path);
            new FileInfo(path).Length.Should().BeGreaterThan(MaxSettingsFileBytes);

            var adapter = new JsonSettingsDocumentStoreAdapter(path);
            var loaded = adapter.Load();
            loaded.Should().BeEmpty();

            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["brush_base_size"] = "20"
                }
            };

            Action act = () => adapter.Save(data);
            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Save_ShouldThrow_WhenExistingJsonExceedsSizeLimit_WithoutPriorLoad()
    {
        var tempDir = CreateTempDirectory();
        var path = Path.Combine(tempDir, "settings.json");
        try
        {
            WriteOversizedJson(path);
            var adapter = new JsonSettingsDocumentStoreAdapter(path);
            var originalLength = new FileInfo(path).Length;

            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["brush_base_size"] = "21"
                }
            };

            Action act = () => adapter.Save(data);
            act.Should().Throw<InvalidOperationException>();
            new FileInfo(path).Length.Should().Be(originalLength);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Save_ShouldTreatNullSectionDictionary_AsEmptySection()
    {
        var tempDir = CreateTempDirectory();
        var path = Path.Combine(tempDir, "settings.json");
        try
        {
            var adapter = new JsonSettingsDocumentStoreAdapter(path);
            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Paint"] = null!
            };

            Action act = () => adapter.Save(data);

            act.Should().NotThrow();
            var loaded = adapter.Load();
            loaded.Should().ContainKey("Paint");
            loaded["Paint"].Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_ShouldReturnEmpty_WhenPathIsInvalid()
    {
        var adapter = new JsonSettingsDocumentStoreAdapter("\0invalid-path.json");

        var act = () => adapter.Load();

        var data = act.Should().NotThrow().Subject;
        data.Should().BeEmpty();
    }

    private static string CreateTempDirectory()
    {
        return TestPathHelper.CreateDirectory("ctool_json_store");
    }

    private static void WriteOversizedJson(string path)
    {
        const int targetPayloadChars = 4 * 1024 * 1024 + 1024;
        const string prefix = "{\"Paint\":{\"payload\":\"";
        const string suffix = "\"}}";

        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(prefix);

        var chunk = new string('a', 8192);
        var remaining = targetPayloadChars;
        while (remaining > 0)
        {
            var take = Math.Min(remaining, chunk.Length);
            writer.Write(chunk.AsSpan(0, take));
            remaining -= take;
        }

        writer.Write(suffix);
    }
}
