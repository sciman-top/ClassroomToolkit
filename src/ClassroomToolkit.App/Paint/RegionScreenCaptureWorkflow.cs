using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using ClassroomToolkit.App.Helpers;

namespace ClassroomToolkit.App.Paint;

internal enum RegionScreenCaptureCancelReason
{
    None = 0,
    UserCanceled = 1,
    ToolbarPassthroughCanceled = 2
}

internal readonly record struct RegionScreenCaptureResult(
    bool Succeeded,
    string? FilePath,
    RegionScreenCaptureCancelReason CancelReason);

internal static class RegionScreenCaptureWorkflow
{
    private const string CaptureDirectoryName = "Captures";
    private const string SessionCaptureDirectoryName = "SessionCaptures";
    private const string CaptureFilePrefix = "capture-";

    internal static RegionScreenCaptureResult TryCaptureToPng(IReadOnlyCollection<Rectangle>? passthroughRegions = null)
    {
        var virtualBounds = SystemInformation.VirtualScreen;
        if (virtualBounds.Width <= 0 || virtualBounds.Height <= 0)
        {
            return new RegionScreenCaptureResult(false, null, RegionScreenCaptureCancelReason.UserCanceled);
        }

        var selector = new RegionSelectionOverlayWindow(virtualBounds, passthroughRegions);
        var accepted = selector.SafeShowDialog() == true;
        if (!accepted || !selector.TryGetSelection(out var selection))
        {
            var cancelReason = selector.CanceledByPassthrough
                ? RegionScreenCaptureCancelReason.ToolbarPassthroughCanceled
                : RegionScreenCaptureCancelReason.UserCanceled;
            return new RegionScreenCaptureResult(false, null, cancelReason);
        }

        return TryCaptureSelection(virtualBounds, selection);
    }

    private static RegionScreenCaptureResult TryCaptureSelection(Rectangle virtualBounds, Rectangle selection)
    {
        var target = Rectangle.Intersect(virtualBounds, selection);
        if (target.Width <= 0 || target.Height <= 0)
        {
            return new RegionScreenCaptureResult(false, null, RegionScreenCaptureCancelReason.UserCanceled);
        }

        var outputDir = GetSessionCaptureRootDirectory();
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, $"{CaptureFilePrefix}{DateTime.Now:yyyyMMdd-HHmmss-fff}.png");

        using var full = new Bitmap(virtualBounds.Width, virtualBounds.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(full))
        {
            graphics.CopyFromScreen(
                virtualBounds.Left,
                virtualBounds.Top,
                0,
                0,
                full.Size,
                CopyPixelOperation.SourceCopy);
        }

        var localRect = new Rectangle(
            target.Left - virtualBounds.Left,
            target.Top - virtualBounds.Top,
            target.Width,
            target.Height);

        using var cropped = full.Clone(localRect, PixelFormat.Format32bppArgb);
        cropped.Save(outputPath, ImageFormat.Png);
        return new RegionScreenCaptureResult(true, outputPath, RegionScreenCaptureCancelReason.None);
    }

    internal static string GetPersistentCaptureRootDirectory()
    {
        var picturesRoot = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (string.IsNullOrWhiteSpace(picturesRoot))
        {
            picturesRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        return Path.Combine(picturesRoot, "ClassroomToolkit", CaptureDirectoryName);
    }

    internal static string GetSessionCaptureRootDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.GetTempPath();
        }

        return Path.Combine(localAppData, "ClassroomToolkit", SessionCaptureDirectoryName);
    }

    internal static bool IsSessionRegionCaptureFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(path);
        var fileName = Path.GetFileName(fullPath);
        if (!fileName.StartsWith(CaptureFilePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var captureRoot = Path.GetFullPath(GetSessionCaptureRootDirectory());
        if (!captureRoot.EndsWith(Path.DirectorySeparatorChar))
        {
            captureRoot += Path.DirectorySeparatorChar;
        }

        return fullPath.StartsWith(captureRoot, StringComparison.OrdinalIgnoreCase);
    }
}
