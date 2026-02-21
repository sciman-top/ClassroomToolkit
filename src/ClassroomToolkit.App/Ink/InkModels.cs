using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;
using ClassroomToolkit.App.Paint;

namespace ClassroomToolkit.App.Ink;

public enum InkStrokeType
{
    Brush = 0,
    Shape = 1
}

public sealed class InkBloomData
{
    public string GeometryPath { get; set; } = string.Empty;
    public double Opacity { get; set; } = 1.0;
}

public sealed class InkStrokeData
{
    public InkStrokeType Type { get; set; } = InkStrokeType.Brush;
    public PaintBrushStyle BrushStyle { get; set; } = PaintBrushStyle.StandardRibbon;
    public string GeometryPath { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#FF0000";
    public byte Opacity { get; set; } = 255;
    public double BrushSize { get; set; } = 12.0;
    public int MaskSeed { get; set; } = 0;
    public double InkFlow { get; set; } = 1.0;
    public double StrokeDirectionX { get; set; } = 1.0;
    public double StrokeDirectionY { get; set; } = 0.0;
    public bool CalligraphyInkBloomEnabled { get; set; } = true;
    public bool CalligraphySealEnabled { get; set; } = true;
    public byte CalligraphyOverlayOpacityThreshold { get; set; } = 230;
    public double ReferenceWidth { get; set; }
    public double ReferenceHeight { get; set; }
    public List<InkBloomData> Blooms { get; set; } = new();

    [JsonIgnore]
    public string DebugLabel => $"{Type}-{BrushStyle}-{GeometryPath.Length}";

    [JsonIgnore]
    public System.Windows.Media.Geometry? CachedGeometry { get; set; }

    [JsonIgnore]
    public System.Windows.Rect? CachedBounds { get; set; }
}

public sealed class InkPageData
{
    public int PageIndex { get; set; } = 1;
    public string DocumentName { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string BackgroundImageFile { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<InkStrokeData> Strokes { get; set; } = new();

    [JsonIgnore]
    public string PageKey => $"slide_{PageIndex.ToString("D3", CultureInfo.InvariantCulture)}";
}
