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
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[SingleInstanceGate] acquire failed name={name}: {ex.GetType().Name} - {ex.Message}");
            mutex = null;
            return SingleInstanceAcquireOutcome.AccessDenied;
        }
    }
}
