namespace ClassroomToolkit.App.Paint.Brushes;

/// <summary>
/// Shared preview-tail budget for live stroke rendering. Both brush renderers
/// reuse the same window/stride so preview smoothness stays consistent when the
/// teacher switches tool.
/// </summary>
internal static class BrushPreviewDefaults
{
    internal const int PreviewTailPointWindow = 56;
    internal const int PreviewBaseRefreshStride = 14;
}
