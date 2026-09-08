using System;
using System.Runtime.InteropServices;
using ClassroomToolkit.Interop;

namespace ClassroomToolkit.App.Windowing;

internal sealed class NativeWindowStyleInteropAdapter : IWindowStyleInteropAdapter
{
    public bool TryGetWindowLong(IntPtr hwnd, int index, out int style, out int errorCode)
    {
        style = NativeMethods.GetWindowLong(hwnd, index);
        errorCode = Marshal.GetLastPInvokeError();
        return IsCallSuccessful(style, errorCode);
    }

    public bool TrySetWindowLong(IntPtr hwnd, int index, int value, out int errorCode)
    {
        var previous = NativeMethods.SetWindowLong(hwnd, index, value);
        errorCode = Marshal.GetLastPInvokeError();
        return IsCallSuccessful(previous, errorCode);
    }

    /// <summary>
    /// GetWindowLong/SetWindowLong 的返回值 0 是合法值（如样式位恰好为 0），而
    /// SetLastError=true 的封送 stub 会在调用后覆写 last-error，user32 成功时又
    /// 不重置它——线程上此前失败的 Win32 调用会残留非零值。因此失败判定必须是
    /// "返回值为 0 且 last-error 非零"的组合，不能只看 last-error。
    /// </summary>
    internal static bool IsCallSuccessful(int returnValue, int errorCode)
    {
        return returnValue != 0 || errorCode == 0;
    }
}
