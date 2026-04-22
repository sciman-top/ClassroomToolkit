using System;

using System.Globalization;

namespace ClassroomToolkit.App.Photos;

internal static class PhotoNavigationDiagnosticsTimestampPolicy
{
    internal static string Format(DateTime localTimestamp)
    {
        return localTimestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}
