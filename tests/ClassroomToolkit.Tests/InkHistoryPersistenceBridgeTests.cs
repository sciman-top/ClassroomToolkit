using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClassroomToolkit.App.Ink;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InkHistoryPersistenceBridgeTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public void LoadOrCreate_ShouldReturnSerializedStrokes_WhenSidecarHasPageData()
    {
        var persistence = new InkPersistenceService();
        var bridge = new InkHistoryPersistenceBridge(persistence);
        var sourcePath = TestPathHelper.CreateFilePath("ctool_ink_history_bridge_lesson_history", ".pptx");
        var expected = new List<InkStrokeData>
        {
            new()
            {
                GeometryPath = "M0,0 L10,10",
                ColorHex = "#FF112233",
                BrushSize = 6
            }
        };

        persistence.SaveInkForFile(sourcePath, 2, expected);

        var result = bridge.LoadOrCreate(sourcePath, 2);
        var loaded = JsonSerializer.Deserialize<List<InkStrokeData>>(result.StrokesJson!, JsonOptions);
        loaded.Should().NotBeNull();
        var loadedStrokes = loaded ?? new List<InkStrokeData>();

        result.SourcePath.Should().Be(sourcePath);
        result.PageIndex.Should().Be(2);
        result.CreatedTemplate.Should().BeFalse();
        loadedStrokes.Should().HaveCount(1);
        loadedStrokes[0].GeometryPath.Should().Be("M0,0 L10,10");
    }

    [Fact]
    public void Save_ShouldPersistJsonStrokes_ToSidecar()
    {
        var persistence = new InkPersistenceService();
        var bridge = new InkHistoryPersistenceBridge(persistence);
        var sourcePath = TestPathHelper.CreateFilePath("ctool_ink_history_bridge_save", ".pdf");
        var payload = JsonSerializer.Serialize(
            new List<InkStrokeData>
            {
                new()
                {
                    GeometryPath = "M1,1 L3,3",
                    ColorHex = "#FF445566",
                    BrushSize = 4
                }
            },
            JsonOptions);

        bridge.Save(sourcePath, 1, payload);

        var loaded = persistence.LoadInkPageForFile(sourcePath, 1);
        loaded.Should().NotBeNull();
        var loadedStrokes = loaded ?? new List<InkStrokeData>();
        loadedStrokes.Should().HaveCount(1);
        loadedStrokes[0].GeometryPath.Should().Be("M1,1 L3,3");
    }

    [Fact]
    public void Save_ShouldClearSidecarPage_WhenPayloadIsNull()
    {
        var persistence = new InkPersistenceService();
        var bridge = new InkHistoryPersistenceBridge(persistence);
        var sourcePath = TestPathHelper.CreateFilePath("ctool_ink_history_bridge_clear", ".pdf");
        persistence.SaveInkForFile(
            sourcePath,
            3,
            new List<InkStrokeData>
            {
                new()
                {
                    GeometryPath = "M2,2 L4,4",
                    ColorHex = "#FF778899",
                    BrushSize = 5
                }
            });

        bridge.Save(sourcePath, 3, null);

        var loaded = persistence.LoadInkPageForFile(sourcePath, 3);
        loaded.Should().BeNull();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
