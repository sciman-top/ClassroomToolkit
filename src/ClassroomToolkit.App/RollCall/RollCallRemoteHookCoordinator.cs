namespace ClassroomToolkit.App.RollCall;

internal readonly record struct RollCallRemoteHookStartRequest(
    bool ShouldEnable,
    string ConfiguredKey,
    string FallbackToken,
    Action Handler,
    Func<bool> ShouldKeepActive,
    bool AlreadyUnavailableNotified,
    bool NotifyUnavailableOnFailure);

internal readonly record struct RollCallRemoteHookStartResult(
    bool Started,
    bool ShouldNotifyUnavailable);

internal sealed class RollCallRemoteHookCoordinator
{
    private readonly Func<IEnumerable<string>, Action, Func<bool>, Task<bool>> _registerHookAsync;
    private readonly Func<string, string, IReadOnlyList<string>> _resolveBindings;
    private readonly Action _unregisterAll;

    internal RollCallRemoteHookCoordinator(
        Func<IEnumerable<string>, Action, Func<bool>, Task<bool>> registerHookAsync,
        Func<string, string, IReadOnlyList<string>> resolveBindings,
        Action unregisterAll)
    {
        _registerHookAsync = registerHookAsync ?? throw new ArgumentNullException(nameof(registerHookAsync));
        _resolveBindings = resolveBindings ?? throw new ArgumentNullException(nameof(resolveBindings));
        _unregisterAll = unregisterAll ?? throw new ArgumentNullException(nameof(unregisterAll));
    }

    internal void StopAllHooks()
    {
        _unregisterAll();
    }

    internal async Task<RollCallRemoteHookStartResult> TryStartAsync(RollCallRemoteHookStartRequest request)
    {
        if (!request.ShouldEnable)
        {
            return new RollCallRemoteHookStartResult(Started: false, ShouldNotifyUnavailable: false);
        }
        if (!request.ShouldKeepActive())
        {
            return new RollCallRemoteHookStartResult(Started: false, ShouldNotifyUnavailable: false);
        }

        var bindings = _resolveBindings(request.ConfiguredKey, request.FallbackToken);
        var started = await _registerHookAsync(bindings, request.Handler, request.ShouldKeepActive);
        var shouldNotifyUnavailable =
            request.NotifyUnavailableOnFailure
            && !started
            && !request.AlreadyUnavailableNotified
            && request.ShouldEnable
            && request.ShouldKeepActive();

        return new RollCallRemoteHookStartResult(
            Started: started,
            ShouldNotifyUnavailable: shouldNotifyUnavailable);
    }
}
