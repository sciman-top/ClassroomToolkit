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
        AppendHashToken(hash, stroke.WetnessStart.HasValue
            ? stroke.WetnessStart.Value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)
            : null);
        AppendHashToken(hash, stroke.WetnessEnd.HasValue
            ? stroke.WetnessEnd.Value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)
            : null);
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

        // 全页哈希在每次抬笔/变更收尾都会重算：这里若走 GetBytes 会给每条旧笔迹的
        // 每个 token 重新分配字节数组（几何路径可达数十 KB）。固定栈缓冲 + 池化
        // 大缓冲后输出字节流与 GetBytes 完全一致，哈希值不变。
        var byteCount = Encoding.UTF8.GetByteCount(value);
        if (byteCount <= StackBufferSize)
        {
            Span<byte> buffer = stackalloc byte[StackBufferSize];
            var written = Encoding.UTF8.GetBytes(value, buffer);
            hash.AppendData(buffer[..written]);
            return;
        }

        var rented = System.Buffers.ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            var written = Encoding.UTF8.GetBytes(value, rented);
            hash.AppendData(rented.AsSpan(0, written));
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private const int StackBufferSize = 512;
}
