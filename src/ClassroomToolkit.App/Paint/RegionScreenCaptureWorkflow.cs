using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using System.Windows.Threading;
using ClassroomToolkit.App;
using ClassroomToolkit.App.Helpers;

namespace ClassroomToolkit.App.Paint;

internal enum RegionScreenCaptureCancelReason
{
    None = 0,
    UserCanceled = 1,
    ToolbarPassthroughCanceled = 2
}

internal enum RegionScreenCapturePassthroughInputKind
{
    None = 0,
    PointerMove = 1,
    PointerPress = 2,
    ToolbarHandledPress = 3
}

internal readonly record struct RegionScreenCaptureResult(
    bool Succeeded,
    string? FilePath,
    RegionScreenCaptureCancelReason CancelReason,
    RegionScreenCapturePassthroughInputKind PassthroughInputKind = RegionScreenCapturePassthroughInputKind.None,
    Point? PassthroughScreenPoint = null);

internal static class RegionScreenCaptureWorkflow
{
    private const string CaptureDirectoryName = "Captures";
    private const string SessionCaptureDirectoryName = "SessionCaptures";
    private const string CaptureFilePrefix = "capture-";
    private static RegionSelectionOverlayWindow? _activeSelector;

    internal static RegionScreenCaptureResult TryCaptureToPng(
        IReadOnlyCollection<Rectangle>? passthroughRegions = null,
        Point? initialPointerScreenPoint = null,
        bool deferInitialPassthroughCancelUntilPointerLeaves = false)
    {
        var virtualBounds = SystemInformation.VirtualScreen;
        if (virtualBounds.Width <= 0 || virtualBounds.Height <= 0)
        {
            return new RegionScreenCaptureResult(false, null, RegionScreenCaptureCancelReason.UserCanceled);
        }

        var cursorPosition = initialPointerScreenPoint ?? Cursor.Position;
        var initialPassthroughDecision = RegionCaptureInitialPassthroughPolicy.Resolve(
            cursorPosition.X,
            cursorPosition.Y,
            passthroughRegions);
        if (initialPassthroughDecision.ShouldCancel && !deferInitialPassthroughCancelUntilPointerLeaves)
        {
            return new RegionScreenCaptureResult(
                false,
                null,
                RegionScreenCaptureCancelReason.ToolbarPassthroughCanceled,
                initialPassthroughDecision.InputKind,
                initialPassthroughDecision.ScreenPoint);
        }

        var selector = new RegionSelectionOverlayWindow(
            virtualBounds,
            passthroughRegions,
            deferInitialPassthroughCancelUntilPointerLeaves ? cursorPosition : null);
        var accepted = ShowSelectionOverlay(selector);
        // PushFrame 嵌套消息泵期间定时器照常触发，应用可能已在框选中被自动退出/关停；
        // 关停后不得继续执行窗口续接逻辑（进入照片模式/操作已关闭的工具条）。
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher
            && (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished))
        {
            return new RegionScreenCaptureResult(false, null, RegionScreenCaptureCancelReason.UserCanceled);
        }
        if (!accepted || !selector.TryGetSelection(out var selection))
        {
            var cancelReason = selector.CanceledByPassthrough
                ? RegionScreenCaptureCancelReason.ToolbarPassthroughCanceled
                : RegionScreenCaptureCancelReason.UserCanceled;
            return new RegionScreenCaptureResult(
                false,
                null,
                cancelReason,
                selector.PassthroughInputKind,
                selector.PassthroughScreenPoint);
        }

        return TryCaptureSelection(virtualBounds, selection);
    }

    private static bool ShowSelectionOverlay(RegionSelectionOverlayWindow selector)
    {
        var frame = new DispatcherFrame();
        void OnClosed(object? sender, EventArgs e) => frame.Continue = false;

        selector.Closed += OnClosed;
        _activeSelector = selector;
        try
        {
            selector.Show();
            Dispatcher.PushFrame(frame);
            return selector.SelectionAccepted;
        }
        finally
        {
            selector.Closed -= OnClosed;
            if (selector.IsVisible)
            {
                selector.Close();
            }

            if (ReferenceEquals(_activeSelector, selector))
            {
                _activeSelector = null;
            }
        }
    }

    internal static bool CancelActiveSelectionFromToolbarHandledPress()
    {
        return _activeSelector?.CancelFromToolbarHandledPress() == true;
    }

    internal static bool CancelActiveSelectionFromToolbarPointerMove()
    {
        return _activeSelector?.CancelFromToolbarPointerMove() == true;
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

        using var captured = new Bitmap(target.Width, target.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(captured))
        {
            graphics.CopyFromScreen(
                target.Left,
                target.Top,
                0,
                0,
                target.Size,
                CopyPixelOperation.SourceCopy);
        }

        captured.Save(outputPath, ImageFormat.Png);
        return new RegionScreenCaptureResult(true, outputPath, RegionScreenCaptureCancelReason.None);
    }

    internal static string GetPersistentCaptureRootDirectory()
    {
        if (PortableRuntimeContext.DataDirectory is { } portableDataRoot)
        {
            return Path.Combine(portableDataRoot, CaptureDirectoryName);
        }

        var picturesRoot = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (string.IsNullOrWhiteSpace(picturesRoot))
        {
            picturesRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        return Path.Combine(picturesRoot, "ClassroomToolkit", CaptureDirectoryName);
    }

    internal static string GetSessionCaptureRootDirectory()
    {
        if (PortableRuntimeContext.DataDirectory is { } portableDataRoot)
        {
            return Path.Combine(portableDataRoot, SessionCaptureDirectoryName);
        }

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

        try
        {
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
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine(
                $"RegionScreenCaptureWorkflow: invalid session capture path ignored. reason={ex.GetType().Name}:{ex.Message}");
            return false;
        }
    }
}
