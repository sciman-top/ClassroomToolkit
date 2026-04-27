namespace ClassroomToolkit.App.Paint.Brushes;

internal sealed class CpuInkRendererFactory : IInkRendererFactory
{
    public string BackendId => "cpu";

    public bool CanReuse(PaintBrushStyle brushStyle, IBrushRenderer renderer)
    {
        return brushStyle switch
        {
            PaintBrushStyle.Calligraphy => renderer is VariableWidthBrushRenderer,
            PaintBrushStyle.StandardRibbon => renderer is MarkerBrushRenderer marker
                && marker.RenderMode == MarkerRenderMode.Ribbon,
            _ => renderer is MarkerBrushRenderer marker
                && marker.RenderMode == MarkerRenderMode.SegmentUnion
        };
    }

    public IBrushRenderer Create(
        PaintBrushStyle brushStyle,
        MarkerBrushConfig markerConfig,
        BrushPhysicsConfig calligraphyConfig)
    {
        return brushStyle switch
        {
            PaintBrushStyle.Calligraphy => new VariableWidthBrushRenderer(calligraphyConfig),
            PaintBrushStyle.StandardRibbon => new MarkerBrushRenderer(MarkerRenderMode.Ribbon, markerConfig),
            _ => new MarkerBrushRenderer(MarkerRenderMode.SegmentUnion, markerConfig)
        };
    }
}
