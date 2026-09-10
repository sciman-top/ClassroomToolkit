using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Utilities;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void TryCapturePresentationExitSnapshot()
    {
        if (!PresentationInkExitSnapshotPolicy.ShouldCapture(_hasDrawing, _inkStrokes.Count))
        {
            return;
        }
        var surface = _rasterSurface;
        if (surface == null)
        {
            return;
        }

        byte[] pngBytes;
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(surface));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            pngBytes = stream.ToArray();
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[PresentationSnapshot] encode failed: {ex.GetType().Name} - {ex.Message}");
            return;
        }

        var fileName = PresentationInkSnapshotNamer.BuildFileName(DateTime.Now);
        var date = DateTime.Today;
        _ = SafeTaskRunner.Run(
            "PaintOverlayWindow.PresentationExitSnapshot",
            _ => _inkStorage.SavePresentationSnapshot(date, fileName, pngBytes),
            ex => Debug.WriteLine(
                $"[PresentationSnapshot] save failed: {ex.GetType().Name} - {ex.Message}"),
            _overlayLifecycleCancellation.Token);
    }
}
