using System.Collections;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WpfApplication = System.Windows.Application;
using WpfColor = System.Windows.Media.Color;

namespace ClassroomToolkit.App.UI.Themes;

public sealed class ThemeManager
{
    private static readonly string ThemeResourceName =
        $"{Uri.EscapeDataString(typeof(ThemeManager).Assembly.GetName().Name ?? "ClassroomToolkit.App")};component/UI/Themes/Colors.";
    private readonly WpfApplication _application;
    private ResourceDictionary? _activeColorDictionary;

    public ThemeManager(WpfApplication application)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        CurrentTheme = AppTheme.MidnightTeal;
    }

    public AppTheme CurrentTheme { get; private set; }

    public bool Apply(AppTheme theme)
    {
        var normalized = Enum.IsDefined(theme) ? theme : ThemePreferenceService.DefaultTheme;
        var nextDictionary = new ResourceDictionary
        {
            Source = new Uri(
                $"/{ThemeResourceName}{normalized}.xaml",
                UriKind.Relative)
        };

        if (!TryReplaceColorDictionary(nextDictionary, out var previousDictionary))
        {
            Debug.WriteLine($"[Theme] color dictionary not found; theme={normalized}");
            return false;
        }

        RefreshExistingThemeResources(previousDictionary, nextDictionary);
        CurrentTheme = normalized;
        return true;
    }

    private bool TryReplaceColorDictionary(ResourceDictionary nextDictionary, out ResourceDictionary previousDictionary)
    {
        foreach (var dictionary in _application.Resources.MergedDictionaries)
        {
            if (TryReplaceNestedDictionary(dictionary, nextDictionary, out previousDictionary))
            {
                return true;
            }
        }

        if (_activeColorDictionary != null)
        {
            var index = _application.Resources.MergedDictionaries.IndexOf(_activeColorDictionary);
            if (index >= 0)
            {
                previousDictionary = _activeColorDictionary;
                _application.Resources.MergedDictionaries[index] = nextDictionary;
                _activeColorDictionary = nextDictionary;
                return true;
            }
        }

        previousDictionary = null!;
        return false;
    }

    private bool TryReplaceNestedDictionary(
        ResourceDictionary owner,
        ResourceDictionary nextDictionary,
        out ResourceDictionary previousDictionary)
    {
        for (var index = 0; index < owner.MergedDictionaries.Count; index++)
        {
            var candidate = owner.MergedDictionaries[index];
            if (IsThemeDictionary(candidate))
            {
                previousDictionary = candidate;
                owner.MergedDictionaries[index] = nextDictionary;
                _activeColorDictionary = nextDictionary;
                return true;
            }

            if (TryReplaceNestedDictionary(candidate, nextDictionary, out previousDictionary))
            {
                return true;
            }
        }

        previousDictionary = null!;
        return false;
    }

    private void RefreshExistingThemeResources(
        ResourceDictionary previousColors,
        ResourceDictionary nextColors)
    {
        var replacements = BuildColorReplacements(previousColors, nextColors);
        if (replacements.Count == 0)
        {
            return;
        }

        var visited = new HashSet<ResourceDictionary>();
        RefreshDictionary(_application.Resources, replacements, visited);
    }

    private static Dictionary<WpfColor, WpfColor> BuildColorReplacements(
        ResourceDictionary previousColors,
        ResourceDictionary nextColors)
    {
        var replacements = new Dictionary<WpfColor, WpfColor>();
        foreach (DictionaryEntry entry in previousColors)
        {
            if (entry.Key is not string key || entry.Value is not WpfColor previous || nextColors[key] is not WpfColor next)
            {
                continue;
            }

            replacements[previous] = next;
        }

        return replacements;
    }

    private static void RefreshDictionary(
        ResourceDictionary dictionary,
        IReadOnlyDictionary<WpfColor, WpfColor> replacements,
        ISet<ResourceDictionary> visited)
    {
        if (!visited.Add(dictionary))
        {
            return;
        }

        if (IsDynamicThemeResourceDictionary(dictionary))
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                switch (entry.Value)
                {
                    case SolidColorBrush brush when !brush.IsFrozen:
                        RefreshBrushColor(brush, replacements);
                        break;
                    case GradientBrush gradient when !gradient.IsFrozen:
                        foreach (var stop in gradient.GradientStops)
                        {
                            RefreshGradientStopColor(stop, replacements);
                        }
                        break;
                    case DropShadowEffect shadow when !shadow.IsFrozen:
                        RefreshShadowColor(shadow, replacements);
                        break;
                }
            }
        }

        foreach (var child in dictionary.MergedDictionaries)
        {
            RefreshDictionary(child, replacements, visited);
        }
    }

    private static void RefreshBrushColor(SolidColorBrush brush, IReadOnlyDictionary<WpfColor, WpfColor> replacements)
    {
        if (replacements.TryGetValue(brush.Color, out var replacement))
        {
            brush.Color = replacement;
        }
    }

    private static void RefreshGradientStopColor(GradientStop stop, IReadOnlyDictionary<WpfColor, WpfColor> replacements)
    {
        if (replacements.TryGetValue(stop.Color, out var replacement))
        {
            stop.Color = replacement;
        }
    }

    private static void RefreshShadowColor(DropShadowEffect shadow, IReadOnlyDictionary<WpfColor, WpfColor> replacements)
    {
        if (replacements.TryGetValue(shadow.Color, out var replacement))
        {
            shadow.Color = replacement;
        }
    }

    private static bool IsThemeDictionary(ResourceDictionary dictionary)
    {
        return dictionary.Source?.OriginalString.Contains("Colors.", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsDynamicThemeResourceDictionary(ResourceDictionary dictionary)
    {
        return dictionary.Keys.Cast<object>().Any(key =>
            string.Equals(key as string, "CTK.Brush.Canvas", StringComparison.Ordinal) ||
            string.Equals(key as string, "Brush_AppBackground", StringComparison.Ordinal));
    }
}
