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
    private bool _overwriteBlockedAfterLoadFailure;

    public bool IsOverwriteBlocked => _overwriteBlockedAfterLoadFailure || _store.IsOverwriteBlocked;

    public AppSettingsService(ISettingsDocumentStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public AppSettings Load()
    {
        var data = _store.Load()
            ?? new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        _overwriteBlockedAfterLoadFailure = _store.IsOverwriteBlocked;
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
        if (data.TryGetValue("UI", out var ui))
        {
            ApplyUiSettings(ui, settings);
        }
        if (data.TryGetValue("Update", out var update))
        {
            ApplyUpdateSettings(update, settings);
        }

        return settings;
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ThrowIfOverwriteBlocked();

        var data = _store.Load()
            ?? new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        ThrowIfOverwriteBlocked();
        SaveRollCallSettings(data, settings);

        SavePaintSettings(data, settings);

        SaveLauncherSettings(data, settings);
        SaveDiagnosticsSettings(data, settings);
        SaveUiSettings(data, settings);
        SaveUpdateSettings(data, settings);

        _store.Save(data);
    }

    private void ThrowIfOverwriteBlocked()
    {
        _overwriteBlockedAfterLoadFailure |= _store.IsOverwriteBlocked;
        if (_overwriteBlockedAfterLoadFailure)
        {
            throw new InvalidOperationException("设置文件读取失败，请成功重新加载设置后再保存，以避免覆盖原有配置。");
        }
    }
}
