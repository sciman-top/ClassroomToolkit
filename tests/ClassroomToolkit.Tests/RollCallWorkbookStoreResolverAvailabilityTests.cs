using ClassroomToolkit.Infra.Storage;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class RollCallWorkbookStoreResolverAvailabilityTests
{
    [Fact]
    public void Create_ShouldThrow_WhenAvailabilityEvaluatorIsNull()
    {
        Action act = () => RollCallWorkbookStoreResolver.Create(
            preferSqlite: true,
            experimentalSqliteEnabled: true,
            out _,
            sqliteAvailabilityEvaluator: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_ShouldPassExperimentalFlag_ToAvailabilityEvaluator()
    {
        bool? capturedFlag = null;
        _ = RollCallWorkbookStoreResolver.Create(
            preferSqlite: true,
            experimentalSqliteEnabled: false,
            out var backend,
            sqliteAvailabilityEvaluator: enabled =>
            {
                capturedFlag = enabled;
                return true;
            });

        capturedFlag.Should().BeFalse();
        backend.Should().Be(BusinessStorageBackend.Sqlite);
    }
}
