using System.Globalization;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkPresentationSnapshotTests
{
    [Fact]
    public void SavePresentationSnapshot_ShouldCreateDatedFolderAndWriteBytes()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_ink_presentation");
        try
        {
            var service = new InkStorageService(rootPath);
            var date = new DateTime(2026, 9, 10);
            var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0A, 0x11 };

            var path = service.SavePresentationSnapshot(date, "presentation_20260910_080000000.png", pngBytes);

            File.Exists(path).Should().BeTrue();
            path.Should().Contain($"20260910{Path.DirectorySeparatorChar}presentation");
            File.ReadAllBytes(path).Should().Equal(pngBytes);
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
    public void SavePresentationSnapshot_ShouldSanitizeFileName()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_ink_presentation_bad");
        try
        {
            var service = new InkStorageService(rootPath);

            var path = service.SavePresentationSnapshot(DateTime.Today, "bad<name>.png", new byte[] { 1 });

            Path.GetFileName(path).Should().NotContain("<");
            File.Exists(path).Should().BeTrue();
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

public sealed class PresentationInkExitSnapshotPolicyTests
{
    [Fact]
    public void ShouldCapture_ShouldReturnTrue_WhenDrawingExists()
    {
        PresentationInkExitSnapshotPolicy.ShouldCapture(hasDrawing: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldCapture_ShouldReturnTrue_WhenRecordDisabledLeavesEmptyVectorList()
    {
        // 出厂默认 ink_record_enabled=false：笔画只渲染不进向量表，
        // 快照判据必须只依赖位图表面，否则放映批注退出即静默丢失。
        PresentationInkExitSnapshotPolicy.ShouldCapture(hasDrawing: true).Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    public void ShouldCapture_ShouldReturnFalse_WhenNothingToCapture(bool hasDrawing)
    {
        PresentationInkExitSnapshotPolicy.ShouldCapture(hasDrawing).Should().BeFalse();
    }
}

public sealed class PresentationInkSnapshotNamerTests
{
    [Fact]
    public void BuildFileName_ShouldUseInvariantTimestampFormat()
    {
        var localTime = new DateTime(2026, 9, 10, 8, 5, 9, 123);

        var name = PresentationInkSnapshotNamer.BuildFileName(localTime);

        name.Should().Be("presentation_20260910_080509123.png");
        DateTime.TryParseExact(
            "20260910_080509123",
            "yyyyMMdd_HHmmssfff",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _).Should().BeTrue();
    }
}
