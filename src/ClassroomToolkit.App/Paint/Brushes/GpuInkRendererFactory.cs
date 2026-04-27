namespace ClassroomToolkit.App.Paint.Brushes;

internal sealed class GpuInkRendererFactory : IInkRendererFactory
{
    private readonly CpuInkRendererFactory _fallback = new();

    public string BackendId => "gpu";

    public bool CanReuse(PaintBrushStyle brushStyle, IBrushRenderer renderer)
    {
        return _fallback.CanReuse(brushStyle, renderer);
    }

    public IBrushRenderer Create(
        PaintBrushStyle brushStyle,
        MarkerBrushConfig markerConfig,
        BrushPhysicsConfig calligraphyConfig)
    {
        // GPU backend placeholder: until GPU renderer lands, keep behavior identical
        // by using CPU renderer implementation.
        return _fallback.Create(brushStyle, markerConfig, calligraphyConfig);
    }
}
