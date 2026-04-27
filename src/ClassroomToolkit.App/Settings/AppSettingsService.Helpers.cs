using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint;

namespace ClassroomToolkit.App.Settings;

public sealed partial class AppSettingsService
{
    private static Dictionary<string, string> GetOrCreate(Dictionary<string, Dictionary<string, string>> data, string key)
    {
        if (!data.TryGetValue(key, out var section))
        {
            section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            data[key] = section;
        }
        return section;
    }

    private static bool TryGetRollCallSection(
        Dictionary<string, Dictionary<string, string>> data,
        [NotNullWhen(true)] out Dictionary<string, string>? roll)
    {
        if (data.TryGetValue("RollCallTimer", out roll))
        {
            return true;
        }

        // Backward compatibility for legacy settings section name.
        if (data.TryGetValue("RollCall", out roll))
        {
            return true;
        }

        roll = null!;
        return false;
    }

    private static string GetString(Dictionary<string, string> section, string key, string fallback)
    {
        return section.TryGetValue(key, out var value) ? value : fallback;
    }

    private static double GetDouble(Dictionary<string, string> section, string key, double fallback)
    {
        if (!section.TryGetValue(key, out var value))
        {
            return fallback;
        }
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : fallback;
    }

    private static byte GetByte(Dictionary<string, string> section, string key, byte fallback)
    {
        if (!section.TryGetValue(key, out var value))
        {
            return fallback;
        }
        return byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : fallback;
    }

    private static int GetInt(Dictionary<string, string> section, string key, int fallback)
    {
        if (!section.TryGetValue(key, out var value))
        {
            return fallback;
        }
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : fallback;
    }

