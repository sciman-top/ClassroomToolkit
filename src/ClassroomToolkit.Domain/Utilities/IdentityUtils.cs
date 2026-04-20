using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace ClassroomToolkit.Domain.Utilities;

public static class IdentityUtils
{
    public const string AllGroupName = "全部";

    public static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }
        var trimmed = value.Trim();
        var lower = trimmed.ToLowerInvariant();
        if (lower is "nan" or "none" or "nat")
        {
            return string.Empty;
        }
        return trimmed;
    }

    public static string CompactText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }
        var normalized = NormalizeText(value);
        if (string.IsNullOrEmpty(normalized))
        {
            return string.Empty;
        }
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (!char.IsWhiteSpace(ch))
            {
                builder.Append(ch);
            }
        }
        return builder.ToString();
    }

    public static string NormalizeGroupName(string? value)
    {
        var normalized = NormalizeText(value);
        return string.IsNullOrEmpty(normalized) ? string.Empty : normalized.ToUpper(CultureInfo.InvariantCulture);
    }

    [SuppressMessage(
        "Security",
        "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "SHA1 row-key hashing is a compatibility identifier, not a security boundary.")]
    public static string BuildRowKey(string? studentId, string? name, string? className, string? groupName)
    {
        var parts = string.Join("|",
            CompactText(studentId),
            NormalizeText(name),
            NormalizeText(className),
            NormalizeGroupName(groupName));
        if (!HasNonDelimiterContent(parts))
        {
            return string.Empty;
        }
        var bytes = Encoding.UTF8.GetBytes(parts);
        var hash = SHA1.HashData(bytes);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }
        return $"rk:{builder}";
    }

    private static bool HasNonDelimiterContent(string value)
    {
        foreach (var ch in value)
        {
            if (ch != '|')
            {
                return true;
            }
        }
        return false;
    }
}
