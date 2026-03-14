using ClassroomToolkit.Domain.Utilities;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class AtomicReplaceFallbackPolicyTests
{
    [Fact]
    public void ShouldFallback_ShouldReturnTrue_ForUnauthorizedAccessException()
    {
        AtomicReplaceFallbackPolicy.ShouldFallback(new UnauthorizedAccessException()).Should().BeTrue();
    }

    [Fact]
    public void ShouldFallback_ShouldReturnTrue_ForPlatformNotSupportedException()
    {
        AtomicReplaceFallbackPolicy.ShouldFallback(new PlatformNotSupportedException()).Should().BeTrue();
    }

    [Fact]
    public void ShouldFallback_ShouldReturnFalse_ForOtherExceptions()
    {
        AtomicReplaceFallbackPolicy.ShouldFallback(new IOException()).Should().BeFalse();
        AtomicReplaceFallbackPolicy.ShouldFallback(new InvalidOperationException()).Should().BeFalse();
    }
}
