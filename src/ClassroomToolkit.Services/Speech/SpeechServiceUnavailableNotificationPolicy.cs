using System.Threading;

namespace ClassroomToolkit.Services.Speech;

internal static class SpeechServiceUnavailableNotificationPolicy
{
    internal static bool IsNotified(ref int notifiedState)
    {
        return Volatile.Read(ref notifiedState) != 0;
    }

    internal static bool ShouldNotify(ref int notifiedState)
    {
        return Interlocked.Exchange(ref notifiedState, 1) == 0;
    }

    internal static void Reset(ref int notifiedState)
    {
        Interlocked.Exchange(ref notifiedState, 0);
    }
}
