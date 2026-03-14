using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClassroomToolkit.App.Ink;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InkWriteAheadLogServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly InkWriteAheadLogService _wal = new();
    private readonly InkPersistenceService _persistence = new();

    public InkWriteAheadLogServiceTests()
    {
        _tempDir = TestPathHelper.CreateDirectory("ctk_wal_test");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void RecoverDirectory_ShouldReplayPendingPageAndClearWal()
    {
        var sourcePath = Path.Combine(_tempDir, "lesson.png");
        File.WriteAllText(sourcePath, "x");
        var strokes = new List<InkStrokeData>
        {
            new()
            {
                Type = InkStrokeType.Shape,
                GeometryPath = "M0,0 L1,1",
                ColorHex = "#FF0000",
                Opacity = 255,
                BrushSize = 2
            }
        };
        var hash = ComputeInkHash(strokes);
        _wal.Upsert(sourcePath, 1, strokes, hash);

        var recovered = _wal.RecoverDirectory(
            _tempDir,
            _persistence,
            ComputeInkHash);

        recovered.Should().Be(1);
        var persisted = _persistence.LoadInkPageForFile(sourcePath, 1);
        persisted.Should().NotBeNull();
        persisted!.Count.Should().Be(1);
        ComputeInkHash(persisted).Should().Be(hash);
    }

    private static string ComputeInkHash(IReadOnlyList<InkStrokeData> strokes)
    {
        if (strokes == null || strokes.Count == 0)
        {
            return "empty";
        }

        var raw = string.Join('|', strokes.Select(s =>
            $"{s.Type},{s.BrushStyle},{s.ColorHex},{s.Opacity},{s.BrushSize},{s.GeometryPath}"));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));
    }
}
