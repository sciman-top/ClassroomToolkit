using System.Diagnostics;
using System.Threading;

namespace ClassroomToolkit.App.Startup;

internal enum SingleInstanceAcquireOutcome
{
    Acquired,
    AlreadyRunning,
    AccessDenied
}

/// <summary>
/// 单实例互斥获取：优先 Global\ 跨会话锁（防快速用户切换/管理员第二会话双开
/// 互覆 settings 与名册），ACL 拒绝时降级会话级 Local\。返回的互斥体必须由
/// 调用方持有整个进程生命周期；被 GC 回收会提前释放单实例锁。
/// </summary>
internal static class SingleInstanceGate
{
    public const string GlobalMutexName = @"Global\ClassroomToolkit.SingleInstance";
    public const string SessionMutexName = @"Local\ClassroomToolkit.SingleInstance";

    public static SingleInstanceAcquireOutcome AcquireWithFallback(
        string preferredName,
        string fallbackName,
        out Mutex? mutex)
    {
        var preferred = TryAcquire(preferredName, out var preferredMutex);
        if (preferred != SingleInstanceAcquireOutcome.AccessDenied)
        {
            mutex = preferredMutex;
            return preferred;
        }

        return TryAcquire(fallbackName, out mutex);
    }

    public static SingleInstanceAcquireOutcome TryAcquire(string name, out Mutex? mutex)
    {
        try
        {
            var created = new Mutex(initiallyOwned: true, name, out var createdNew);
            if (createdNew)
            {
                mutex = created;
                return SingleInstanceAcquireOutcome.Acquired;
            }

            created.Dispose();
            mutex = null;
            return SingleInstanceAcquireOutcome.AlreadyRunning;
        }
        catch (UnauthorizedAccessException)
        {
            mutex = null;
            return SingleInstanceAcquireOutcome.AccessDenied;
        }
        catch (AbandonedMutexException)
        {
            // 前任持有进程已死但互斥体仍被其它句柄钉住：所有权请求以"抛弃"信号回绝。
            // 误报 ACL 会触发 Local 降级，跨会话双开后两个进程互写 settings；
            // 改为尝试接管同一命名互斥体。
            return TryTakeOverAbandoned(name, out mutex);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandling.IsNonFatal(ex))
        {
            Debug.WriteLine($"[SingleInstanceGate] acquire failed name={name}: {ex.GetType().Name} - {ex.Message}");
            mutex = null;
            return SingleInstanceAcquireOutcome.AccessDenied;
        }
    }

    internal static SingleInstanceAcquireOutcome TryTakeOverAbandoned(string name, out Mutex? mutex)
    {
        mutex = null;
        Mutex? reopened = null;
        try
        {
            reopened = Mutex.OpenExisting(name);
            var owned = false;
            try
            {
                // WaitOne 抛 AbandonedMutexException 时所有权已同时判给本线程；
                // 返回 false 则已被新主持有，按"已在运行"处理。
                owned = reopened.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                owned = true;
            }

            if (owned)
            {
                mutex = reopened;
                return SingleInstanceAcquireOutcome.Acquired;
            }

            reopened.Dispose();
            return SingleInstanceAcquireOutcome.AlreadyRunning;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandling.IsNonFatal(ex))
        {
            Debug.WriteLine($"[SingleInstanceGate] abandoned takeover failed name={name}: {ex.GetType().Name} - {ex.Message}");
            reopened?.Dispose();
            return SingleInstanceAcquireOutcome.AccessDenied;
        }
    }
}
