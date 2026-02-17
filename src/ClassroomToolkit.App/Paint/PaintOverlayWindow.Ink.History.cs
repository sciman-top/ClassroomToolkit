using System;
using System.Collections.Generic;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Shapes;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using ClassroomToolkit.Interop;
using MediaColor = System.Windows.Media.Color;
using WpfPath = System.Windows.Shapes.Path;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaBrush = System.Windows.Media.Brush;
using MediaPen = System.Windows.Media.Pen;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private sealed class RasterSnapshot : IDisposable
    {
        public RasterSnapshot(int width, int height, double dpiX, double dpiY, byte[] pixels)
        {
            PixelWidth = width;
            PixelHeight = height;
            DpiX = dpiX;
            DpiY = dpiY;
            Pixels = pixels;
        }

        public int PixelWidth { get; }
        public int PixelHeight { get; }
        public double DpiX { get; }
        public double DpiY { get; }
        public byte[] Pixels { get; }

        public void Dispose()
        {
            if (Pixels != null)
            {
                PixelPool.Return(Pixels);
            }
        }
    }

    private sealed record InkSnapshot(List<InkStrokeData> Strokes);

    private void PushHistory()
    {
        EnsureRasterSurface();
        if (_rasterSurface == null)
        {
            return;
        }

        var stride = _surfacePixelWidth * 4;
        var bytesRequired = stride * _surfacePixelHeight;
        
        // Check memory pressure and trim if needed
        while (_history.Count > 0 && (_history.Count >= HistoryLimit || _currentHistoryMemoryBytes + bytesRequired > MaxHistoryMemoryBytes))
        {
            var oldest = _history[0];
            _currentHistoryMemoryBytes -= oldest.Pixels.Length;
            oldest.Dispose();
            _history.RemoveAt(0);
        }

        var pixels = PixelPool.Rent(bytesRequired);
        _rasterSurface.CopyPixels(pixels, stride, 0);
        
        var snapshot = new RasterSnapshot(_surfacePixelWidth, _surfacePixelHeight, _surfaceDpiX, _surfaceDpiY, pixels);
        _history.Add(snapshot);
        _currentHistoryMemoryBytes += pixels.Length;

        if (_inkRecordEnabled)
        {
            _inkHistory.Add(new InkSnapshot(CloneInkStrokes(_inkStrokes)));
            if (_inkHistory.Count > HistoryLimit)
            {
                _inkHistory.RemoveAt(0);
            }
        }
    }

    private void RestoreSnapshot(RasterSnapshot snapshot)
    {
        if (_rasterSurface == null
            || snapshot.PixelWidth != _surfacePixelWidth
            || snapshot.PixelHeight != _surfacePixelHeight)
        {
            _rasterSurface = new WriteableBitmap(
                snapshot.PixelWidth,
                snapshot.PixelHeight,
                snapshot.DpiX,
                snapshot.DpiY,
                PixelFormats.Pbgra32,
                null);
            _surfacePixelWidth = snapshot.PixelWidth;
            _surfacePixelHeight = snapshot.PixelHeight;
            _surfaceDpiX = snapshot.DpiX;
            _surfaceDpiY = snapshot.DpiY;
            RasterImage.Source = _rasterSurface;
        }
        var rect = new Int32Rect(0, 0, snapshot.PixelWidth, snapshot.PixelHeight);
        var stride = snapshot.PixelWidth * 4;
        _rasterSurface.WritePixels(rect, snapshot.Pixels, stride, 0);
        _hasDrawing = true;
    }

    private void ClearInkSurfaceState()
    {
        _inkStrokes.Clear();
        ResetInkHistory();
        RedrawInkSurface();
        _inkCacheDirty = false;
    }

    private void ClearInkSurfaceForPresentationExit()
    {
        _activeRenderer?.Reset();
        _visualHost.Clear();
        _strokeInProgress = false;
        _isErasing = false;
        _lastEraserPoint = null;
        _lastCalligraphyPreviewPoint = null;
        _inkStrokes.Clear();
        _hasDrawing = false;
        ResetInkHistory();
        ClearSurface();
        _inkCacheDirty = false;
    }

    private void SaveAndClearInkSurface()
    {
        SaveCurrentPageOnNavigate(forceBackground: false);
        ClearInkSurfaceState();
    }
}
