using System.Diagnostics;
using System.Windows;
using WpfApplication = System.Windows.Application;

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

        if (!TryReplaceColorDictionary(nextDictionary))
        {
            Debug.WriteLine($"[Theme] color dictionary not found; theme={normalized}");
            return false;
        }

        CurrentTheme = normalized;
        return true;
    }

    private bool TryReplaceColorDictionary(ResourceDictionary nextDictionary)
    {
        foreach (var dictionary in _application.Resources.MergedDictionaries)
        {
            if (TryReplaceNestedDictionary(dictionary, nextDictionary))
            {
                return true;
            }
        }

        if (_activeColorDictionary != null)
        {
            var index = _application.Resources.MergedDictionaries.IndexOf(_activeColorDictionary);
            if (index >= 0)
            {
                _application.Resources.MergedDictionaries[index] = nextDictionary;
                _activeColorDictionary = nextDictionary;
                return true;
            }
        }

        return false;
    }

    private bool TryReplaceNestedDictionary(
        ResourceDictionary owner,
        ResourceDictionary nextDictionary)
    {
        for (var index = 0; index < owner.MergedDictionaries.Count; index++)
        {
            var candidate = owner.MergedDictionaries[index];
            if (IsThemeDictionary(candidate))
            {
                owner.MergedDictionaries[index] = nextDictionary;
                _activeColorDictionary = nextDictionary;
                return true;
            }

            if (TryReplaceNestedDictionary(candidate, nextDictionary))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsThemeDictionary(ResourceDictionary dictionary)
    {
        return dictionary.Source?.OriginalString.Contains("Colors.", StringComparison.OrdinalIgnoreCase) == true;
    }
}
