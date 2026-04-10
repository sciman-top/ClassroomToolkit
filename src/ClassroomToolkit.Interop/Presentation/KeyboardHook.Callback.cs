using System.Diagnostics;
using System.Runtime.InteropServices;
using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.Interop.Utilities;

public sealed partial class KeyboardHook
{
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var startTime = Stopwatch.GetTimestamp();
        try
        {
            if (_disposed || !_acceptEvents || nCode < 0 || lParam == IntPtr.Zero)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            var msg = wParam.ToInt32();
            var isDown = msg == WmKeyDown || msg == WmSysKeyDown;
            var isUp = msg == WmKeyUp || msg == WmSysKeyUp;
            var data = Marshal.PtrToStructure<KbdHookStruct>(lParam);
            var virtualKey = (VirtualKey)data.VirtualKeyCode;

            if (isDown || isUp)
            {
                UpdateModifiers(virtualKey, isDown);
            }

            var bindingMatched = false;
            if (isDown)
            {
                var key = MapKey(virtualKey);
                if (key.HasValue)
                {
                    var modifiers = CurrentModifiers();
                    var binding = new KeyBinding(key.Value, modifiers);
                    if (TargetBinding != null && binding.Equals(TargetBinding))
                    {
                        if (!_acceptEvents)
                        {
                            return CallNextHookEx(_hookId, nCode, wParam, lParam);
                        }

                        bindingMatched = true;
                        InteropEventDispatchPolicy.InvokeSafely(
                            BindingTriggered,
                            binding,
                            "KeyboardHook.BindingTriggered");
                    }
                }
            }

            var suppressionDecision = KeyboardHookSuppressionPolicy.Resolve(
                suppressWhenMatched: SuppressWhenMatched,
                bindingMatched: bindingMatched,
                isDown: isDown,
                isUp: isUp,
                key: virtualKey,
                pendingSuppressedKey: _pendingSuppressedKey);
            _pendingSuppressedKey = suppressionDecision.PendingSuppressedKey;
            if (suppressionDecision.ShouldSuppress)
            {
                return new IntPtr(1);
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
        catch (Exception ex) when (InteropExceptionFilterPolicy.IsNonFatal(ex))
        {
            InteropHookDiagnostics.RecordCallbackException(
                component: "KeyboardHook",
                source: "keyboard",
                ex,
                ref _callbackExceptionCount,
                ref _lastExceptionLogTick,
                ExceptionLogIntervalMs);
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
        finally
        {
            InteropHookDiagnostics.LogSlowCallback("KeyboardHook", startTime, HookCallbackTimeoutMs);
        }
    }

    private void UpdateModifiers(VirtualKey key, bool isDown)
    {
        switch (key)
        {
            case VirtualKey.Shift:
                RefreshModifierState();
                break;
            case VirtualKey.LeftShift:
                _leftShiftDown = isDown;
                break;
            case VirtualKey.RightShift:
                _rightShiftDown = isDown;
                break;
            case VirtualKey.Control:
                RefreshModifierState();
                break;
            case VirtualKey.LeftControl:
                _leftCtrlDown = isDown;
                break;
            case VirtualKey.RightControl:
                _rightCtrlDown = isDown;
                break;
            case VirtualKey.Alt:
                RefreshModifierState();
                break;
            case VirtualKey.LeftAlt:
                _leftAltDown = isDown;
                break;
            case VirtualKey.RightAlt:
                _rightAltDown = isDown;
                break;
        }
    }

    private void RefreshModifierState()
    {
        _leftShiftDown = IsKeyDown(VirtualKey.LeftShift);
        _rightShiftDown = IsKeyDown(VirtualKey.RightShift);
        _leftCtrlDown = IsKeyDown(VirtualKey.LeftControl);
        _rightCtrlDown = IsKeyDown(VirtualKey.RightControl);
        _leftAltDown = IsKeyDown(VirtualKey.LeftAlt);
        _rightAltDown = IsKeyDown(VirtualKey.RightAlt);
    }

    private static bool IsKeyDown(VirtualKey key)
    {
        return (GetKeyState((int)key) & 0x8000) != 0;
    }

    private KeyModifiers CurrentModifiers()
    {
        var inconsistent =
            (_leftShiftDown != IsKeyDown(VirtualKey.LeftShift)) ||
            (_rightShiftDown != IsKeyDown(VirtualKey.RightShift)) ||
            (_leftCtrlDown != IsKeyDown(VirtualKey.LeftControl)) ||
            (_rightCtrlDown != IsKeyDown(VirtualKey.RightControl)) ||
            (_leftAltDown != IsKeyDown(VirtualKey.LeftAlt)) ||
            (_rightAltDown != IsKeyDown(VirtualKey.RightAlt));

        if (inconsistent)
        {
            Debug.WriteLine("[KeyboardHook] Modifier state inconsistency detected. Forcing refresh.");
            RefreshModifierState();
        }

        var modifiers = KeyModifiers.None;
        if (_leftShiftDown || _rightShiftDown)
        {
            modifiers |= KeyModifiers.Shift;
        }
        if (_leftCtrlDown || _rightCtrlDown)
        {
            modifiers |= KeyModifiers.Control;
        }
        if (_leftAltDown || _rightAltDown)
        {
            modifiers |= KeyModifiers.Alt;
        }

        return modifiers;
    }

    private static VirtualKey? MapKey(VirtualKey key)
    {
        if (key is VirtualKey.Shift or VirtualKey.Control or VirtualKey.Alt
            or VirtualKey.LeftShift or VirtualKey.RightShift
            or VirtualKey.LeftControl or VirtualKey.RightControl
            or VirtualKey.LeftAlt or VirtualKey.RightAlt)
        {
            return null;
        }

        return Enum.IsDefined(typeof(VirtualKey), key) ? key : null;
    }
}
