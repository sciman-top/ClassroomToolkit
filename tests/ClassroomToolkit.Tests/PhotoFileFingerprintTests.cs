using System;
using System.IO;
using AwesomeAssertions;
using ClassroomToolkit.App.Photos;

namespace ClassroomToolkit.Tests;

public sealed class PhotoFileFingerprintTests
{
    [Fact]
    public void TryRead_ShouldDetectContentReplacement_WhenLengthAndTimestampStayTheSame()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_photo_fingerprint");
        var path = Path.Combine(rootPath, "student.png");
        var stableTimestamp = new DateTime(2026, 9, 10, 8, 30, 0, DateTimeKind.Utc);

        try
        {
            File.WriteAllBytes(path, [0x41, 0x41, 0x41, 0x41]);
            File.SetLastWriteTimeUtc(path, stableTimestamp);
            PhotoFileFingerprintReader.TryRead(path, out var first).Should().BeTrue();

            File.WriteAllBytes(path, [0x42, 0x42, 0x42, 0x42]);
            File.SetLastWriteTimeUtc(path, stableTimestamp);
            PhotoFileFingerprintReader.TryRead(path, out var second).Should().BeTrue();

            second.Length.Should().Be(first.Length);
            second.LastWriteTimeUtcTicks.Should().Be(first.LastWriteTimeUtcTicks);
            second.ContentHash.Should().NotBe(first.ContentHash);
            first.Matches(second).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void TryRead_ShouldTreatEquivalentContentAsTheSameFingerprint()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_photo_fingerprint_same");
        var path = Path.Combine(rootPath, "student.png");
        var stableTimestamp = new DateTime(2026, 9, 10, 8, 45, 0, DateTimeKind.Utc);

        try
        {
            File.WriteAllBytes(path, [0x10, 0x20, 0x30, 0x40]);
            File.SetLastWriteTimeUtc(path, stableTimestamp);
            PhotoFileFingerprintReader.TryRead(path, out var first).Should().BeTrue();

            File.WriteAllBytes(path, [0x10, 0x20, 0x30, 0x40]);
            File.SetLastWriteTimeUtc(path, stableTimestamp);
            PhotoFileFingerprintReader.TryRead(path, out var second).Should().BeTrue();

            first.Matches(second).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }
}
