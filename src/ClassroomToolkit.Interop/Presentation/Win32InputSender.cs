using System.Runtime.InteropServices;

namespace ClassroomToolkit.Interop.Presentation;

public sealed class Win32InputSender : IInputSender
{
    private const uint InputKeyboard = 1;
    private const uint KeyeventfKeyup = 0x0002;
    private const uint KeyeventfExtended = 0x0001;

    public bool SendKey(IntPtr hwnd, VirtualKey key, KeyModifiers modifiers, InputStrategy strategy)
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
            ? SendKeyMessage(hwnd, key, modifiers)
            : SendKeyInput(key, modifiers);
    }

    private static bool SendKeyMessage(IntPtr hwnd, VirtualKey key, KeyModifiers modifiers)
    {
        var downParam = BuildKeyLParam(isKeyUp: false);
        var upParam = BuildKeyLParam(isKeyUp: true);
        if (!SendModifiers(hwnd, modifiers, true))
        {
            return false;
        }
        var down = NativeMethods.PostMessage(hwnd, NativeMethods.WmKeyDown, (IntPtr)key, downParam);
        var up = NativeMethods.PostMessage(hwnd, NativeMethods.WmKeyUp, (IntPtr)key, upParam);
        SendModifiers(hwnd, modifiers, false);
        return down && up;
    }

    private static bool SendKeyInput(VirtualKey key, KeyModifiers modifiers)
    {
        var inputs = new List<NativeMethods.Input>();
        AddModifierInputs(inputs, modifiers, true);
        inputs.Add(CreateKeyInput(key, isKeyUp: false));
        inputs.Add(CreateKeyInput(key, isKeyUp: true));
        AddModifierInputs(inputs, modifiers, false);
        return SendInputs(inputs);
    }

    private static bool SendModifiers(IntPtr hwnd, KeyModifiers modifiers, bool isKeyDown)
    {
        foreach (var mod in EnumerateModifiers(modifiers))
        {
            var msg = isKeyDown ? NativeMethods.WmKeyDown : NativeMethods.WmKeyUp;
            if (!NativeMethods.PostMessage(hwnd, msg, (IntPtr)mod, BuildKeyLParam(!isKeyDown)))
            {
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
}
