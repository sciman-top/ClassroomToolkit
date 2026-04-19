namespace ClassroomToolkit.App.Paint;

internal static class PaintPresetDefaults
{
    internal const int WpsDebounceBalancedMs = 120;
    internal const int WpsDebounceResponsiveMs = 80;
    internal const int WpsDebounceStableMs = 200;
    internal const int WpsDebounceDualScreenMs = 160;
    internal const int WpsDebounceLegacyDefaultMs = 200;
    internal const int WpsDebounceDefaultMs = WpsDebounceBalancedMs;

    internal const int PostInputRefreshDefaultMs = 120;

    internal const int PostInputBalancedMs = 120;
    internal const int PostInputResponsiveMs = 80;
    internal const int PostInputStableMs = 140;
    internal const int PostInputDualScreenMs = 160;

    internal const double WheelZoomBalanced = 1.0008;
    internal const double WheelZoomResponsive = 1.0010;
    internal const double WheelZoomStable = 1.0006;
    internal const double WheelZoomDualScreen = 1.0007;

    internal const double GestureSensitivityResponsive = 1.2;
    internal const double GestureSensitivityStable = 0.8;
    internal const double GestureSensitivityDualScreen = 0.9;

    internal const string InertiaProfileBalanced = PhotoInertiaProfileDefaults.Standard;
    internal const string InertiaProfileResponsive = PhotoInertiaProfileDefaults.Sensitive;
    internal const string InertiaProfileStable = PhotoInertiaProfileDefaults.Heavy;
    internal const string InertiaProfileDualScreen = PhotoInertiaProfileDefaults.Heavy;
}
