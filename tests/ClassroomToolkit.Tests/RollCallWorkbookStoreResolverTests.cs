using ClassroomToolkit.Infra.Storage;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class RollCallWorkbookStoreResolverTests
{
    [Fact]
    public void Create_ShouldReturnWorkbookAdapter_ByDefault()
    {
        var store = RollCallWorkbookStoreResolver.Create(
            preferSqlite: false,
            experimentalSqliteEnabled: false,
            out var backend);

        store.Should().BeOfType<RollCallWorkbookStoreAdapter>();
        backend.Should().Be(BusinessStorageBackend.ExcelWorkbook);
    }

    [Fact]
    public void Create_ShouldFallbackToWorkbookAdapter_WhenSqlitePreferredButUnavailable()
    {
        var store = RollCallWorkbookStoreResolver.Create(
            preferSqlite: true,
            experimentalSqliteEnabled: false,
            out var backend);

        store.Should().BeOfType<RollCallWorkbookStoreAdapter>();
        backend.Should().Be(BusinessStorageBackend.ExcelWorkbook);
    }

    [Fact]
    public void Create_ShouldReturnSqliteAdapter_WhenPreferredAndAvailable()
    {
        var store = RollCallWorkbookStoreResolver.Create(
            preferSqlite: true,
            experimentalSqliteEnabled: true,
            out var backend,
            _ => true);

        store.Should().BeOfType<StudentWorkbookSqliteRollCallStoreAdapter>();
        backend.Should().Be(BusinessStorageBackend.Sqlite);
    }

    [Fact]
    public void Create_ShouldThrow_WhenAvailabilityEvaluatorIsNull()
    {
        Action act = () => RollCallWorkbookStoreResolver.Create(
            preferSqlite: true,
            experimentalSqliteEnabled: true,
            out _,
            null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
