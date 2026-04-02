using System.Collections.Generic;

namespace ClassroomToolkit.App.Paint;

internal sealed class PaintToolSelectionManager
{
    private readonly Stack<PaintToolMode> _history = new();

    public PaintToolSelectionManager(PaintToolMode initialMode = PaintToolMode.Brush)
    {
        CurrentMode = initialMode;
    }

    public PaintToolMode CurrentMode { get; private set; }

    public PaintToolMode Select(PaintToolMode requestedMode, bool allowToggleOffCurrent)
    {
        if (requestedMode == CurrentMode)
        {
            if (!allowToggleOffCurrent)
            {
                return CurrentMode;
            }

            CurrentMode = ResolveFallbackMode();
            return CurrentMode;
        }

        PushHistory(CurrentMode);
        CurrentMode = requestedMode;
        return CurrentMode;
    }

    public void Reset(PaintToolMode mode)
    {
        _history.Clear();
        CurrentMode = mode;
    }

    private PaintToolMode ResolveFallbackMode()
    {
        while (_history.Count > 0)
        {
            var candidate = _history.Pop();
            if (candidate != CurrentMode && IsHistoryMode(candidate))
            {
                return candidate;
            }
        }

        return PaintToolMode.Brush;
    }

    private void PushHistory(PaintToolMode mode)
    {
        if (!IsHistoryMode(mode))
        {
            return;
        }

        if (_history.Count > 0 && _history.Peek() == mode)
        {
            return;
        }

        _history.Push(mode);
    }

    private static bool IsHistoryMode(PaintToolMode mode)
    {
        return mode != PaintToolMode.Cursor;
    }
}
