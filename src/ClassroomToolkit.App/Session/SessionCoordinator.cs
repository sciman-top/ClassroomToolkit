using System;
using System.Collections.Generic;
using System.Diagnostics;
using ClassroomToolkit.App;

namespace ClassroomToolkit.App.Session;

public sealed class SessionCoordinator
{
    private readonly object _sync = new();
    private readonly IUiSessionEffectRunner _effectRunner;
    private IReadOnlyList<string> _lastViolations = Array.Empty<string>();
    private UiSessionState _currentState;
    private long _transitionId;

    public SessionCoordinator(IUiSessionEffectRunner effectRunner, UiSessionState? initialState = null)
    {
        _effectRunner = effectRunner ?? throw new ArgumentNullException(nameof(effectRunner));
        _currentState = initialState ?? UiSessionState.Default;
    }

    public UiSessionState CurrentState
    {
        get
        {
            lock (_sync)
            {
                return _currentState;
            }
        }
    }

    public IReadOnlyList<string> LastViolations
    {
        get
        {
            lock (_sync)
            {
                return _lastViolations;
            }
        }
    }

    public UiSessionTransition Dispatch(UiSessionEvent sessionEvent)
    {
        if (sessionEvent == null)
        {
            throw new ArgumentNullException(nameof(sessionEvent));
        }

        UiSessionTransition transition;
        lock (_sync)
        {
            var previous = _currentState;
            var current = UiSessionReducer.Reduce(previous, sessionEvent);
            _lastViolations = UiSessionInvariants.Validate(current);
            transition = new UiSessionTransition(
                Id: ++_transitionId,
                OccurredAtUtc: DateTime.UtcNow,
                Event: sessionEvent,
                Previous: previous,
                Current: current);
            _currentState = current;
        }

        if (_lastViolations.Count > 0)
        {
            Debug.WriteLine(
                $"[SessionCoordinator] invariant violations #{transition.Id}: {string.Join(" | ", _lastViolations)}");
        }

        try
        {
            _effectRunner.Run(transition);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[SessionCoordinator] effect runner failed: {ex.GetType().Name}: {ex.Message}");
        }

        return transition;
    }
}
