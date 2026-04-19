using System;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoPanReleaseTuningPolicy
{
    internal static PhotoPanReleaseTuning Resolve(
        PhotoPanPointerKind pointerKind,
        PhotoPanInertiaTuning tuning)
    {
        return pointerKind switch
        {
            PhotoPanPointerKind.Touch => new PhotoPanReleaseTuning(
                DecelerationDipPerMs2: tuning.MouseDecelerationDipPerMs2 * 0.74,
                StopSpeedDipPerMs: tuning.MouseStopSpeedDipPerMs,
                MinReleaseSpeedDipPerMs: Math.Max(0.03, tuning.MouseMinReleaseSpeedDipPerMs * 0.58),
                MaxReleaseSpeedDipPerMs: Math.Min(6.2, tuning.MouseMaxReleaseSpeedDipPerMs * 1.22),
                MaxDurationMs: Math.Max(1350.0, tuning.MouseMaxDurationMs * 1.15),
                MaxTranslationPerFrameDip: Math.Max(210.0, tuning.MouseMaxTranslationPerFrameDip * 1.2),
                VelocitySampleWindowMs: Math.Max(
                    PhotoPanInertiaDefaults.TouchVelocitySampleWindowMs,
                    PhotoPanInertiaDefaults.MouseVelocitySampleWindowMs * 1.25),
                MaxVelocitySampleAgeMs: Math.Max(
                    PhotoPanInertiaDefaults.TouchMaxVelocitySampleAgeMs,
                    PhotoPanInertiaDefaults.MouseMaxVelocitySampleAgeMs * 1.4),
                MinVelocitySampleDistanceDip: Math.Min(
                    PhotoPanInertiaDefaults.MouseMinVelocitySampleDistanceDip,
                    PhotoPanInertiaDefaults.TouchMinVelocitySampleDistanceDip),
                VelocityRecentWeightGain: Math.Max(
                    PhotoPanInertiaDefaults.TouchVelocityRecentWeightGain,
                    PhotoPanInertiaDefaults.MouseVelocityRecentWeightGain + 0.15)),
            _ => new PhotoPanReleaseTuning(
                DecelerationDipPerMs2: tuning.MouseDecelerationDipPerMs2,
                StopSpeedDipPerMs: tuning.MouseStopSpeedDipPerMs,
                MinReleaseSpeedDipPerMs: tuning.MouseMinReleaseSpeedDipPerMs,
                MaxReleaseSpeedDipPerMs: tuning.MouseMaxReleaseSpeedDipPerMs,
                MaxDurationMs: tuning.MouseMaxDurationMs,
                MaxTranslationPerFrameDip: tuning.MouseMaxTranslationPerFrameDip,
                VelocitySampleWindowMs: PhotoPanInertiaDefaults.MouseVelocitySampleWindowMs,
                MaxVelocitySampleAgeMs: PhotoPanInertiaDefaults.MouseMaxVelocitySampleAgeMs,
                MinVelocitySampleDistanceDip: PhotoPanInertiaDefaults.MouseMinVelocitySampleDistanceDip,
                VelocityRecentWeightGain: PhotoPanInertiaDefaults.MouseVelocityRecentWeightGain)
        };
    }
}
