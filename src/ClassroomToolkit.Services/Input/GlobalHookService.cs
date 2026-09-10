using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.Services.Input;

namespace ClassroomToolkit.Services.Input;

public class GlobalHookService : IDisposable
{
    private const int StopRetryCount = 3;

    private readonly object _syncRoot = new();
    private readonly List<IKeyboardHookHandle> _activeHooks = new();
    private bool _disposed;

    /// <summary>
    /// 钩子句柄工厂。生产默认创建真实 KeyboardHook；测试经 InternalsVisibleTo
    /// 注入假句柄，行为级验证注册-回滚契约而不安装系统级钩子。
    /// </summary>
    internal Func<KeyBinding, IKeyboardHookHandle> HookFactory { get; set; } =
        static binding => new KeyboardHook
        {
            TargetBinding = binding,
            SuppressWhenMatched = true
        };

    [SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Action-based event is part of the existing app contract.")]
    public event Action? HookUnavailable;

    public int ResidualHookCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _activeHooks.Count;
            }
        }
    }

    public Task<bool> RegisterHookAsync(
        IEnumerable<string> bindingTokens,
        Action callback,
        Func<bool> shouldKeepActive)
    {
        ArgumentNullException.ThrowIfNull(bindingTokens);
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(shouldKeepActive);

        var bindings = bindingTokens
            .Select(token =>
            {
                if (KeyBindingParser.TryParse(token, out var parsed) && parsed != null)
                {
                    return parsed;
                }

                Debug.WriteLine($"[GlobalHookService] Skip invalid binding token: '{token}'.");
                return null;
            })
            .Where(binding => binding != null)
            .Select(binding => binding!)
            .Distinct()
            .ToArray();

        if (bindings.Length == 0)
        {
            NotifyHookUnavailable();
            return Task.FromResult(false);
        }

        return RegisterHookAsync(bindings, _ => TryInvokeBindingCallback(callback), shouldKeepActive);
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "KeyboardHook ownership is transferred to the active hook list after successful registration; failure paths dispose started hooks.")]
    public async Task<bool> RegisterHookAsync(
        IEnumerable<KeyBinding> bindings,
        Action<KeyBinding> callback,
        Func<bool> shouldKeepActive)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(shouldKeepActive);

        if (IsDisposed())
        {
            return false;
        }

        var startedHooks = new List<IKeyboardHookHandle>();

        try
        {
            foreach (var binding in bindings)
            {
                if (IsDisposed() || !shouldKeepActive())
                {
                    CleanupHooks(startedHooks, callback);
                    return false;
                }

                var hook = HookFactory(binding);
                hook.BindingTriggered += callback;

                try
                {
                    // 不用 ConfigureAwait(false)：多绑定循环时第二个钩子的 StartAsync 必须
                    // 仍在调用方上下文（UI 线程）安装，WH_KEYBOARD_LL 才能收到回调。
                    await hook.StartAsync();
                }
                catch (Exception ex) when (IsNonFatal(ex))
                {
                    Debug.WriteLine($"[GlobalHookService] Start hook failed: {ex.GetType().Name} - {ex.Message}");
                    hook.BindingTriggered -= callback;
                    RetainIfStopFailed(hook, "register-failed");
                    CleanupHooks(startedHooks, callback);
                    NotifyHookUnavailable();
                    return false;
                }

                if (IsDisposed() || !shouldKeepActive())
                {
                    hook.BindingTriggered -= callback;
                    RetainIfStopFailed(hook, "register-aborted");
                    CleanupHooks(startedHooks, callback);
                    return false;
                }

                if (!hook.IsActive)
                {
                    hook.BindingTriggered -= callback;
                    RetainIfStopFailed(hook, "register-inactive");
                    CleanupHooks(startedHooks, callback);
                    NotifyHookUnavailable();
                    return false;
                }
                startedHooks.Add(hook);
            }
        }
        catch (Exception ex) when (IsNonFatal(ex))
        {
            Debug.WriteLine($"[GlobalHookService] Register bindings failed: {ex.GetType().Name} - {ex.Message}");
            CleanupHooks(startedHooks, callback);
            NotifyHookUnavailable();
            return false;
        }

        if (IsDisposed() || !shouldKeepActive())
        {
            CleanupHooks(startedHooks, callback);
            return false;
        }

        if (!TryTrackActiveHooks(startedHooks))
        {
            CleanupHooks(startedHooks, callback);
            return false;
        }

        return true;
    }

    public void UnregisterAll()
    {
        StopTrackedHooks("unregister-all");
    }

    private void CleanupHooks(List<IKeyboardHookHandle> hooks, Action<KeyBinding> callback)
    {
        foreach (var hook in hooks)
        {
            hook.BindingTriggered -= callback;
            RetainIfStopFailed(hook, "cleanup");
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                // A previous disposal may have failed to release a native hook. Keep
                // the residual ownership reachable and retry on subsequent Dispose calls.
                if (_activeHooks.Count == 0)
                {
                    return;
                }
            }

            _disposed = true;
        }

        StopTrackedHooks("dispose");
    }

    private bool IsDisposed()
    {
        lock (_syncRoot)
        {
            return _disposed;
        }
    }

    private bool TryTrackActiveHooks(List<IKeyboardHookHandle> hooks)
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return false;
            }

            _activeHooks.AddRange(hooks);
            return true;
        }
    }

    private void StopTrackedHooks(string reason)
    {
        IKeyboardHookHandle[] hooks;
        lock (_syncRoot)
        {
            if (_activeHooks.Count == 0)
            {
                return;
            }

            hooks = _activeHooks.ToArray();
        }

        foreach (var hook in hooks)
        {
            var stopped = false;
            for (var attempt = 1; attempt <= StopRetryCount; attempt++)
            {
                if (TryStopHook(hook, reason))
                {
                    stopped = true;
                    break;
                }

                // Keep cleanup bounded without blocking the caller. Residual ownership
                // remains in _activeHooks for a later lifecycle retry.
            }

            if (stopped)
            {
                lock (_syncRoot)
                {
                    _activeHooks.Remove(hook);
                }
            }
            else
            {
                Debug.WriteLine($"[GlobalHookService] Residual hook retained after stop retries ({reason}).");
            }
        }
    }

    private void RetainIfStopFailed(IKeyboardHookHandle hook, string reason)
    {
        if (TryStopHook(hook, reason))
        {
            return;
        }

        lock (_syncRoot)
        {
            if (!_activeHooks.Contains(hook))
            {
                _activeHooks.Add(hook);
            }
        }
    }

    private static bool TryStopHook(IKeyboardHookHandle hook, string reason)
    {
        try
        {
            hook.Dispose();
            if (hook.IsActive)
            {
                Debug.WriteLine($"[GlobalHookService] Stop hook remained active ({reason}); retaining ownership.");
                return false;
            }

            return true;
        }
        catch (Exception ex) when (IsNonFatal(ex))
        {
            Debug.WriteLine($"[GlobalHookService] Stop hook failed ({reason}): {ex.GetType().Name} - {ex.Message}");
            return false;
        }
    }

    private static bool IsNonFatal(Exception ex)
    {
        return ex is not (
            OutOfMemoryException
            or AppDomainUnloadedException
            or BadImageFormatException
            or CannotUnloadAppDomainException
            or InvalidProgramException
            or StackOverflowException
            or AccessViolationException);
    }

    private static void TryInvokeBindingCallback(Action callback)
    {
        try
        {
            callback();
        }
        catch (Exception ex) when (IsNonFatal(ex))
        {
            Debug.WriteLine($"[GlobalHookService] Binding callback failed: {ex.GetType().Name} - {ex.Message}");
        }
    }

    private void NotifyHookUnavailable()
    {
        var handlers = HookUnavailable?.GetInvocationList();
        if (handlers == null)
        {
            return;
        }

        foreach (var callback in handlers)
        {
            try
            {
                ((Action)callback)();
            }
            catch (Exception ex) when (IsNonFatal(ex))
            {
                Debug.WriteLine($"[GlobalHookService] HookUnavailable callback failed: {ex.GetType().Name} - {ex.Message}");
            }
        }
    }
}
