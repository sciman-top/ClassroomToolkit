using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using ClassroomToolkit.Application.Abstractions;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint;

namespace ClassroomToolkit.App.Settings;

public sealed partial class AppSettingsService
{
    private static readonly Regex GeometryRegex = new(
        @"^(?<w>\d+)x(?<h>\d+)(?<x>[+-]\d+)(?<y>[+-]\d+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ISettingsDocumentStore _store;

    public AppSettingsService(ISettingsDocumentStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public AppSettings Load()
    {
        var data = _store.Load()
            ?? new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var settings = new AppSettings();

        if (TryGetRollCallSection(data, out var roll))
        {
            ApplyRollCallSettings(roll, settings);
        }
        if (data.TryGetValue("Paint", out var paint))
        {
            ApplyPaintSettings(paint, settings);
        }
        if (data.TryGetValue("Launcher", out var launcher))
        {
            ApplyLauncherSettings(launcher, settings);
        }
        if (data.TryGetValue("Diagnostics", out var diagnostics))
        {
            ApplyDiagnosticsSettings(diagnostics, settings);
        }

        return settings;
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var data = _store.Load()
            ?? new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        SaveRollCallSettings(data, settings);

        SavePaintSettings(data, settings);

        SaveLauncherSettings(data, settings);
        SaveDiagnosticsSettings(data, settings);

        _store.Save(data);
    }
}
