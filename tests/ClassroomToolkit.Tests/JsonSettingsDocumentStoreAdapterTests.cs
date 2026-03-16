using ClassroomToolkit.Infra.Settings;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class JsonSettingsDocumentStoreAdapterTests
{
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

    private static string CreateTempDirectory()
    {
        return TestPathHelper.CreateDirectory("ctool_json_store");
    }
}
