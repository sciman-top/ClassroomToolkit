using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkStorageDiagnosticsContractTests
{
    [Fact]
    public void InkPersistenceService_ShouldExposeFailureDiagnostics()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Ink",
            "InkPersistenceService.cs"));

        source.Should().Contain("[InkPersistence] failed to parse sidecar json");
        source.Should().Contain("[InkPersistence] failed to read sidecar json");
        source.Should().Contain("[InkPersistence] delete file failed");
    }

    [Fact]
    public void InkStorageService_ShouldExposeFailureDiagnostics()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Ink",
            "InkStorageService.cs"));

        source.Should().Contain("[InkStorage] failed to parse page json");
        source.Should().Contain("[InkStorage] failed to read page json");
        source.Should().Contain("[InkStorage] cleanup folder failed");
    }

    [Fact]
    public void InkWriteAheadLogService_ShouldExposeFailureDiagnostics()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Ink",
            "InkWriteAheadLogService.cs"));

        source.Should().Contain("[InkWAL] failed to load wal");
        source.Should().Contain("[InkWAL] save failed");
        source.Should().Contain("[InkWAL] temp cleanup failed");
    }

    [Fact]
    public void InkWriteAheadLogService_ShouldUsePerWalPathLocking()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Ink",
            "InkWriteAheadLogService.cs"));

        source.Should().Contain("ConcurrentDictionary<string, object> WalFileLocks");
        source.Should().Contain("lock (GetWalFileLock(walPath))");
    }
}
