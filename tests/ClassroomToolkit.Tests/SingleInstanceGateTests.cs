using ClassroomToolkit.App.Startup;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class SingleInstanceGateTests
{
    [Fact]
    public void TryAcquire_ShouldReturnAcquired_WhenNameIsFree()
    {
        var name = UniqueSessionMutexName();
        var outcome = SingleInstanceGate.TryAcquire(name, out var mutex);

        try
        {
            outcome.Should().Be(SingleInstanceAcquireOutcome.Acquired);
            mutex.Should().NotBeNull();
        }
        finally
        {
            mutex?.Dispose();
        }
    }

    [Fact]
    public void TryAcquire_ShouldReturnAlreadyRunning_WhenNameIsHeldByLiveMutex()
    {
        var name = UniqueSessionMutexName();
        var first = SingleInstanceGate.TryAcquire(name, out var firstMutex);
        try
        {
            first.Should().Be(SingleInstanceAcquireOutcome.Acquired);

            var second = SingleInstanceGate.TryAcquire(name, out var secondMutex);

            second.Should().Be(SingleInstanceAcquireOutcome.AlreadyRunning);
            secondMutex.Should().BeNull();
        }
        finally
        {
            firstMutex?.Dispose();
        }
    }

    [Fact]
    public void TryAcquire_ShouldAllowReacquire_AfterPreviousMutexReleased()
    {
        // 崩溃后进程句柄由 OS 关闭、内核互斥体消亡：重启场景必须能重新获取。
        var name = UniqueSessionMutexName();
        var first = SingleInstanceGate.TryAcquire(name, out var firstMutex);
        first.Should().Be(SingleInstanceAcquireOutcome.Acquired);
        firstMutex!.Dispose();

        var second = SingleInstanceGate.TryAcquire(name, out var secondMutex);
        try
        {
            second.Should().Be(SingleInstanceAcquireOutcome.Acquired);
            secondMutex.Should().NotBeNull();
        }
        finally
        {
            secondMutex?.Dispose();
        }
    }

    [Fact]
    public void AcquireWithFallback_ShouldPreferPreferredScope_WhenBothFree()
    {
        var preferred = UniqueSessionMutexName();
        var fallback = UniqueSessionMutexName();
        var outcome = SingleInstanceGate.AcquireWithFallback(preferred, fallback, out var mutex);
        try
        {
            outcome.Should().Be(SingleInstanceAcquireOutcome.Acquired);
            mutex.Should().NotBeNull();

            // 拿到的是优先（Global）锁，而不是降级（Local）锁。
            SingleInstanceGate.TryAcquire(preferred, out _)
                .Should().Be(SingleInstanceAcquireOutcome.AlreadyRunning);
        }
        finally
        {
            mutex?.Dispose();
        }
    }

    [Fact]
    public void AcquireWithFallback_ShouldReportAlreadyRunning_WhenPreferredIsHeld()
    {
        // 优先锁被存活实例持有时必须判"已在运行"，绝不能降级到后备锁造成双开。
        var preferred = UniqueSessionMutexName();
        var fallback = UniqueSessionMutexName();
        var holder = SingleInstanceGate.TryAcquire(preferred, out var holderMutex);
        try
        {
            holder.Should().Be(SingleInstanceAcquireOutcome.Acquired);

            var outcome = SingleInstanceGate.AcquireWithFallback(preferred, fallback, out var mutex);

            outcome.Should().Be(SingleInstanceAcquireOutcome.AlreadyRunning);
            mutex.Should().BeNull();
            // 后备锁未被占用：后续同会话启动仍走正常路径。
            SingleInstanceGate.TryAcquire(fallback, out var fallbackMutex)
                .Should().Be(SingleInstanceAcquireOutcome.Acquired);
            fallbackMutex?.Dispose();
        }
        finally
        {
            holderMutex?.Dispose();
        }
    }

    private static string UniqueSessionMutexName()
    {
        return $@"Local\ClassroomToolkit.Tests.{Guid.NewGuid():N}";
    }
}
