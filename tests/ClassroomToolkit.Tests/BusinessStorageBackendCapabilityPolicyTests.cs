using ClassroomToolkit.Infra.Storage;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class BusinessStorageBackendCapabilityPolicyTests
{
    [Fact]
    public void IsSqliteAvailable_ShouldReturnFalse_WhenExperimentalDisabled()
    {
        var available = BusinessStorageBackendCapabilityPolicy.IsSqliteAvailable(experimentalEnabled: false);

        available.Should().BeFalse();
    }

    [Fact]
    public void IsSqliteAvailable_ShouldBeDeterministic_ForSameInput()
    {
        var first = BusinessStorageBackendCapabilityPolicy.IsSqliteAvailable(experimentalEnabled: true);
        var second = BusinessStorageBackendCapabilityPolicy.IsSqliteAvailable(experimentalEnabled: true);

        second.Should().Be(first);
    }
}
