namespace ClassroomToolkit.App.Paint;

internal static class InkEraseStrokeChangePolicy
{
    internal static bool ShouldMarkStrokeChanged(
        bool geometryPathChanged,
        bool bloomGeometryChanged,
        bool ribbonGeometryChanged)
    {
        return geometryPathChanged
            || bloomGeometryChanged
            || ribbonGeometryChanged;
    }
}
