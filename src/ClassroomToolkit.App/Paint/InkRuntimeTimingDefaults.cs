using System;

namespace ClassroomToolkit.App.Paint;

internal static class InkRuntimeTimingDefaults
{
    internal const int CalligraphyPreviewMinIntervalMs = 16;
    internal const int InputCooldownMs = 120;
    internal const int MonitorActiveIntervalMs = 600;
    internal const int MonitorIdleIntervalMs = 1400;
    internal const int IdleThresholdMs = 2500;
    internal const int RedrawMinIntervalMs = 16;
    internal const double PhotoPanRedrawThresholdDip = 3.0;
    internal const int RedrawDispatchDelayMinMs = 1;
    internal const int SidecarAutoSaveDelayMs = 600;
    internal const int SidecarAutoSaveRetryMax = 3;
    internal const int SidecarAutoSaveRetryDelayMs = 900;
    internal const int CalligraphyAdaptiveAdjustMinIntervalMs = 200;
    internal static readonly DateTime UnsetTimestampUtc = DateTime.MinValue;
}
