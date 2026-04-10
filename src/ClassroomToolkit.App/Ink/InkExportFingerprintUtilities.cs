using System.Security.Cryptography;
using System.Text;

namespace ClassroomToolkit.App.Ink;

internal static class InkExportFingerprintUtilities
{
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

