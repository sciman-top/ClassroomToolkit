using System;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPagePostInputRefreshSlotAcquireResult(
    bool Acquired,
    long PointerUpSequence);

internal static class CrossPagePostInputRefreshSlotCoordinator
{
    internal delegate long ReadAppliedSequenceDelegate();
    internal delegate long CompareExchangeAppliedSequenceDelegate(long nextValue, long comparand);

    internal static CrossPagePostInputRefreshSlotAcquireResult TryAcquire(
        long pointerUpSequence,
        DateTime lastPointerUpUtc,
        ReadAppliedSequenceDelegate readAppliedSequence,
        CompareExchangeAppliedSequenceDelegate compareExchangeAppliedSequence)
    {
        ArgumentNullException.ThrowIfNull(readAppliedSequence);
        ArgumentNullException.ThrowIfNull(compareExchangeAppliedSequence);

        if (lastPointerUpUtc == CrossPageRuntimeDefaults.UnsetTimestampUtc)
        {
            return new CrossPagePostInputRefreshSlotAcquireResult(
                Acquired: true,
                PointerUpSequence: pointerUpSequence);
        }

        while (true)
        {
            var appliedSequence = readAppliedSequence();
            if (appliedSequence == pointerUpSequence)
            {
                return new CrossPagePostInputRefreshSlotAcquireResult(
                    Acquired: false,
                    PointerUpSequence: pointerUpSequence);
            }

            var exchanged = compareExchangeAppliedSequence(pointerUpSequence, appliedSequence);
            if (exchanged == appliedSequence)
            {
                return new CrossPagePostInputRefreshSlotAcquireResult(
                    Acquired: true,
                    PointerUpSequence: pointerUpSequence);
            }
        }
    }
}
