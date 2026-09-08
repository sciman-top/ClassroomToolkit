using System.Security.Cryptography;
using System.Text;

namespace ClassroomToolkit.App.Ink;

internal static class InkExportFingerprintUtilities
{
    internal static void AppendStrokePayload(IncrementalHash hash, InkStrokeData stroke)
    {
        AppendHashToken(hash, stroke.Type.ToString());
        AppendHashToken(hash, stroke.BrushStyle.ToString());
        AppendHashToken(hash, stroke.ColorHex);
        AppendHashToken(hash, stroke.Opacity.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendHashToken(hash, stroke.BrushSize.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
        AppendHashToken(hash, stroke.ReferenceWidth.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
        AppendHashToken(hash, stroke.ReferenceHeight.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
        AppendHashToken(hash, stroke.CalligraphyRenderMode.ToString());
        AppendHashToken(hash, (stroke.CalligraphyInkBloomEnabled ? 1 : 0).ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendHashToken(hash, (stroke.CalligraphySealEnabled ? 1 : 0).ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendHashToken(hash, stroke.CalligraphyOverlayOpacityThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendHashToken(hash, stroke.MaskSeed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendHashToken(hash, stroke.InkFlow.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
        AppendHashToken(hash, stroke.StrokeDirectionX.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
        AppendHashToken(hash, stroke.StrokeDirectionY.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
        AppendHashField(hash, stroke.GeometryPath);

        foreach (var ribbon in stroke.Ribbons)
        {
            AppendHashUtf8(hash, "r:");
            AppendHashToken(hash, ribbon.RibbonT.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
            AppendHashToken(hash, ribbon.Opacity.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
            AppendHashField(hash, ribbon.GeometryPath);
        }

        foreach (var bloom in stroke.Blooms)
        {
            AppendHashUtf8(hash, "b:");
            AppendHashToken(hash, bloom.Opacity.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
            AppendHashField(hash, bloom.GeometryPath);
        }

        AppendHashUtf8(hash, ";");
    }

    internal static void AppendHashField(IncrementalHash hash, string? value)
    {
        AppendHashUtf8(hash, value ?? string.Empty);
        AppendHashUtf8(hash, "|");
    }

    internal static void AppendHashToken(IncrementalHash hash, string? value)
    {
        AppendHashUtf8(hash, value ?? string.Empty);
        AppendHashUtf8(hash, ",");
    }

    internal static void AppendHashUtf8(IncrementalHash hash, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        hash.AppendData(Encoding.UTF8.GetBytes(value));
    }
}
