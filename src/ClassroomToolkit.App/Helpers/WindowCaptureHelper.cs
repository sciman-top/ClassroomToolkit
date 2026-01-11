using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ClassroomToolkit.App.Helpers;

public static class WindowCaptureHelper
{
    public static bool TryCaptureWindow(IntPtr hwnd, string outputPath)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return false;
        }
        if (!GetWindowRect(hwnd, out var rect))
        {
            return false;
        }
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            return false;
        }
        try
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
            using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            bool printed = false;
            using (var graphics = Graphics.FromImage(bitmap))
            {
                var hdc = graphics.GetHdc();
                try
                {
                    printed = PrintWindow(hwnd, hdc, 0);
                }
                finally
                {
                    graphics.ReleaseHdc(hdc);
                }
                if (!printed)
                {
                    graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
                }
            }
            bitmap.Save(outputPath, ImageFormat.Png);
        }
        catch
        {
            return false;
        }
        return true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect lpRect);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
