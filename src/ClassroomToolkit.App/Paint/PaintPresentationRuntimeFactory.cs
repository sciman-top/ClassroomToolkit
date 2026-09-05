using System;
using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.Services.Presentation;

namespace ClassroomToolkit.App.Paint;

/// <summary>
/// Assembles the presentation runtime owned by a paint overlay.
/// The window keeps the behavior and lifecycle policy; this module keeps the
/// concrete adapter graph and its defaults in one replaceable construction seam.
/// </summary>
internal static class PaintPresentationRuntimeFactory
{
    internal static PaintPresentationRuntime Create(
        PresentationClassifier classifier,
        Func<PresentationClassifier> classifierAccessor,
        Func<IntPtr, bool> isFullscreenWindow,
        uint currentProcessId)
    {
        ArgumentNullException.ThrowIfNull(classifier);
        ArgumentNullException.ThrowIfNull(classifierAccessor);
        ArgumentNullException.ThrowIfNull(isFullscreenWindow);

        var planner = new PresentationControlPlanner(classifier);
        var mapper = new PresentationCommandMapper();
        var inputSender = new Win32InputSender();
        var resolver = new Win32PresentationResolver();
        var service = new PresentationControlService(
            planner,
            mapper,
            inputSender,
            resolver,
            new Win32PresentationWindowValidator());
        var options = CreateDefaultOptions();
        var inputPipeline = new PresentationInputPipeline(
            service,
            options.Strategy,
            InputStrategy.Auto);
        var targetSnapshotProvider = new OverlayPresentationTargetSnapshotProvider(
            resolver,
            classifierAccessor,
            isFullscreenWindow,
            currentProcessId);
        var dispatchCoordinator = new OverlayPresentationDispatchCoordinator(targetSnapshotProvider);
        var wpsNavHook = new WpsSlideshowNavigationHook();

        return new PaintPresentationRuntime(
            classifier,
            resolver,
            service,
            options,
            inputPipeline,
            targetSnapshotProvider,
            dispatchCoordinator,
            wpsNavHook,
            new WpsNavHookClient(wpsNavHook));
    }

    private static PresentationControlOptions CreateDefaultOptions()
    {
        return new PresentationControlOptions
        {
            Strategy = InputStrategy.Auto,
            WheelAsKey = false,
            WpsDebounceMs = PresentationRuntimeDefaults.WpsNavDebounceMs,
            LockStrategyWhenDegraded = true,
            AutoFallbackFailureThreshold = PresentationControlOptions.AutoFallbackFailureThresholdDefault,
            AutoFallbackProbeIntervalCommands = PresentationControlOptions.AutoFallbackProbeIntervalCommandsDefault,
            AllowOffice = true,
            AllowWps = true
        };
    }
}

/// <summary>
/// The concrete presentation components that a paint overlay needs.
/// This is intentionally an internal construction result, not a second public
/// presentation API; behavior remains in the existing service and policy types.
/// </summary>
internal sealed class PaintPresentationRuntime
{
    internal PaintPresentationRuntime(
        PresentationClassifier classifier,
        Win32PresentationResolver resolver,
        PresentationControlService service,
        PresentationControlOptions options,
        PresentationInputPipeline inputPipeline,
        IOverlayPresentationTargetSnapshotProvider targetSnapshotProvider,
        OverlayPresentationDispatchCoordinator dispatchCoordinator,
        WpsSlideshowNavigationHook wpsNavHook,
        IWpsNavHookClient wpsNavHookClient)
    {
        Classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
        Resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        Service = service ?? throw new ArgumentNullException(nameof(service));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        InputPipeline = inputPipeline ?? throw new ArgumentNullException(nameof(inputPipeline));
        TargetSnapshotProvider = targetSnapshotProvider ?? throw new ArgumentNullException(nameof(targetSnapshotProvider));
        DispatchCoordinator = dispatchCoordinator ?? throw new ArgumentNullException(nameof(dispatchCoordinator));
        WpsNavHook = wpsNavHook ?? throw new ArgumentNullException(nameof(wpsNavHook));
        WpsNavHookClient = wpsNavHookClient ?? throw new ArgumentNullException(nameof(wpsNavHookClient));
    }

    internal PresentationClassifier Classifier { get; }
    internal Win32PresentationResolver Resolver { get; }
    internal PresentationControlService Service { get; }
    internal PresentationControlOptions Options { get; }
    internal PresentationInputPipeline InputPipeline { get; }
    internal IOverlayPresentationTargetSnapshotProvider TargetSnapshotProvider { get; }
    internal OverlayPresentationDispatchCoordinator DispatchCoordinator { get; }
    internal WpsSlideshowNavigationHook WpsNavHook { get; }
    internal IWpsNavHookClient WpsNavHookClient { get; }
}
