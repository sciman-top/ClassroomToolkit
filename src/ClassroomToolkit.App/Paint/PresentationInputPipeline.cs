using ClassroomToolkit.Services.Presentation;
using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App.Paint;

internal sealed class PresentationInputPipeline
{
    private readonly PresentationControlService _presentationService;

    public PresentationInputPipeline(
        PresentationControlService presentationService,
        InputStrategy wpsStrategy = InputStrategy.Auto,
        InputStrategy officeStrategy = InputStrategy.Auto)
    {
        _presentationService = presentationService ?? throw new ArgumentNullException(nameof(presentationService));
        WpsStrategy = wpsStrategy;
        OfficeStrategy = officeStrategy;
    }

    public InputStrategy WpsStrategy { get; private set; }
    public InputStrategy OfficeStrategy { get; private set; }
    public bool WpsForceMessageFallback { get; private set; }

    public void UpdateWpsMode(string mode)
    {
        WpsStrategy = ResolveInputStrategyMode(mode);
        _presentationService.ResetWpsAutoFallback();
        _presentationService.ResetOfficeAutoFallback();
        WpsForceMessageFallback = false;
    }

    public void UpdateOfficeMode(string mode)
    {
        OfficeStrategy = ResolveInputStrategyMode(mode);
        _presentationService.ResetOfficeAutoFallback();
    }

    public void ResetAutoFallbacks()
    {
        _presentationService.ResetWpsAutoFallback();
        _presentationService.ResetOfficeAutoFallback();
    }

    public void ResetOfficeAutoFallback()
    {
        _presentationService.ResetOfficeAutoFallback();
    }

    public void ResetWpsHookFallback()
    {
        WpsForceMessageFallback = false;
    }

    public void MarkWpsHookUnavailable()
    {
        WpsForceMessageFallback = true;
    }

    public InputStrategy ResolveWpsSendMode(bool targetIsValid, IntPtr targetHandle = default)
    {
        if (WpsForceMessageFallback)
        {
            return InputStrategy.Message;
        }

        if (WpsStrategy == InputStrategy.Auto)
        {
            var forceMessage = targetHandle == IntPtr.Zero
                ? _presentationService.IsWpsAutoForcedMessage
                : _presentationService.IsWpsAutoForcedMessageForTarget(targetHandle);
            if (forceMessage)
            {
                return InputStrategy.Message;
            }

            return targetIsValid
                ? InputStrategy.Raw
                : InputStrategy.Message;
        }

        return WpsStrategy;
    }

    public PresentationControlOptions BuildWpsOptions(PresentationControlOptions currentOptions, string? source = null)
    {
        if (currentOptions == null)
        {
            return new PresentationControlOptions
            {
                Strategy = InputStrategy.Message,
                AllowOffice = false,
                AllowWps = true
            };
        }

        var strategy = ResolveWpsOptionStrategy(currentOptions, source);
        return new PresentationControlOptions
        {
            Strategy = strategy,
            WheelAsKey = currentOptions.WheelAsKey,
            WpsDebounceMs = currentOptions.WpsDebounceMs,
            LockStrategyWhenDegraded = currentOptions.LockStrategyWhenDegraded,
            AutoFallbackFailureThreshold = currentOptions.AutoFallbackFailureThreshold,
            AutoFallbackProbeIntervalCommands = currentOptions.AutoFallbackProbeIntervalCommands,
            AllowOffice = false,
            AllowWps = true
        };
    }

    public PresentationControlOptions BuildOfficeOptions(PresentationControlOptions currentOptions)
    {
        if (currentOptions == null)
        {
            return new PresentationControlOptions
            {
                Strategy = OfficeStrategy,
                AllowOffice = true,
                AllowWps = false
            };
        }

        return new PresentationControlOptions
        {
            Strategy = OfficeStrategy,
            WheelAsKey = currentOptions.WheelAsKey,
            WpsDebounceMs = currentOptions.WpsDebounceMs,
            LockStrategyWhenDegraded = currentOptions.LockStrategyWhenDegraded,
            AutoFallbackFailureThreshold = currentOptions.AutoFallbackFailureThreshold,
            AutoFallbackProbeIntervalCommands = currentOptions.AutoFallbackProbeIntervalCommands,
            AllowOffice = true,
            AllowWps = false
        };
    }

    public static InputStrategy ResolveInputStrategyMode(string mode)
    {
        return mode switch
        {
            WpsInputModeDefaults.Raw => InputStrategy.Raw,
            WpsInputModeDefaults.Message => InputStrategy.Message,
            _ => InputStrategy.Auto
        };
    }

    private InputStrategy ResolveWpsOptionStrategy(PresentationControlOptions currentOptions, string? source)
    {
        var strategy = WpsStrategy;
        if (WpsForceMessageFallback)
        {
            strategy = InputStrategy.Message;
        }
        if (string.Equals(source, "wheel", StringComparison.OrdinalIgnoreCase) && currentOptions.WheelAsKey)
        {
            strategy = InputStrategy.Message;
        }

        return strategy;
    }
}
