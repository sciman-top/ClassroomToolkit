using System.Runtime.InteropServices;

namespace ClassroomToolkit.Interop.Utilities;

/// <summary>
/// 管理 COM 对象的生命周期，确保正确释放资源，防止进程残留
/// Thread-safe COM object lifecycle manager
/// </summary>
public sealed class ComObjectManager : IDisposable
{
    private readonly List<object> _comObjects = new();
    private readonly object _lock = new object();
    private bool _disposed;

    /// <summary>
    /// 追踪 COM 对象，在 Dispose 时自动释放
    /// </summary>
    /// <typeparam name="T">COM 对象类型</typeparam>
    /// <param name="comObject">要追踪的 COM 对象</param>
    /// <returns>原 COM 对象（便于链式调用）</returns>
    public T Track<T>(T comObject) where T : class
    {
        if (comObject == null)
        {
            return comObject;
        }

        if (!Marshal.IsComObject(comObject))
        {
            return comObject;
        }

        lock (_lock)
        {
            if (_disposed)
            {
                // 如果已经 Dispose，立即释放新对象
                Marshal.ReleaseComObject(comObject);
                throw new ObjectDisposedException(nameof(ComObjectManager));
            }
            _comObjects.Add(comObject);
        }

        return comObject;
    }

    /// <summary>
    /// 手动释放特定 COM 对象
    /// </summary>
    public void Release(object comObject)
    {
        if (comObject == null || !Marshal.IsComObject(comObject))
        {
            return;
        }

        lock (_lock)
        {
            if (_comObjects.Remove(comObject))
            {
                try
                {
                    Marshal.ReleaseComObject(comObject);
                }
                catch
                {
                    // 吞噬释放异常，避免终止应用
                }
            }
        }
    }

    /// <summary>
    /// 释放所有追踪的 COM 对象
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // 逆序释放（后进先出）
            for (int i = _comObjects.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (_comObjects[i] != null)
                    {
                        Marshal.ReleaseComObject(_comObjects[i]);
                    }
                }
                catch
                {
                    // 静默处理释放异常，确保所有对象都尝试释放
                }
            }

            _comObjects.Clear();
        }
    }
}
