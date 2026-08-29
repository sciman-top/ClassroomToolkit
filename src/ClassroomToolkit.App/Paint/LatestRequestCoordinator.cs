namespace ClassroomToolkit.App.Paint;

/// <summary>
/// Serializes a background request stream while retaining the newest request.
/// A newer request invalidates the currently running work; when that work
/// completes, the newest request is admitted exactly once.
/// </summary>
internal sealed class LatestRequestCoordinator<TRequest>
{
    private readonly object _gate = new();
    private TRequest? _latestRequest;
    private long _generation;
    private long _activeGeneration;
    private bool _hasLatestRequest;
    private bool _inFlight;

    internal bool TryBegin(TRequest request, out LatestRequestTicket<TRequest> ticket)
    {
        lock (_gate)
        {
            _latestRequest = request;
            _hasLatestRequest = true;
            _generation++;
            if (_inFlight)
            {
                ticket = default;
                return false;
            }

            _inFlight = true;
            _activeGeneration = _generation;
            ticket = new LatestRequestTicket<TRequest>(_generation, request);
            return true;
        }
    }

    internal bool IsCurrent(LatestRequestTicket<TRequest> ticket)
    {
        lock (_gate)
        {
            return _hasLatestRequest && ticket.Generation == _generation;
        }
    }

    internal bool TryComplete(
        LatestRequestTicket<TRequest> ticket,
        out LatestRequestTicket<TRequest> nextTicket)
    {
        lock (_gate)
        {
            if (!_inFlight || _activeGeneration != ticket.Generation)
            {
                nextTicket = default;
                return false;
            }

            _inFlight = false;
            _activeGeneration = 0;
            if (!_hasLatestRequest || ticket.Generation == _generation)
            {
                nextTicket = default;
                return false;
            }

            _inFlight = true;
            _activeGeneration = _generation;
            nextTicket = new LatestRequestTicket<TRequest>(_generation, _latestRequest!);
            return true;
        }
    }

    internal void Invalidate()
    {
        lock (_gate)
        {
            _generation++;
            _activeGeneration = 0;
            _latestRequest = default;
            _hasLatestRequest = false;
            _inFlight = false;
        }
    }
}

internal readonly record struct LatestRequestTicket<TRequest>(long Generation, TRequest Request);
