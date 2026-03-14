using System;
using System.Windows;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoManipulationDeltaExecutionPolicy
{
    internal static PhotoManipulationDeltaExecutionPlan Resolve(
        Vector translation,
        double translationEpsilonDip,
        bool crossPageDisplayActive)
    {
        var shouldApplyTranslation =
            Math.Abs(translation.X) > translationEpsilonDip
            || Math.Abs(translation.Y) > translationEpsilonDip;
        return new PhotoManipulationDeltaExecutionPlan(
            ShouldApplyTranslation: shouldApplyTranslation,
            ShouldLogPanTelemetry: shouldApplyTranslation,
            ShouldRequestCrossPageUpdate: crossPageDisplayActive);
    }
}