    private static bool GetBool(Dictionary<string, string> section, string key, bool fallback)
    {
        if (!section.TryGetValue(key, out var value))
        {
            return fallback;
        }
        var normalized = value.Trim();
        if (normalized.Equals("true", StringComparison.OrdinalIgnoreCase) || normalized.Equals("1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (normalized.Equals("yes", StringComparison.OrdinalIgnoreCase) || normalized.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (normalized.Equals("false", StringComparison.OrdinalIgnoreCase) || normalized.Equals("0", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (normalized.Equals("no", StringComparison.OrdinalIgnoreCase) || normalized.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return fallback;
    }

    private static string ResolveSpeechEngine(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.IsNullOrWhiteSpace(fallback) ? "sapi" : fallback;
        }

        var normalized = value.Trim();
        if (normalized.Equals("pyttsx3", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("sapi", StringComparison.OrdinalIgnoreCase))
        {
            return "sapi";
        }

        return string.IsNullOrWhiteSpace(fallback) ? "sapi" : fallback;
    }

    private static PaintShapeType GetShapeType(string raw)
    {
        if (Enum.TryParse<PaintShapeType>(raw, true, out var parsed))
        {
            return parsed;
        }
        return PaintShapeType.None;
    }

    private static PaintBrushStyle GetBrushStyle(string raw)
    {
        if (Enum.TryParse<PaintBrushStyle>(raw, true, out var parsed))
        {
            if (parsed == PaintBrushStyle.Standard)
            {
                return PaintBrushStyle.StandardRibbon;
            }
            return parsed;
        }
        return PaintBrushStyle.StandardRibbon;
    }

    private static InkExportScope GetInkExportScope(string raw)
    {
        if (Enum.TryParse<InkExportScope>(raw, true, out var parsed))
        {
            return parsed;
        }
        return InkExportScope.AllPersistedAndSession;
    }

    private static WhiteboardBrushPreset ResolveWhiteboardPreset(Dictionary<string, string> section, WhiteboardBrushPreset fallback)
    {
        if (section.TryGetValue("whiteboard_preset", out var rawPreset))
        {
            if (Enum.TryParse<WhiteboardBrushPreset>(rawPreset, true, out var parsed))
            {
                return parsed;
            }
            if (rawPreset.Trim().Equals("Compatibility", StringComparison.OrdinalIgnoreCase))
            {
                return WhiteboardBrushPreset.Sharp;
            }
        }

        if (section.ContainsKey("whiteboard_smooth_mode"))
        {
            bool legacy = GetBool(section, "whiteboard_smooth_mode", true);
            return legacy ? WhiteboardBrushPreset.Smooth : WhiteboardBrushPreset.Sharp;
        }

        return fallback;
    }

    private static CalligraphyBrushPreset ResolveCalligraphyPreset(Dictionary<string, string> section, CalligraphyBrushPreset fallback)
    {
        if (section.TryGetValue("calligraphy_preset", out var rawPreset) &&
            Enum.TryParse<CalligraphyBrushPreset>(rawPreset, true, out var parsed))
        {
            return parsed;
        }

        if (section.ContainsKey("calligraphy_sharp_mode"))
        {
            bool legacy = GetBool(section, "calligraphy_sharp_mode", true);
            return legacy ? CalligraphyBrushPreset.Sharp : CalligraphyBrushPreset.Soft;
        }

        return fallback;
    }

    private static ClassroomWritingMode ResolveClassroomWritingMode(Dictionary<string, string> section, ClassroomWritingMode fallback)
    {
        if (section.TryGetValue("classroom_writing_mode", out var raw) &&
            Enum.TryParse<ClassroomWritingMode>(raw, true, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static string ResolveWpsInputMode(Dictionary<string, string> section, string fallback)
    {
        var mode = GetString(section, "wps_input_mode", fallback);
        var normalizedFallback = NormalizeInputMode(fallback, WpsInputModeDefaults.Auto);
        if (mode.Trim().Equals("manual", StringComparison.OrdinalIgnoreCase))
        {
            var rawInput = GetBool(section, "wps_raw_input", fallback: false);
            return rawInput ? WpsInputModeDefaults.Raw : WpsInputModeDefaults.Message;
        }

        return NormalizeInputMode(mode, normalizedFallback);
    }

    private static string ResolveOfficeInputMode(
        Dictionary<string, string> section,
        string fallback,
        string wpsResolvedMode)
    {
        var mode = GetString(section, "office_input_mode", string.Empty);
        if (!string.IsNullOrWhiteSpace(mode))
        {
            return NormalizeInputMode(mode, fallback);
        }

        // Backward compatibility:
        // For users upgraded from single-mode configuration, inherit WPS mode,
        // but avoid keeping Office on message mode by default.
        var normalizedLegacy = NormalizeInputMode(wpsResolvedMode, fallback);
        if (string.Equals(normalizedLegacy, WpsInputModeDefaults.Message, StringComparison.OrdinalIgnoreCase))
        {
            return WpsInputModeDefaults.Auto;
        }

        return normalizedLegacy;
    }

    private static string NormalizeWpsInputMode(string? rawMode, string fallback)
    {
        return NormalizeInputMode(rawMode, fallback);
    }

    private static string NormalizeInputMode(string? rawMode, string fallback)
    {
        var normalizedFallback = NormalizeInputModeToken(
            string.IsNullOrWhiteSpace(fallback)
                ? WpsInputModeDefaults.Auto
                : fallback);
        if (string.IsNullOrWhiteSpace(normalizedFallback))
        {
            normalizedFallback = WpsInputModeDefaults.Auto;
        }

        var normalizedMode = NormalizeInputModeToken(rawMode);
        return string.IsNullOrWhiteSpace(normalizedMode)
            ? normalizedFallback
            : normalizedMode;
    }

    private static string NormalizeInputModeToken(string? rawMode)
    {
        var normalizedMode = (rawMode ?? string.Empty).Trim().ToUpperInvariant();
        return normalizedMode switch
        {
            "AUTO" => WpsInputModeDefaults.Auto,
            "RAW" => WpsInputModeDefaults.Raw,
            "MESSAGE" => WpsInputModeDefaults.Message,
            _ => string.Empty
        };
    }

    private static string NormalizePresetScheme(string? rawScheme)
    {
        var normalized = (rawScheme ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return PresetSchemeDefaults.Custom;
        }

        if (normalized == "DUAL_SCREEN")
        {
            return PresetSchemeDefaults.Stable;
        }

        return normalized switch
        {
            "CUSTOM" => PresetSchemeDefaults.Custom,
            "BALANCED" => PresetSchemeDefaults.Balanced,
            "RESPONSIVE" => PresetSchemeDefaults.Responsive,
            "STABLE" => PresetSchemeDefaults.Stable,
            _ => PresetSchemeDefaults.Custom
        };
    }

    private static int NormalizeStylusPressureProfile(int rawProfile)
    {
        return Enum.IsDefined(typeof(StylusPressureDeviceProfile), rawProfile)
            ? rawProfile
            : (int)StylusPressureDeviceProfile.Unknown;
    }

    private static int NormalizeStylusSampleRateTier(int rawTier)
    {
        return Enum.IsDefined(typeof(StylusSampleRateTier), rawTier)
            ? rawTier
            : (int)StylusSampleRateTier.Unknown;
    }

    private static int NormalizeStylusPredictionHorizonMs(int rawPredictionHorizonMs)
    {
        return Math.Clamp(
            rawPredictionHorizonMs,
            StylusAdaptiveProfilingDefaults.SeedPredictionHorizonMinMs,
            StylusAdaptiveProfilingDefaults.SeedPredictionHorizonMaxMs);
    }

    private static int NormalizeWpsDebounceMs(int debounceMs)
    {
        return Math.Max(0, debounceMs);
    }

    private static int NormalizePresentationAutoFallbackFailureThreshold(int threshold)
    {
        return Math.Clamp(
            threshold,
            min: 1,
            max: 10);
    }

    private static int NormalizePresentationAutoFallbackProbeIntervalCommands(int interval)
    {
        return Math.Clamp(
            interval,
            min: 1,
            max: 100);
    }

    private static double NormalizePaintToolbarScale(double scale)
    {
        return Math.Clamp(scale, ToolbarScaleDefaults.Min, ToolbarScaleDefaults.Max);
    }

    private static int NormalizeInkExportMaxParallelFiles(int maxParallelFiles)
    {
        return Math.Max(0, maxParallelFiles);
    }

    private static int NormalizeInkRetentionDays(int retentionDays)
    {
        return Math.Max(0, retentionDays);
    }

    private static string NormalizeInkPhotoRootPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return AppSettings.ResolveDefaultInkPhotoRootPath();
        }

        return path.Trim();
    }

    private static int NormalizePhotoNeighborPrefetchRadiusMax(int maxRadius)
    {
        return Math.Clamp(
            maxRadius,
            CrossPageNeighborPrefetchDefaults.RadiusMin,
            CrossPageNeighborPrefetchDefaults.RadiusMax);
    }

    private static int NormalizePhotoPostInputRefreshDelayMs(int delayMs)
    {
        return CrossPagePostInputRefreshDelayClampPolicy.Clamp(delayMs);
    }

    private static double NormalizePhotoWheelZoomBase(double wheelZoomBase)
    {
        return Math.Clamp(
            wheelZoomBase,
            PhotoZoomInputDefaults.WheelZoomBaseMin,
            PhotoZoomInputDefaults.WheelZoomBaseMax);
    }

    private static double NormalizePhotoGestureZoomSensitivity(double sensitivity)
    {
        return Math.Clamp(
            sensitivity,
            PhotoZoomInputDefaults.GestureSensitivityMin,
            PhotoZoomInputDefaults.GestureSensitivityMax);
    }

    private static string NormalizePhotoInertiaProfile(string? profile)
    {
        return PhotoInertiaProfileDefaults.Normalize(profile);
    }

    private static int NormalizeLauncherAutoExitSeconds(int autoExitSeconds)
    {
        return Math.Max(0, autoExitSeconds);
    }

    private static double ClampUnitInterval(double value)
    {
        return Math.Clamp(value, 0.0, 1.0);
    }

    private static (double Low, double High) NormalizeStylusCalibrationRange(double low, double high)
    {
        var normalizedLow = ClampUnitInterval(low);
        var normalizedHigh = ClampUnitInterval(high);
        if (normalizedHigh - normalizedLow >= StylusRuntimeDefaults.CalibratedRangeSeedMinWidth)
        {
            return (normalizedLow, normalizedHigh);
        }

        return (StylusRuntimeDefaults.CalibratedLowDefault, StylusRuntimeDefaults.CalibratedHighDefault);
    }

    private static bool HasGeometry(AppSettings settings)
    {
        return settings.RollCallWindowWidth > 0
               && settings.RollCallWindowHeight > 0
               && settings.RollCallWindowX != AppSettings.UnsetPosition
               && settings.RollCallWindowY != AppSettings.UnsetPosition;
    }

    private static bool TryParseGeometry(string value, out int width, out int height, out int x, out int y)
    {
        width = 0;
        height = 0;
        x = 0;
        y = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }
        var match = GeometryRegex.Match(value.Trim());
        if (!match.Success)
        {
            return false;
        }
        if (!int.TryParse(match.Groups["w"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out width))
        {
            return false;
        }
        if (!int.TryParse(match.Groups["h"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out height))
        {
            return false;
        }
        if (!int.TryParse(match.Groups["x"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out x))
        {
            return false;
        }
        if (!int.TryParse(match.Groups["y"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
        {
            return false;
        }
        return width > 0 && height > 0;
    }

    private static string FormatGeometry(int width, int height, int x, int y)
    {
        var safeWidth = Math.Max(1, width);
        var safeHeight = Math.Max(1, height);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{safeWidth}x{safeHeight}{x:+#;-#;0}{y:+#;-#;0}");
    }

    private static List<string> ParseList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new List<string>();
        }

        var segments = raw.Split('|', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>(segments.Length);
        foreach (var segment in segments)
        {
            var trimmed = segment.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                result.Add(trimmed);
            }
        }

        return result;
    }

    private static string JoinList(IReadOnlyList<string> items)
    {
        if (items == null || items.Count == 0)
        {
            return string.Empty;
        }

        var buffer = new List<string>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (!string.IsNullOrWhiteSpace(item))
            {
                buffer.Add(item);
            }
        }

        return buffer.Count == 0
            ? string.Empty
            : string.Join("|", buffer);
    }

    private static void SetBool(Dictionary<string, string> section, string key, bool value)
    {
        section[key] = value ? "True" : "False";
    }
}
