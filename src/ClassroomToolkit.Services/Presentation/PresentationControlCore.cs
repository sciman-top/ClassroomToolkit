using ClassroomToolkit.Domain;
using ClassroomToolkit.Interop.Presentation;
using System.Diagnostics.CodeAnalysis;

namespace ClassroomToolkit.Services.Presentation;

public sealed record PresentationControlPlan(
    PresentationType TargetType,
    InputStrategy Strategy,
    bool UseWheelAsKey);

public enum PresentationCommand
{
    Next,
    Previous,
    First,
    Last,
    BlackScreenToggle,
    WhiteScreenToggle
}

internal static class PresentationExceptionFilterPolicy
{
    internal static bool IsNonFatal(Exception ex) => DomainExceptionFilterPolicy.IsNonFatal(ex);
}

internal static class PresentationControlDiagnosticsPolicy
{
    internal static string FormatSendFailureMessage(
        string operation,
        PresentationCommand command,
        IntPtr target,
        string exceptionType,
        string message)
    {
        return $"[PresentationControl] send-failed op={operation} cmd={command} target=0x{target.ToInt64():X} ex={exceptionType} msg={message}";
    }
}

public sealed class PresentationControlOptions
{
    public const int AutoFallbackFailureThresholdDefault = 2;
    public const int AutoFallbackProbeIntervalCommandsDefault = 8;

    public InputStrategy Strategy { get; set; } = InputStrategy.Auto;
    public bool WheelAsKey { get; set; }
    // 后台中继必须走窗口消息；SendInput 只能进入当前系统输入流，
    // 而且不能保证事件送达后台的放映窗口。
    public bool AllowBackground { get; set; }
    public bool AllowWps { get; set; } = true;
    public bool AllowOffice { get; set; } = true;
    public int WpsDebounceMs { get; set; } = 200;
    public bool LockStrategyWhenDegraded { get; set; } = true;
    public int AutoFallbackFailureThreshold { get; set; } = AutoFallbackFailureThresholdDefault;
    public int AutoFallbackProbeIntervalCommands { get; set; } = AutoFallbackProbeIntervalCommandsDefault;
}

public sealed class PresentationCommandMapper
{
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Kept as instance API for DI/testing compatibility across current call sites.")]
    public KeyBinding Map(PresentationType _, PresentationCommand command)
    {
        return command switch
        {
            PresentationCommand.Next => new KeyBinding(VirtualKey.PageDown, KeyModifiers.None),
            PresentationCommand.Previous => new KeyBinding(VirtualKey.PageUp, KeyModifiers.None),
            PresentationCommand.First => new KeyBinding(VirtualKey.Home, KeyModifiers.None),
            PresentationCommand.Last => new KeyBinding(VirtualKey.End, KeyModifiers.None),
            PresentationCommand.BlackScreenToggle => new KeyBinding(VirtualKey.B, KeyModifiers.None),
            PresentationCommand.WhiteScreenToggle => new KeyBinding(VirtualKey.W, KeyModifiers.None),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported presentation command.")
        };
    }
}

public sealed class PresentationControlPlanner
{
    public PresentationControlPlanner(PresentationClassifier classifier)
    {
        ArgumentNullException.ThrowIfNull(classifier);
        Classifier = classifier;
    }

    public PresentationClassifier Classifier { get; private set; }

    public void UpdateClassifier(PresentationClassifier classifier)
    {
        ArgumentNullException.ThrowIfNull(classifier);
        Classifier = classifier;
    }

    public PresentationControlPlan? Plan(
        PresentationWindowInfo info,
        PresentationControlOptions options,
        PresentationCommand command)
    {
        ArgumentNullException.ThrowIfNull(options);

        var type = Classifier.Classify(info);
        if (type == PresentationType.Wps && !options.AllowWps)
        {
            return null;
        }
        if (type == PresentationType.Office && !options.AllowOffice)
        {
            return null;
        }
        if (type is PresentationType.None or PresentationType.Other)
        {
            return null;
        }

        var strategy = ResolveStrategy(options.Strategy);
        var useWheel = options.WheelAsKey && type == PresentationType.Wps && IsNavigationCommand(command);
        return new PresentationControlPlan(type, strategy, useWheel);
    }

    private static InputStrategy ResolveStrategy(InputStrategy requested)
    {
        if (requested != InputStrategy.Auto)
        {
            return requested;
        }
        return InputStrategy.Raw;
    }

    private static bool IsNavigationCommand(PresentationCommand command)
    {
        return command is PresentationCommand.Next or PresentationCommand.Previous;
    }
}
