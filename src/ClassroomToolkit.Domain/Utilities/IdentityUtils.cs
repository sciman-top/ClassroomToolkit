using System.Globalization;
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

    public static string BuildRowKey(string? studentId, string? name, string? className, string? groupName)
    {
        var parts = string.Join("|",
            CompactText(studentId),
            NormalizeText(name),
            NormalizeText(className),
            NormalizeGroupName(groupName));
        if (string.IsNullOrWhiteSpace(parts.Replace("|", string.Empty, StringComparison.Ordinal)))
        {
            return string.Empty;
        }
        using var sha1 = SHA1.Create();
        var bytes = Encoding.UTF8.GetBytes(parts);
        var hash = sha1.ComputeHash(bytes);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }
        return $"rk:{builder}";
    }
}
