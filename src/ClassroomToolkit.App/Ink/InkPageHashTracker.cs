using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using ClassroomToolkit.App.Ink;

namespace ClassroomToolkit.App.Ink;

/// <summary>
/// 每笔画结束的全页哈希增量器：普通书写从 O(全页 payload) SHA256 降为 O(新笔 payload)。
///
/// 完备性论证（为何无需外部失效钩子）：墨迹 payload 的任何运行时变异只有四种形态——
/// 追加、删除、整表替换、替换某引用（GeometryPath/Blooms/Ribbons 的 string 与对象均不可变，
/// "原地修改"实际是赋新引用）。逐笔核对 (笔画引用, GeometryPath 引用, Bloom/Ribbon 引用对)
/// 能捕获全部四种；检测不到变异的唯一前提是所有引用原样保留，而引用不变即内容不变。
/// 校验失败只会退回全量重算（安全方向），不会产出错误哈希。
/// </summary>
internal sealed class InkPageHashTracker
{
    private sealed record StrokeFingerprint(
        object Stroke,
        object GeometryPath,
        List<object> BloomPaths,
        List<object> RibbonPaths);

    private sealed class PageState
    {
        public required string SourcePath { get; init; }
        public required int PageIndex { get; init; }
        public int StrokeCount { get; set; }
        public required List<StrokeFingerprint> Fingerprints { get; init; }
        public required IncrementalHash Running { get; set; }
    }

    private PageState? _state;

    /// <summary>换页、加载或外部全量 Mark 后调用：丢弃运行状态，下一次全量重建。</summary>
    public void Reset()
    {
        _state = null;
    }

    /// <summary>
    /// 计算与 <see cref="ComputeInkHash"/> 逐位一致的哈希：前缀经引用指纹核对未变时
    /// 只追加末笔，否则全量重建。kind 不匹配（换页/换文档）同样全量重建。
    /// </summary>
    public string ComputeStrokesHash(
        string sourcePath,
        int pageIndex,
        IReadOnlyList<InkStrokeData> strokes)
    {
        if (strokes.Count == 0)
        {
            _state = null;
            return "empty";
        }

        var state = _state;
        if (state != null
            && state.SourcePath == sourcePath
            && state.PageIndex == pageIndex
            && TryVerifyAppendOnly(state, strokes, out var appendedStroke))
        {
            var appended = state.Running.Clone();
            InkExportFingerprintUtilities.AppendStrokePayload(appended, appendedStroke);
            // hex 必须取自独立克隆：GetHashAndReset 会清空哈希状态，
            // 而 appended 本身要作为后续追加的运行状态存回。
            var hexSource = appended.Clone();
            Commit(state, strokes, appended);
            return Convert.ToHexString(hexSource.GetHashAndReset());
        }

        var running = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var stroke in strokes)
        {
            InkExportFingerprintUtilities.AppendStrokePayload(running, stroke);
        }

        var rebuilt = new PageState
        {
            SourcePath = sourcePath,
            PageIndex = pageIndex,
            StrokeCount = strokes.Count,
            Fingerprints = BuildFingerprints(strokes),
            Running = running
        };
        _state = rebuilt;
        var final = rebuilt.Running.Clone();
        return Convert.ToHexString(final.GetHashAndReset());
    }

    private static bool TryVerifyAppendOnly(PageState state, IReadOnlyList<InkStrokeData> strokes, out InkStrokeData appended)
    {
        appended = null!;
        if (strokes.Count != state.StrokeCount + 1)
        {
            return false;
        }

        var fingerprints = state.Fingerprints;
        for (var i = 0; i < fingerprints.Count; i++)
        {
            if (!Matches(fingerprints[i], strokes[i]))
            {
                return false;
            }
        }

        appended = strokes[strokes.Count - 1];
        return true;
    }

    private static void Commit(PageState state, IReadOnlyList<InkStrokeData> strokes, IncrementalHash running)
    {
        state.Fingerprints.Add(BuildStrokeFingerprint(strokes[strokes.Count - 1]));
        state.Running.Dispose();
        state.Running = running;
        state.StrokeCount = strokes.Count;
    }

    private static List<StrokeFingerprint> BuildFingerprints(IReadOnlyList<InkStrokeData> strokes)
    {
        var result = new List<StrokeFingerprint>(strokes.Count);
        foreach (var stroke in strokes)
        {
            result.Add(BuildStrokeFingerprint(stroke));
        }

        return result;
    }

    private static StrokeFingerprint BuildStrokeFingerprint(InkStrokeData stroke)
    {
        var bloomPaths = new List<object>(stroke.Blooms.Count);
        foreach (var bloom in stroke.Blooms)
        {
            bloomPaths.Add(bloom.GeometryPath);
        }

        var ribbonPaths = new List<object>(stroke.Ribbons.Count);
        foreach (var ribbon in stroke.Ribbons)
        {
            ribbonPaths.Add(ribbon.GeometryPath);
        }

        return new StrokeFingerprint(stroke, stroke.GeometryPath, bloomPaths, ribbonPaths);
    }

    private static bool Matches(StrokeFingerprint fingerprint, InkStrokeData stroke)
    {
        if (!ReferenceEquals(fingerprint.Stroke, stroke)
            || !ReferenceEquals(fingerprint.GeometryPath, stroke.GeometryPath)
            || fingerprint.BloomPaths.Count != stroke.Blooms.Count
            || fingerprint.RibbonPaths.Count != stroke.Ribbons.Count)
        {
            return false;
        }

        for (var i = 0; i < fingerprint.BloomPaths.Count; i++)
        {
            if (!ReferenceEquals(fingerprint.BloomPaths[i], stroke.Blooms[i].GeometryPath))
            {
                return false;
            }
        }

        for (var i = 0; i < fingerprint.RibbonPaths.Count; i++)
        {
            if (!ReferenceEquals(fingerprint.RibbonPaths[i], stroke.Ribbons[i].GeometryPath))
            {
                return false;
            }
        }

        return true;
    }
}
