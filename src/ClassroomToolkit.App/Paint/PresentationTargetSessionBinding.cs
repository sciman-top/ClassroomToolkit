using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.App.Paint;

/// <summary>
/// Keeps a presentation channel bound to one admitted HWND for the lifetime of
/// the current presentation session.  Window enumeration remains the recovery
/// path, not the normal destination selection path for every input event.
/// </summary>
internal sealed class PresentationTargetSessionBinding
{
    private readonly object _sync = new();
    private PresentationTarget _wpsTarget = PresentationTarget.Empty;
    private PresentationTarget _officeTarget = PresentationTarget.Empty;

    internal PresentationTarget Resolve(
        PresentationType type,
        Func<PresentationTarget> resolveCandidate,
        Func<PresentationTarget, bool> isAdmitted)
    {
        ArgumentNullException.ThrowIfNull(resolveCandidate);
        ArgumentNullException.ThrowIfNull(isAdmitted);

        lock (_sync)
        {
            var bound = Get(type);
            if (bound.IsValid && isAdmitted(bound))
            {
                return bound;
            }

            Set(type, PresentationTarget.Empty);
            var candidate = resolveCandidate();
            if (!candidate.IsValid || !isAdmitted(candidate))
            {
                return PresentationTarget.Empty;
            }

            Set(type, candidate);
            return candidate;
        }
    }

    internal void Invalidate(PresentationType type)
    {
        lock (_sync)
        {
            Set(type, PresentationTarget.Empty);
        }
    }

    internal void InvalidateAll()
    {
        lock (_sync)
        {
            _wpsTarget = PresentationTarget.Empty;
            _officeTarget = PresentationTarget.Empty;
        }
    }

    private PresentationTarget Get(PresentationType type)
    {
        return type switch
        {
            PresentationType.Wps => _wpsTarget,
            PresentationType.Office => _officeTarget,
            _ => PresentationTarget.Empty
        };
    }

    private void Set(PresentationType type, PresentationTarget target)
    {
        switch (type)
        {
            case PresentationType.Wps:
                _wpsTarget = target;
                break;
            case PresentationType.Office:
                _officeTarget = target;
                break;
        }
    }
}
