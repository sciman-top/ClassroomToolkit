namespace ClassroomToolkit.Interop.Presentation;

/// <summary>
/// 分类器覆盖文档的持久化 JSON 契约：overrides JSON 是持久化与跨机导入格式，
/// 属性名与声明顺序即序列化字节契约（亦为包签名输入），变更须随附历史文件迁移。
/// </summary>
public sealed class PresentationClassifierOverridesDocument
{
    public List<string>? AdditionalWpsClassTokens { get; set; }

    public List<string>? AdditionalOfficeClassTokens { get; set; }

    public List<string>? AdditionalSlideshowClassTokens { get; set; }

    public List<string>? AdditionalWpsProcessTokens { get; set; }

    public List<string>? AdditionalOfficeProcessTokens { get; set; }

    public int? ClassMatchWeight { get; set; }

    public int? ProcessMatchWeight { get; set; }

    public int? NoCaptionWeight { get; set; }

    public int? IsFullscreenWeight { get; set; }

    public int? FullscreenClassMatchBonus { get; set; }

    public bool? RequireClassMatchOrFullscreen { get; set; }

    public int? MinimumCandidateScore { get; set; }
}

public static class PresentationClassifierProcessTokens
{
    /// <summary>分类器进程 token 契约：去首尾空白并剥离 .exe 后缀。</summary>
    public static string Normalize(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return string.Empty;
        }

        var normalized = processName.Trim();
        if (normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }

        return normalized;
    }
}
