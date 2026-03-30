using System.Threading;

namespace ClassroomToolkit.App;

internal static class SpeechUnavailableNotificationPolicy
{
    internal static bool ShouldNotify(ref int notifiedState)
    {
        return Interlocked.Exchange(ref notifiedState, 1) == 0;
    }
}
