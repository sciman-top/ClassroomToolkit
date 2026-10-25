using System;

namespace ClassroomToolkit.App.RollCall;

internal static class RollCallRuntimeDefaults
{
    internal static readonly DateTime UnsetTimestampUtc = DateTime.MinValue;
    internal const int ClassSwitchSuppressMs = 250;
}
