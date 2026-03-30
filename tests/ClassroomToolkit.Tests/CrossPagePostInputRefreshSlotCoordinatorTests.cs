using System;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPagePostInputRefreshSlotCoordinatorTests
{
    [Fact]
    public void TryAcquire_ShouldAcquire_WhenPointerUpTimestampIsUnset()
    {
        var result = CrossPagePostInputRefreshSlotCoordinator.TryAcquire(
            pointerUpSequence: 7,
            lastPointerUpUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc,
            readAppliedSequence: static () => throw new Xunit.Sdk.XunitException("should not read"),
            compareExchangeAppliedSequence: static (_, _) => throw new Xunit.Sdk.XunitException("should not compare-exchange"));

        result.Acquired.Should().BeTrue();
        result.PointerUpSequence.Should().Be(7);
    }

    [Fact]
    public void TryAcquire_ShouldReject_WhenAppliedSequenceAlreadyMatchesPointer()
    {
        var readCount = 0;

        var result = CrossPagePostInputRefreshSlotCoordinator.TryAcquire(
            pointerUpSequence: 9,
            lastPointerUpUtc: DateTime.UtcNow,
            readAppliedSequence: () =>
            {
                readCount++;
                return 9;
            },
            compareExchangeAppliedSequence: static (_, _) => throw new Xunit.Sdk.XunitException("should not compare-exchange"));

        result.Acquired.Should().BeFalse();
        readCount.Should().Be(1);
    }

    [Fact]
    public void TryAcquire_ShouldAcquire_WhenCompareExchangeSucceeds()
    {
        long applied = 3;
        var compareExchangeCount = 0;

        var result = CrossPagePostInputRefreshSlotCoordinator.TryAcquire(
            pointerUpSequence: 5,
            lastPointerUpUtc: DateTime.UtcNow,
            readAppliedSequence: () => applied,
            compareExchangeAppliedSequence: (nextValue, comparand) =>
            {
                compareExchangeCount++;
                if (applied == comparand)
                {
                    var original = applied;
                    applied = nextValue;
                    return original;
                }

                return applied;
            });

        result.Acquired.Should().BeTrue();
        applied.Should().Be(5);
        compareExchangeCount.Should().Be(1);
    }

    [Fact]
    public void TryAcquire_ShouldRetry_WhenCompareExchangeRacesThenEventuallySucceeds()
    {
        long applied = 1;
        var compareExchangeCount = 0;
        var forceRaceOnce = true;

        var result = CrossPagePostInputRefreshSlotCoordinator.TryAcquire(
            pointerUpSequence: 4,
            lastPointerUpUtc: DateTime.UtcNow,
            readAppliedSequence: () => applied,
            compareExchangeAppliedSequence: (nextValue, comparand) =>
            {
                compareExchangeCount++;
                if (forceRaceOnce)
                {
                    forceRaceOnce = false;
                    applied = 2;
                    return 999;
                }

                if (applied == comparand)
                {
                    var original = applied;
                    applied = nextValue;
                    return original;
                }

                return applied;
            });

        result.Acquired.Should().BeTrue();
        applied.Should().Be(4);
        compareExchangeCount.Should().BeGreaterThan(1);
    }
}
