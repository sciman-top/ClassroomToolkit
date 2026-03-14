using ClassroomToolkit.Infra.Storage;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class BusinessStorageBackendSelectionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnExcel_WhenSqliteNotPreferred()
    {
        var backend = BusinessStorageBackendSelectionPolicy.Resolve(
            preferSqlite: false,
            sqliteAvailable: true);

        backend.Should().Be(BusinessStorageBackend.ExcelWorkbook);
    }

    [Fact]
    public void Resolve_ShouldReturnExcel_WhenSqliteUnavailable()
    {
        var backend = BusinessStorageBackendSelectionPolicy.Resolve(
            preferSqlite: true,
            sqliteAvailable: false);

        backend.Should().Be(BusinessStorageBackend.ExcelWorkbook);
    }

    [Fact]
    public void Resolve_ShouldReturnSqlite_WhenPreferredAndAvailable()
    {
        var backend = BusinessStorageBackendSelectionPolicy.Resolve(
            preferSqlite: true,
            sqliteAvailable: true);

        backend.Should().Be(BusinessStorageBackend.Sqlite);
    }
}
