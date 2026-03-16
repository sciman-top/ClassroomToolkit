using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.Services.Input;

namespace ClassroomToolkit.Services.Input;

public class GlobalHookService : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly List<KeyboardHook> _activeHooks = new();
    private bool _disposed;

    public event Action? HookUnavailable;

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

        return RegisterHookAsync(bindings, _ => callback(), shouldKeepActive);
    }

    public async Task<bool> RegisterHookAsync(
        IEnumerable<KeyBinding> bindings,
        Action<KeyBinding> callback,
        Func<bool> shouldKeepActive)
    {
        if (IsDisposed())
        {
            return false;
        }

        var startedHooks = new List<KeyboardHook>();
        bool success = true;
        int lastError = 0;

        foreach (var binding in bindings)
        {
            if (IsDisposed() || !shouldKeepActive())
            {
                CleanupHooks(startedHooks, callback);
                return false;
            }

            var hook = new KeyboardHook
            {
                TargetBinding = binding,
                SuppressWhenMatched = true
            };
            hook.BindingTriggered += callback;
            
            await hook.StartAsync();
            
            if (IsDisposed() || !shouldKeepActive())
            {
                hook.BindingTriggered -= callback;
                TryStopHook(hook, "register-aborted");
                CleanupHooks(startedHooks, callback);
                return false;
            }

            if (!hook.IsActive)
            {
                success = false;
                lastError = hook.LastError;
                // If one fails, we might decide to stop all or keep partial.
                // For now, let's keep trying others but mark failure.
            }
            startedHooks.Add(hook);
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

        if (!success)
        {
            NotifyHookUnavailable();
            return false;
        }

        return true;
    }

    public void UnregisterAll()
    {
        var hooks = DrainActiveHooks();
        StopHooks(hooks, "unregister-all");
    }

    private void CleanupHooks(List<KeyboardHook> hooks, Action<KeyBinding> callback)
    {
        foreach (var hook in hooks)
        {
            hook.BindingTriggered -= callback;
            TryStopHook(hook, "cleanup");
        }
    }

    public void Dispose()
    {
        List<KeyboardHook>? hooks = null;
        lock (_syncRoot)
        {
            if (_disposed) return;
            _disposed = true;
            if (_activeHooks.Count > 0)
            {
                hooks = new List<KeyboardHook>(_activeHooks);
                _activeHooks.Clear();
            }
        }

        if (hooks is not null)
        {
            StopHooks(hooks, "dispose");
        }

        GC.SuppressFinalize(this);
    }

    private bool IsDisposed()
    {
        lock (_syncRoot)
        {
            return _disposed;
        }
    }

    private bool TryTrackActiveHooks(List<KeyboardHook> hooks)
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

    private List<KeyboardHook> DrainActiveHooks()
    {
        lock (_syncRoot)
        {
            if (_activeHooks.Count == 0)
            {
                return [];
            }

            var hooks = new List<KeyboardHook>(_activeHooks);
            _activeHooks.Clear();
            return hooks;
        }
    }

    private static void StopHooks(IEnumerable<KeyboardHook> hooks, string reason)
    {
        foreach (var hook in hooks)
        {
            TryStopHook(hook, reason);
        }
    }

    private static void TryStopHook(KeyboardHook hook, string reason)
    {
        try
        {
            hook.Stop();
        }
        catch (Exception ex) when (IsNonFatal(ex))
        {
            Debug.WriteLine($"[GlobalHookService] Stop hook failed ({reason}): {ex.Message}");
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
                Debug.WriteLine($"[GlobalHookService] HookUnavailable callback failed: {ex.Message}");
            }
        }
    }
}
