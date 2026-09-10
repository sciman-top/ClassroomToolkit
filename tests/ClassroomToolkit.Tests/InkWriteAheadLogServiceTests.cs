using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClassroomToolkit.App.Ink;
using AwesomeAssertions;
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
        _wal.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void DisposedService_ShouldIgnoreFurtherMutations()
    {
        var sourcePath = Path.Combine(_tempDir, "lesson_disposed.png");
        File.WriteAllText(sourcePath, "x");
        using var wal = new InkWriteAheadLogService();
        wal.Dispose();

        var act = () =>
        {
            wal.Upsert(sourcePath, 1, Array.Empty<InkStrokeData>(), "hash");
            wal.Remove(sourcePath, 1);
            wal.FlushPending();
        };

        act.Should().NotThrow();
        Directory.Exists(Path.Combine(_tempDir, ".ctk-ink")).Should().BeFalse();
    }

    [Fact]
    public async Task Dispose_ShouldNotRaceWithConcurrentUpserts()
    {
        var sourcePath = Path.Combine(_tempDir, "lesson_dispose_race.png");
        File.WriteAllText(sourcePath, "x");
        using var wal = new InkWriteAheadLogService();
        var strokes = Array.Empty<InkStrokeData>();
        var cancellationToken = TestContext.Current.CancellationToken;

        var writer = Task.Run(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                wal.Upsert(sourcePath, 1, strokes, "hash");
            }
        }, cancellationToken);
        var disposer = Task.Run(wal.Dispose, cancellationToken);

        var act = async () => await Task.WhenAll(writer, disposer);

        await act.Should().NotThrowAsync();
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
    public void RecoverDirectory_ShouldNormalizeNullStrokeEntriesBeforeHashing()
    {
        var sourcePath = Path.Combine(_tempDir, "lesson_nullable_strokes.png");
        File.WriteAllText(sourcePath, "x");
        var walPath = Path.Combine(_tempDir, ".ctk-ink", ".ink-wal.json");
        Directory.CreateDirectory(Path.GetDirectoryName(walPath)!);
        File.WriteAllText(
            walPath,
            """
            {
              "lesson_nullable_strokes.png|1": {
                "sourcePath": "__SOURCE_PATH__",
                "pageIndex": 1,
                "hash": "empty",
                "updatedAt": "2026-09-05T00:00:00.0000000Z",
                "strokes": [null]
              }
            }
            """.Replace("__SOURCE_PATH__", sourcePath.Replace('\\', '/'), StringComparison.Ordinal));

        var recovered = _wal.RecoverDirectory(_tempDir, _persistence, ComputeInkHash);

        recovered.Should().Be(1);
        _persistence.LoadInkPageForFile(sourcePath, 1).Should().BeNull();
        File.Exists(walPath).Should().BeFalse();
    }

    [Fact]
    public void RecoverDirectory_ShouldIgnoreNullAcknowledgementEntry()
    {
        var sourcePath = Path.Combine(_tempDir, "lesson_null_acknowledgement.png");
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
        _wal.Upsert(sourcePath, 1, strokes, ComputeInkHash(strokes));
        _wal.FlushPending();

        var acknowledgementPath = Path.Combine(_tempDir, ".ctk-ink", ".ink-wal-ack.json");
        File.WriteAllText(
            acknowledgementPath,
            System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                [$"{sourcePath}|1"] = null
            }));

        var recovered = _wal.RecoverDirectory(_tempDir, _persistence, ComputeInkHash);

        recovered.Should().Be(1);
        _persistence.LoadInkPageForFile(sourcePath, 1).Should().ContainSingle();
        File.Exists(Path.Combine(_tempDir, ".ctk-ink", ".ink-wal.json")).Should().BeFalse();
        File.Exists(acknowledgementPath).Should().BeFalse();
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
    public void Dispose_ShouldKeepPendingSnapshotRetryable_WhenFinalFlushIsLocked()
    {
        var sourcePath = Path.Combine(_tempDir, "lesson_dispose_locked.png");
        File.WriteAllText(sourcePath, "x");
        var originalStrokes = new List<InkStrokeData>
        {
            new() { Type = InkStrokeType.Shape, GeometryPath = "M0,0 L1,1", ColorHex = "#FF0000", Opacity = 255, BrushSize = 2 }
        };
        var replacementStrokes = new List<InkStrokeData>
        {
            new() { Type = InkStrokeType.Shape, GeometryPath = "M2,2 L3,3", ColorHex = "#00FF00", Opacity = 255, BrushSize = 3 }
        };
        _wal.Upsert(sourcePath, 1, originalStrokes, ComputeInkHash(originalStrokes));
        _wal.FlushPending();

        var walPath = Path.Combine(_tempDir, ".ctk-ink", ".ink-wal.json");
        using (var lockStream = new FileStream(walPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            _wal.Upsert(sourcePath, 1, replacementStrokes, ComputeInkHash(replacementStrokes));
            _wal.Dispose();
        }

        // Dispose could not discard the in-memory entry. Once the lock is released,
        // the same service remains able to flush it and a restarted service can recover it.
        _wal.FlushPending();
        using var restarted = new InkWriteAheadLogService();
        restarted.RecoverDirectory(_tempDir, _persistence, ComputeInkHash).Should().Be(1);
        ComputeInkHash(_persistence.LoadInkPageForFile(sourcePath, 1)!)
            .Should().Be(ComputeInkHash(replacementStrokes));
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

        // 解锁后由 Remove 自己安排的重试成功：旧条目随空映射一起删除。
        SpinWait.SpinUntil(() => !File.Exists(walPath), TimeSpan.FromSeconds(3))
            .Should().BeTrue();

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

    [Fact]
    public void RecoverDirectory_ShouldRetainWal_WhenSidecarSaveReportsFailureEvenIfReadbackIsEmpty()
    {
        var sourcePath = Path.Combine(_tempDir, "lesson_recovery_save_failure.png");
        File.WriteAllText(sourcePath, "x");
        var existingStrokes = new List<InkStrokeData>
        {
            new()
            {
                Type = InkStrokeType.Shape,
                GeometryPath = "M1,1 L2,2",
                ColorHex = "#FFFFFF",
                Opacity = 255,
                BrushSize = 2
            }
        };
        _persistence.SaveInkForFile(sourcePath, 1, existingStrokes).Should().BeTrue();

        var walPath = Path.Combine(_tempDir, ".ctk-ink", ".ink-wal.json");
        _wal.Upsert(sourcePath, 1, Array.Empty<InkStrokeData>(), ComputeInkHash(Array.Empty<InkStrokeData>()));
        _wal.FlushPending();

        using (var sidecarLock = new FileStream(
                   InkPersistenceService.GetJsonPath(sourcePath),
                   FileMode.Open,
                   FileAccess.ReadWrite,
                   FileShare.None))
        {
            _wal.RecoverDirectory(_tempDir, _persistence, ComputeInkHash).Should().Be(0);
            File.Exists(walPath).Should().BeTrue();
            File.Exists(InkPersistenceService.GetJsonPath(sourcePath)).Should().BeTrue();
        }
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
