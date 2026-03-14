namespace ClassroomToolkit.App.Paint.Brushes;

public interface IInkRendererFactory
{
    string BackendId { get; }

    bool CanReuse(PaintBrushStyle brushStyle, IBrushRenderer renderer);

    IBrushRenderer Create(
        PaintBrushStyle brushStyle,
        MarkerBrushConfig markerConfig,
        BrushPhysicsConfig calligraphyConfig);
}
