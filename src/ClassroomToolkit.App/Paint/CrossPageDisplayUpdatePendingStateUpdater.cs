using System;
using System.Threading;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageDisplayUpdatePendingStateUpdater
{
    internal static void MarkDirectScheduled(ref CrossPageDisplayUpdateRuntimeState state)
    {
        MarkDirectScheduled(ref state, DateTime.UtcNow);
    }

    internal static void MarkDirectScheduled(
        ref CrossPageDisplayUpdateRuntimeState state,
        DateTime nowUtc)
    {
        state = state with
        {
            Pending = true,
            PendingSinceUtc = nowUtc
        };
    }

    internal static int MarkDelayedScheduled(ref CrossPageDisplayUpdateRuntimeState state)
    {
        return MarkDelayedScheduled(ref state, DateTime.UtcNow);
    }

    internal static int MarkDelayedScheduled(
        ref CrossPageDisplayUpdateRuntimeState state,
        DateTime nowUtc)
    {
        state = state with
        {
            Pending = true,
            Token = state.Token + 1,
            PendingSinceUtc = nowUtc
        };
        return state.Token;
    }

    internal static void MarkPendingCleared(ref CrossPageDisplayUpdateRuntimeState state)
    {
        state = state with
        {
            Pending = false,
            PendingSinceUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc
        };
    }

    internal static bool IsTokenMatched(
        CrossPageDisplayUpdateRuntimeState state,
        int token)
    {
        return token == state.Token;
    }

    internal static void MarkDirectScheduled(ref bool pending)
    {
        pending = true;
    }

    internal static int MarkDelayedScheduled(ref bool pending, ref int token)
    {
        pending = true;
        return Interlocked.Increment(ref token);
    }

    internal static void MarkPendingCleared(ref bool pending)
    {
        pending = false;
    }
}
