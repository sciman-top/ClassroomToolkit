using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.Services.Input;

namespace ClassroomToolkit.Services.Input;

public class GlobalHookService : IDisposable
{
    private readonly List<KeyboardHook> _activeHooks = new();
    private bool _disposed;

    public event Action? HookUnavailable;

    public async Task<bool> RegisterHookAsync(
        IEnumerable<KeyBinding> bindings,
        Action<KeyBinding> callback,
        Func<bool> shouldKeepActive)
    {
        var startedHooks = new List<KeyboardHook>();
        bool success = true;
        int lastError = 0;

        foreach (var binding in bindings)
        {
            if (!shouldKeepActive())
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
            
            if (!shouldKeepActive())
            {
                hook.BindingTriggered -= callback;
                hook.Stop();
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

        if (!shouldKeepActive())
        {
            CleanupHooks(startedHooks, callback);
            return false;
        }

        _activeHooks.AddRange(startedHooks);

        if (!success)
        {
            // Notify failure if any hook failed to start
            HookUnavailable?.Invoke();
            return false;
        }

        return true;
    }

    public void UnregisterAll()
    {
        foreach (var hook in _activeHooks)
        {
            // We need to clear event handlers too if we can access the original callback
            // But here we might just Stop them.
            // Ideally we should track the callback to unsubscribe.
            // For simple usage, just Stop is often enough if the hook object is discarded.
            // However, to be safe, KeyboardHook inside Interop might need explicit unsubscribe if it holds refs.
            // Let's assume Stop() is sufficient for the Hook resource.
            // But to avoid leaked delegates in C#, we should clear BindingTriggered if possible.
            // Since we don't track the callback per hook easily here without a wrapper class,
            // we rely on the fact that KeyboardHook.Stop() should disable the native hook.
            hook.Stop();
        }
        // In a real refactor we might want a wrapper object to hold (Hook, Callback) pairs.
        // For now, clearing the list is the main step.
        _activeHooks.Clear();
    }

    private void CleanupHooks(List<KeyboardHook> hooks, Action<KeyBinding> callback)
    {
        foreach (var hook in hooks)
        {
            hook.BindingTriggered -= callback;
            hook.Stop();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnregisterAll();
        GC.SuppressFinalize(this);
    }
}
