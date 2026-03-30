using System;

namespace ClassroomToolkit.App.Session;

public sealed record UiSessionTransition(
    long Id,
    DateTime OccurredAtUtc,
    UiSessionEvent Event,
    UiSessionState Previous,
    UiSessionState Current)
{
    public bool HasStateChange => !Equals(Previous, Current);
}
