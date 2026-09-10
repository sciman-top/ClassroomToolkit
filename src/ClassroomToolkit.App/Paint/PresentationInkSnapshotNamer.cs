using System;
using System.Globalization;

namespace ClassroomToolkit.App.Paint;

internal static class PresentationInkSnapshotNamer
{
    internal static string BuildFileName(DateTime localTime)
    {
        return $"presentation_{localTime.ToString("yyyyMMdd_HHmmssfff", CultureInfo.InvariantCulture)}.png";
    }
}
