using System.Security.Cryptography;
using AwesomeAssertions;
using ClassroomToolkit.App.Ink;
using Xunit;

namespace ClassroomToolkit.Tests.Ink;

/// <summary>
/// 契约锚定：InkPageHashTracker 的增量哈希必须与全量 ComputeInkHash 同序 payload
/// 逐位一致。任何变异形态（追加/删除/引用替换/整表替换/换页）命中 tracker 时只允许
/// 两条出路——增量仍一致，或退回全量重算；绝不允许产出与全量不同的哈希。
/// </summary>
public sealed class InkPageHashTrackerTests
{
    [Fact]
    public void ConsecutiveAppends_ShouldMatchFullHash_AtEveryStep()
    {
        var tracker = new InkPageHashTracker();
        var strokes = new List<InkStrokeData>();

        for (var i = 0; i < 12; i++)
        {
            strokes.Add(MakeStroke(i, withSubItems: i % 3 == 0));
            var incremental = tracker.ComputeStrokesHash("doc.pdf", 1, strokes);
            incremental.Should().Be(FullHash(strokes), $"append step {i} must match full recompute");
        }
    }

    [Fact]
    public void MiddleStrokePathReplacement_ShouldStillMatchFullHash()
    {
        var tracker = new InkPageHashTracker();
        var strokes = new List<InkStrokeData> { MakeStroke(0), MakeStroke(1), MakeStroke(2) };
        tracker.ComputeStrokesHash("doc.pdf", 1, strokes).Should().Be(FullHash(strokes));

        // 橡皮形态：中间笔 GeometryPath 被替换为新字符串（引用变化、count 不变）。
        strokes[1] = CloneWithNewPath(strokes[1], "M 9 9 L 8 8");
        tracker.ComputeStrokesHash("doc.pdf", 1, strokes).Should().Be(FullHash(strokes));

        // 替换后继续追加，增量路径必须基于重建后的状态保持一致。
        strokes.Add(MakeStroke(3));
        tracker.ComputeStrokesHash("doc.pdf", 1, strokes).Should().Be(FullHash(strokes));
    }

    [Fact]
    public void BloomOnlyMutation_ShouldStillMatchFullHash()
    {
        var tracker = new InkPageHashTracker();
        var strokes = new List<InkStrokeData> { MakeStroke(0, withSubItems: true), MakeStroke(1) };
        tracker.ComputeStrokesHash("doc.pdf", 1, strokes).Should().Be(FullHash(strokes));

        // 橡皮只擦到 bloom（主体路径与笔画引用均不变，仅 bloom.GeometryPath 换引用）。
        strokes[0].Blooms[0].GeometryPath = "M 1 1 L 2 2";
        tracker.ComputeStrokesHash("doc.pdf", 1, strokes).Should().Be(FullHash(strokes));

        // bloom 被整项移除。
        strokes[0].Blooms.RemoveAt(0);
        tracker.ComputeStrokesHash("doc.pdf", 1, strokes).Should().Be(FullHash(strokes));
    }

    [Fact]
    public void RemovalAndListReplacement_ShouldMatchFullHash()
    {
        var tracker = new InkPageHashTracker();
        var strokes = new List<InkStrokeData>();
        for (var i = 0; i < 6; i++)
        {
            strokes.Add(MakeStroke(i));
        }

        tracker.ComputeStrokesHash("doc.pdf", 1, strokes).Should().Be(FullHash(strokes));

        strokes.RemoveAt(2);
        tracker.ComputeStrokesHash("doc.pdf", 1, strokes).Should().Be(FullHash(strokes));

        strokes.Clear();
        tracker.ComputeStrokesHash("doc.pdf", 1, strokes).Should().Be("empty");

        strokes.Add(MakeStroke(9));
        tracker.ComputeStrokesHash("doc.pdf", 1, strokes).Should().Be(FullHash(strokes));
    }

    [Fact]
    public void PageOrDocumentSwitch_ShouldRebuildAndStayConsistent()
    {
        var tracker = new InkPageHashTracker();
        var page1 = new List<InkStrokeData> { MakeStroke(0), MakeStroke(1) };
        tracker.ComputeStrokesHash("doc.pdf", 1, page1).Should().Be(FullHash(page1));

        var page2 = new List<InkStrokeData> { MakeStroke(7) };
        tracker.ComputeStrokesHash("doc.pdf", 2, page2).Should().Be(FullHash(page2));

        var otherDoc = new List<InkStrokeData> { MakeStroke(8) };
        tracker.ComputeStrokesHash("other.pdf", 2, otherDoc).Should().Be(FullHash(otherDoc));

        // 回到旧页：状态已被覆盖，必须按该页内容全量重建且一致。
        tracker.ComputeStrokesHash("doc.pdf", 1, page1).Should().Be(FullHash(page1));
    }

    [Fact]
    public void Reset_ShouldForceFullRecompute_WithConsistentResult()
    {
        var tracker = new InkPageHashTracker();
        var strokes = new List<InkStrokeData> { MakeStroke(0) };
        tracker.ComputeStrokesHash("doc.pdf", 1, strokes).Should().Be(FullHash(strokes));

        strokes.Add(MakeStroke(1));
        tracker.Reset();
        tracker.ComputeStrokesHash("doc.pdf", 1, strokes).Should().Be(FullHash(strokes));
    }

    private static InkStrokeData MakeStroke(int seed, bool withSubItems = false)
    {
        var stroke = new InkStrokeData
        {
            ColorHex = $"#{seed % 255:X2}00FF",
            BrushSize = 1.0 + seed,
            GeometryPath = $"M {seed} 0 L {seed + 1} 1 L {seed + 2} 3"
        };
        if (withSubItems)
        {
            stroke.Blooms.Add(new InkBloomData { GeometryPath = $"M {seed} 5 L {seed + 1} 6", Opacity = 0.5 });
            stroke.Ribbons.Add(new InkRibbonData { GeometryPath = $"M {seed} 7 L {seed + 1} 8", Opacity = 0.6, RibbonT = 0.25 });
        }

        return stroke;
    }

    private static InkStrokeData CloneWithNewPath(InkStrokeData stroke, string newPath)
    {
        return new InkStrokeData
        {
            ColorHex = stroke.ColorHex,
            BrushSize = stroke.BrushSize,
            GeometryPath = newPath,
            Blooms = stroke.Blooms,
            Ribbons = stroke.Ribbons
        };
    }

    private static string FullHash(IReadOnlyList<InkStrokeData> strokes)
    {
        if (strokes.Count == 0)
        {
            return "empty";
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var stroke in strokes)
        {
            InkExportFingerprintUtilities.AppendStrokePayload(hash, stroke);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
