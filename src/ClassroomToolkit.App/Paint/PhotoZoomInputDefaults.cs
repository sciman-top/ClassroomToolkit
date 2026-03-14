namespace ClassroomToolkit.App.Paint;

internal static class PhotoZoomInputDefaults
{
    internal const double WheelZoomBaseDefault = 1.0008;
    internal const double WheelZoomBaseMin = 1.0002;
    internal const double WheelZoomBaseMax = 1.0020;
    internal const double GestureSensitivityDefault = 1.0;
    internal const double GestureSensitivityMin = 0.5;
    internal const double GestureSensitivityMax = 1.8;
    internal const double GestureZoomNoiseThreshold = 0.01;
    internal const double ZoomMinEventFactor = 0.85;
    internal const double ZoomMaxEventFactor = 1.18;
    internal const double ScaleApplyEpsilon = 0.001;
    internal const double ManipulationTranslationEpsilonDip = 0.01;
}
