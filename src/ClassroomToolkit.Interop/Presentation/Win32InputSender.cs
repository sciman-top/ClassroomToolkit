using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClassroomToolkit.Interop.Presentation;

public sealed class Win32InputSender : IInputSender
{
    private const uint InputKeyboard = 1;
    private const uint InputMouse = 0;
    private const uint KeyeventfKeyup = 0x0002;
    private const uint KeyeventfExtended = 0x0001;
    private const uint MouseeventfWheel = 0x0800;

    public bool SendKey(IntPtr hwnd, VirtualKey key, KeyModifiers modifiers, InputStrategy strategy, bool keyDownOnly)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }
        return strategy == InputStrategy.Message
            ? SendKeyMessage(hwnd, key, modifiers, keyDownOnly)
            : SendKeyInput(key, modifiers, keyDownOnly);
    }

    public bool SendWheel(IntPtr hwnd, int delta, InputStrategy strategy)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }
        if (hwnd == IntPtr.Zero || delta == 0)
        {
            return false;
        }
        return strategy == InputStrategy.Message
            ? SendWheelMessage(hwnd, delta)
            : SendWheelInput(delta);
    }

    private static bool SendKeyMessage(IntPtr hwnd, VirtualKey key, KeyModifiers modifiers, bool keyDownOnly)
    {
        var downParam = BuildKeyLParam(isKeyUp: false);
        var upParam = BuildKeyLParam(isKeyUp: true);
        var appliedModifiers = new List<VirtualKey>();
        var modifiersApplied = SendModifiers(hwnd, modifiers, true, appliedModifiers);
        try
        {
            if (!modifiersApplied)
            {
                return false;
            }
            var down = NativeMethods.PostMessage(hwnd, NativeMethods.WmKeyDown, (IntPtr)key, downParam);
            if (!down)
            {
                Debug.WriteLine($"[Win32InputSender] PostMessage key down failed: hwnd={hwnd}, key={key}, lastError={Marshal.GetLastWin32Error()}");
            }
            var up = keyDownOnly || !down
                ? true
                : NativeMethods.PostMessage(hwnd, NativeMethods.WmKeyUp, (IntPtr)key, upParam);
            if (!keyDownOnly && down && !up)
            {
                Debug.WriteLine($"[Win32InputSender] PostMessage key up failed: hwnd={hwnd}, key={key}, lastError={Marshal.GetLastWin32Error()}");
            }
            return down && up;
        }
        finally
        {
            if (appliedModifiers.Count > 0)
            {
                SendModifiers(hwnd, appliedModifiers, false);
            }
        }
    }

    private static bool SendKeyInput(VirtualKey key, KeyModifiers modifiers, bool keyDownOnly)
    {
        var inputs = new List<NativeMethods.Input>();
        AddModifierInputs(inputs, modifiers, true);
        inputs.Add(CreateKeyInput(key, isKeyUp: false));
        if (!keyDownOnly)
        {
            inputs.Add(CreateKeyInput(key, isKeyUp: true));
        }
        AddModifierInputs(inputs, modifiers, false);
        return SendInputs(inputs);
    }

    private static bool SendWheelMessage(IntPtr hwnd, int delta)
    {
        var wParam = BuildWheelWParam(delta);
        var lParam = BuildWheelLParam();
        var sent = NativeMethods.PostMessage(hwnd, NativeMethods.WmMouseWheel, wParam, lParam);
        if (!sent)
        {
            Debug.WriteLine($"[Win32InputSender] PostMessage wheel failed: hwnd={hwnd}, delta={delta}, lastError={Marshal.GetLastWin32Error()}");
        }
        return sent;
    }

    private static bool SendWheelInput(int delta)
    {
        var input = new NativeMethods.Input
        {
            Type = InputMouse,
            Data = new NativeMethods.InputUnion
            {
                Mouse = new NativeMethods.MouseInput
                {
                    MouseData = unchecked((uint)delta),
                    Flags = MouseeventfWheel
                }
            }
        };
        return SendInputs(new List<NativeMethods.Input> { input });
    }

    private static bool SendModifiers(IntPtr hwnd, KeyModifiers modifiers, bool isKeyDown, List<VirtualKey>? applied)
    {
        foreach (var mod in EnumerateModifiers(modifiers))
        {
            var msg = isKeyDown ? NativeMethods.WmKeyDown : NativeMethods.WmKeyUp;
            if (!NativeMethods.PostMessage(hwnd, msg, (IntPtr)mod, BuildKeyLParam(!isKeyDown)))
            {
                Debug.WriteLine($"[Win32InputSender] PostMessage modifier failed: hwnd={hwnd}, key={mod}, isDown={isKeyDown}, lastError={Marshal.GetLastWin32Error()}");
                return false;
            }
            applied?.Add(mod);
        }
        return true;
    }

    private static bool SendModifiers(IntPtr hwnd, IReadOnlyList<VirtualKey> modifiers, bool isKeyDown)
    {
        foreach (var mod in modifiers)
        {
            var msg = isKeyDown ? NativeMethods.WmKeyDown : NativeMethods.WmKeyUp;
            if (!NativeMethods.PostMessage(hwnd, msg, (IntPtr)mod, BuildKeyLParam(!isKeyDown)))
            {
                Debug.WriteLine($"[Win32InputSender] PostMessage applied modifier release failed: hwnd={hwnd}, key={mod}, isDown={isKeyDown}, lastError={Marshal.GetLastWin32Error()}");
                return false;
            }
        }
        return true;
    }

    private static IEnumerable<VirtualKey> EnumerateModifiers(KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            yield return VirtualKey.Control;
        }
        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            yield return VirtualKey.Shift;
        }
        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            yield return VirtualKey.Alt;
        }
    }

    private static void AddModifierInputs(List<NativeMethods.Input> inputs, KeyModifiers modifiers, bool isKeyDown)
    {
        foreach (var mod in EnumerateModifiers(modifiers))
        {
            inputs.Add(CreateKeyInput(mod, !isKeyDown));
        }
    }

    private static NativeMethods.Input CreateKeyInput(VirtualKey key, bool isKeyUp)
    {
        var input = new NativeMethods.Input
        {
            Type = InputKeyboard,
            Data = new NativeMethods.InputUnion
            {
                Keyboard = new NativeMethods.KeyboardInput
                {
                    VirtualKey = (ushort)key,
                    Flags = isKeyUp ? KeyeventfKeyup : 0
                }
            }
        };
        if (IsExtendedKey(key))
        {
            input.Data.Keyboard.Flags |= KeyeventfExtended;
        }
        return input;
    }

    private static bool SendInputs(List<NativeMethods.Input> inputs)
    {
        var array = inputs.ToArray();
        var size = Marshal.SizeOf<NativeMethods.Input>();
        var sent = NativeMethods.SendInput((uint)array.Length, array, size);
        if (sent != array.Length)
        {
            Debug.WriteLine($"[Win32InputSender] SendInput failed: expected={array.Length}, actual={sent}, lastError={Marshal.GetLastWin32Error()}");
        }
        return sent == array.Length;
    }

    private static IntPtr BuildKeyLParam(bool isKeyUp)
    {
        const int previousState = 1 << 30;
        const int transitionState = 1 << 31;
        var flags = isKeyUp ? previousState | transitionState : 0;
        return (IntPtr)flags;
    }

    private static bool IsExtendedKey(VirtualKey key)
    {
        return key == VirtualKey.PageDown || key == VirtualKey.PageUp;
    }

    private static IntPtr BuildWheelWParam(int delta)
    {
        var deltaWord = unchecked((short)delta);
        var packed = (deltaWord << 16) & unchecked((int)0xFFFF0000);
        return new IntPtr(packed);
    }

    private static IntPtr BuildWheelLParam()
    {
        if (!NativeMethods.GetCursorPos(out var point))
        {
            Debug.WriteLine($"[Win32InputSender] GetCursorPos failed: lastError={Marshal.GetLastWin32Error()}");
            return IntPtr.Zero;
        }
        var x = point.X & 0xFFFF;
        var y = point.Y & 0xFFFF;
        var packed = x | (y << 16);
        return new IntPtr(packed);
    }
}
