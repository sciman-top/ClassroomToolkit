using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

public sealed class InkRibbonData
{
    public string GeometryPath { get; set; } = string.Empty;
    public double Opacity { get; set; } = 0.28;
    public double RibbonT { get; set; }
}

[SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "List properties are part of the persisted ink JSON contract.")]
[SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setters are required for JSON deserialization compatibility.")]
public sealed class InkStrokeData
{
    public InkStrokeType Type { get; set; } = InkStrokeType.Brush;
    public PaintBrushStyle BrushStyle { get; set; } = PaintBrushStyle.StandardRibbon;
    public string GeometryPath { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#FF0000";
    public byte Opacity { get; set; } = 255;
    public double BrushSize { get; set; } = 12.0;
    public int MaskSeed { get; set; }
    public double InkFlow { get; set; } = 1.0;
    public double StrokeDirectionX { get; set; } = 1.0;
    public double StrokeDirectionY { get; set; }
    public bool CalligraphyInkBloomEnabled { get; set; } = true;
    public bool CalligraphySealEnabled { get; set; } = true;
    public byte CalligraphyOverlayOpacityThreshold { get; set; } = 230;
    public CalligraphyRenderMode CalligraphyRenderMode { get; set; } = CalligraphyRenderMode.Clarity;
    public double ReferenceWidth { get; set; }
    public double ReferenceHeight { get; set; }
    public List<InkRibbonData> Ribbons { get; set; } = new();
    public List<InkBloomData> Blooms { get; set; } = new();

    [JsonIgnore]
    public string DebugLabel => $"{Type}-{BrushStyle}-{GeometryPath.Length}";

    [JsonIgnore]
    public System.Windows.Media.Geometry? CachedGeometry { get; set; }

    [JsonIgnore]
    public System.Windows.Rect? CachedBounds { get; set; }

    [JsonIgnore]
    public List<System.Windows.Media.Geometry>? CachedRibbonGeometries { get; set; }
}

[SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "List property is part of the persisted ink JSON contract.")]
[SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter is required for JSON deserialization compatibility.")]
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
