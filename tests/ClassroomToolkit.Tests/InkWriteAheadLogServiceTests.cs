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

    [Fact]
    public void RecoverDirectory_ShouldRethrowFatalException_WhenHashProviderThrowsFatal()
    {
        var sourcePath = Path.Combine(_tempDir, "lesson_fatal.png");
        File.WriteAllText(sourcePath, "x");
        var strokes = new List<InkStrokeData>
        {
            new()
            {
                Type = InkStrokeType.Shape,
                GeometryPath = "M0,0 L1,1",
                ColorHex = "#00FF00",
                Opacity = 255,
                BrushSize = 2
            }
        };
        _wal.Upsert(sourcePath, 1, strokes, "hash");

        var act = () => _wal.RecoverDirectory(
            _tempDir,
            _persistence,
            _ => throw new BadImageFormatException("fatal-hash-provider"));

        act.Should().Throw<BadImageFormatException>();
    }

    [Fact]
    public void Upsert_ShouldNotLeaveTempFile_WhenWalIsLocked()
    {
        var sourcePath = Path.Combine(_tempDir, "lesson_locked.png");
        File.WriteAllText(sourcePath, "x");
        var strokes = new List<InkStrokeData>
        {
            new()
            {
                Type = InkStrokeType.Shape,
                GeometryPath = "M0,0 L1,1",
                ColorHex = "#0000FF",
                Opacity = 255,
                BrushSize = 2
            }
        };
        var hash = ComputeInkHash(strokes);
        var walPath = Path.Combine(_tempDir, ".ctk-ink", ".ink-wal.json");
        Directory.CreateDirectory(Path.GetDirectoryName(walPath)!);
        File.WriteAllText(walPath, "{}");

        using (var lockStream = new FileStream(walPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // WAL 文件被占用时 Upsert + 强制落盘不得抛出，也不得残留临时文件。
            Action act = () =>
            {
                _wal.Upsert(sourcePath, 1, strokes, hash);
                _wal.FlushPending();
            };

            act.Should().NotThrow();
        }

        // 锁释放后的下一次落盘恢复正常。
        _wal.Upsert(sourcePath, 1, strokes, hash);
        _wal.FlushPending();
        File.Exists(walPath).Should().BeTrue();
        Directory.GetFiles(Path.GetDirectoryName(walPath)!, $"{Path.GetFileName(walPath)}.*.tmp").Should().BeEmpty();
    }

    [Fact]
    public async Task ConcurrentUpserts_ShouldPreserveEveryWalEntry()
    {
        const int entryCount = 32;
        var sources = Enumerable.Range(1, entryCount)
            .Select(index => Path.Combine(_tempDir, $"lesson_{index:D2}.png"))
            .ToArray();
        foreach (var sourcePath in sources)
        {
            File.WriteAllText(sourcePath, "x");
        }

        await Task.WhenAll(sources.Select((sourcePath, index) => Task.Run(() =>
        {
            var strokes = new List<InkStrokeData>
            {
                new()
                {
                    Type = InkStrokeType.Shape,
                    GeometryPath = $"M0,0 L{index + 1},{index + 1}",
                    ColorHex = "#123456",
                    Opacity = 255,
                    BrushSize = 2
                }
            };
            _wal.Upsert(sourcePath, 1, strokes, ComputeInkHash(strokes));
        })));

        var recovered = 0;
        for (var attempt = 0; attempt < 3 && recovered < entryCount; attempt++)
        {
            recovered += _wal.RecoverDirectory(
                _tempDir,
                _persistence,
                ComputeInkHash);
        }

        recovered.Should().Be(entryCount);
        foreach (var sourcePath in sources)
        {
            _persistence.LoadInkPageForFile(sourcePath, 1).Should().ContainSingle();
        }
    }


    [Fact]
    public void Remove_ShouldRetainTombstoneOnWriteFailure_AndNotResurrectPersistedStrokes()
    {
        var sourcePath = Path.Combine(_tempDir, "lesson_tombstone.png");
        File.WriteAllText(sourcePath, "x");
        var persistedStrokes = new List<InkStrokeData>
        {
            new()
            {
                Type = InkStrokeType.Shape,
                GeometryPath = "M5,5 L9,9",
                ColorHex = "#FFFFFF",
                Opacity = 255,
                BrushSize = 3
            }
        };
        // 场景前置：页面墨迹已成功持久化到 sidecar，WAL 中仍残留旧笔画记录。
        _persistence.SaveInkForFile(sourcePath, 1, persistedStrokes);
        var staleStrokes = new List<InkStrokeData>
        {
            new()
            {
                Type = InkStrokeType.Shape,
                GeometryPath = "M0,0 L1,1",
                ColorHex = "#000000",
                Opacity = 255,
                BrushSize = 2
            }
        };
        _wal.Upsert(sourcePath, 1, staleStrokes, ComputeInkHash(staleStrokes));
        _wal.FlushPending();

        var walPath = Path.Combine(_tempDir, ".ctk-ink", ".ink-wal.json");
        File.Exists(walPath).Should().BeTrue();

        using (var lockStream = new FileStream(walPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // WAL 被占用时 Remove 的合并写回失败：tombstone 必须留在内存等待重试，
            // 不能被静默丢弃后把旧笔画留在磁盘 WAL 里。
            var act = () => _wal.Remove(sourcePath, 1);

            act.Should().NotThrow();
        }

        // 解锁后重试成功：旧条目随空映射一起删除。
        _wal.FlushPending();
        File.Exists(walPath).Should().BeFalse();

        // 恢复流程不得回放旧笔画，也不得覆盖已持久化的新墨迹。
        var recovered = _wal.RecoverDirectory(_tempDir, _persistence, ComputeInkHash);
        recovered.Should().Be(0);
        var persisted = _persistence.LoadInkPageForFile(sourcePath, 1);
        persisted.Should().NotBeNull();
        ComputeInkHash(persisted!).Should().Be(ComputeInkHash(persistedStrokes));
    }

    [Fact]
    public void RecoverDirectory_ShouldPersistAcknowledgement_WhenWalCleanupIsLocked()
    {
        var sourcePath = Path.Combine(_tempDir, "lesson_recovery_ack.png");
        File.WriteAllText(sourcePath, "x");
        var staleStrokes = new List<InkStrokeData>
        {
            new()
            {
                Type = InkStrokeType.Shape,
                GeometryPath = "M0,0 L1,1",
                ColorHex = "#000000",
                Opacity = 255,
                BrushSize = 2
            }
        };
        _wal.Upsert(sourcePath, 1, staleStrokes, ComputeInkHash(staleStrokes));
        _wal.FlushPending();

        var walPath = Path.Combine(_tempDir, ".ctk-ink", ".ink-wal.json");
        var acknowledgementPath = Path.Combine(_tempDir, ".ctk-ink", ".ink-wal-ack.json");
        using (var lockStream = new FileStream(walPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var recovered = _wal.RecoverDirectory(_tempDir, _persistence, ComputeInkHash);
            recovered.Should().Be(1);
            File.Exists(acknowledgementPath).Should().BeTrue();

            var newerStrokes = new List<InkStrokeData>
            {
                new()
                {
                    Type = InkStrokeType.Shape,
                    GeometryPath = "M8,8 L9,9",
                    ColorHex = "#FF0000",
                    Opacity = 255,
                    BrushSize = 4
                }
            };
            _persistence.SaveInkForFile(sourcePath, 1, newerStrokes);

            using var restartedWhileLocked = new InkWriteAheadLogService();
            restartedWhileLocked.RecoverDirectory(_tempDir, _persistence, ComputeInkHash).Should().Be(0);
            ComputeInkHash(_persistence.LoadInkPageForFile(sourcePath, 1)!)
                .Should().Be(ComputeInkHash(newerStrokes));
        }

        using (var restartedAfterUnlock = new InkWriteAheadLogService())
        {
            restartedAfterUnlock.RecoverDirectory(_tempDir, _persistence, ComputeInkHash).Should().Be(0);
        }

        File.Exists(walPath).Should().BeFalse();
        File.Exists(acknowledgementPath).Should().BeFalse();
        ComputeInkHash(_persistence.LoadInkPageForFile(sourcePath, 1)!)
            .Should().Be(ComputeInkHash(new List<InkStrokeData>
            {
                new()
                {
                    Type = InkStrokeType.Shape,
                    GeometryPath = "M8,8 L9,9",
                    ColorHex = "#FF0000",
                    Opacity = 255,
                    BrushSize = 4
                }
            }));
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
