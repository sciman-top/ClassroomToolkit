using System.Collections;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WpfApplication = System.Windows.Application;

namespace ClassroomToolkit.App.UI.Themes;

public sealed class ThemeManager
{
    private static readonly string ResourceAssemblyName =
        Uri.EscapeDataString(typeof(ThemeManager).Assembly.GetName().Name ?? "ClassroomToolkit.App");
    private static readonly string ThemeResourceName =
        $"{ResourceAssemblyName};component/UI/Themes/Colors.";
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
        var nextColors = new ResourceDictionary
        {
            Source = new Uri($"/{ThemeResourceName}{normalized}.xaml", UriKind.Relative)
        };

        if (!TryReplaceColorDictionary(nextColors))
        {
            Debug.WriteLine($"[Theme] color dictionary not found; theme={normalized}");
            return false;
        }

        RefreshDynamicThemeDictionaries(_application.Resources, new HashSet<ResourceDictionary>());
        CurrentTheme = normalized;
        return true;
    }

    private bool TryReplaceColorDictionary(ResourceDictionary nextColors)
    {
        foreach (var dictionary in _application.Resources.MergedDictionaries)
        {
            if (TryReplaceNestedColorDictionary(dictionary, nextColors))
            {
                return true;
            }
        }

        if (_activeColorDictionary != null)
        {
            var index = _application.Resources.MergedDictionaries.IndexOf(_activeColorDictionary);
            if (index >= 0)
            {
                _application.Resources.MergedDictionaries[index] = nextColors;
                _activeColorDictionary = nextColors;
                return true;
            }
        }

        return false;
    }

    private bool TryReplaceNestedColorDictionary(ResourceDictionary owner, ResourceDictionary nextColors)
    {
        for (var index = 0; index < owner.MergedDictionaries.Count; index++)
        {
            var candidate = owner.MergedDictionaries[index];
            if (IsColorDictionary(candidate))
            {
                owner.MergedDictionaries[index] = nextColors;
                _activeColorDictionary = nextColors;
                return true;
            }

            if (TryReplaceNestedColorDictionary(candidate, nextColors))
            {
                return true;
            }
        }

        return false;
    }

    private static void RefreshDynamicThemeDictionaries(
        ResourceDictionary dictionary,
        ISet<ResourceDictionary> visited)
    {
        if (!visited.Add(dictionary))
        {
            return;
        }

        foreach (var child in dictionary.MergedDictionaries)
        {
            if (IsDynamicThemeResourceDictionary(child))
            {
                RefreshDynamicThemeResources(child);
                continue;
            }

            RefreshDynamicThemeDictionaries(child, visited);
        }
    }

    private static void RefreshDynamicThemeResources(ResourceDictionary dictionary)
    {
        var refreshed = CreateDynamicThemeResourceDictionary(dictionary);
        var entries = dictionary.Cast<DictionaryEntry>().ToArray();

        foreach (var entry in entries)
        {
            if (!refreshed.Contains(entry.Key))
            {
                continue;
            }

            ApplyThemeResource(dictionary, entry.Key, entry.Value, refreshed[entry.Key]);
        }
    }

    private static void ApplyThemeResource(
        ResourceDictionary owner,
        object key,
        object? current,
        object? refreshed)
    {
        switch (current)
        {
            case SolidColorBrush currentBrush when refreshed is SolidColorBrush refreshedBrush:
                if (currentBrush.IsFrozen)
                {
                    owner[key] = refreshedBrush;
                }
                else
                {
                    currentBrush.Color = refreshedBrush.Color;
                }
                break;
            case GradientBrush currentGradient when refreshed is GradientBrush refreshedGradient:
                if (currentGradient.IsFrozen || currentGradient.GradientStops.Count != refreshedGradient.GradientStops.Count)
                {
                    owner[key] = refreshedGradient;
                }
                else
                {
                    for (var index = 0; index < currentGradient.GradientStops.Count; index++)
                    {
                        currentGradient.GradientStops[index].Color = refreshedGradient.GradientStops[index].Color;
                    }
                }
                break;
            case DropShadowEffect currentShadow when refreshed is DropShadowEffect refreshedShadow:
                if (currentShadow.IsFrozen)
                {
                    owner[key] = refreshedShadow;
                }
                else
                {
                    currentShadow.Color = refreshedShadow.Color;
                }
                break;
        }
    }

    private static bool IsColorDictionary(ResourceDictionary dictionary)
    {
        return dictionary.Source?.OriginalString.Contains("Colors.", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsDynamicThemeResourceDictionary(ResourceDictionary dictionary)
    {
        return dictionary.Keys.Cast<object>().Any(key =>
            string.Equals(key as string, "CTK.Brush.Canvas", StringComparison.Ordinal) ||
            string.Equals(key as string, "Brush_AppBackground", StringComparison.Ordinal));
    }

    private static ResourceDictionary CreateDynamicThemeResourceDictionary(ResourceDictionary dictionary)
    {
        var resourcePath = dictionary.Contains("CTK.Brush.Canvas")
            ? "UI/Themes/SemanticBrushes.xaml"
            : "Assets/Styles/LegacyAliases.xaml";
        return new ResourceDictionary
        {
            Source = new Uri($"/{ResourceAssemblyName};component/{resourcePath}", UriKind.Relative)
        };
    }
}
