using System;

namespace ClassroomToolkit.App.Paint.Brushes;

/// <summary>
/// 板卡尺寸自适应速度参考：教学一体机/大屏上同样的手速会产生更高的 DIP 速度，
/// 固定的速度阈值会让速度→宽度映射落入饱和段（全笔贴底细线）。
/// 以参考工作区对角线归一，只缩放"绝对速度参考"族参数；
/// 归一化阈值（如 DunBiSpeedThreshold 作用于 normalizedSpeed）随
/// VelocityThreshold 自动等效缩放，无需重复处理。
/// 每次构建 renderer 配置时从工厂新配置应用一次，天然幂等、可测试。
/// </summary>
internal static class BrushSpeedReferenceScaler
{
    internal const double ReferenceDiagonalDip = 2400.0;
    internal const double MinScale = 1.0;
    internal const double MaxScale = 2.2;

    public static double ResolveScale(double workingAreaDiagonalDip)
    {
        if (!double.IsFinite(workingAreaDiagonalDip) || workingAreaDiagonalDip <= 0.0)
        {
            return 1.0;
        }

        return Math.Clamp(workingAreaDiagonalDip / ReferenceDiagonalDip, MinScale, MaxScale);
    }

    public static void ApplyToCalligraphyConfig(BrushPhysicsConfig config, double scale)
    {
        ArgumentNullException.ThrowIfNull(config);
        double safeScale = Math.Clamp(scale, MinScale, MaxScale);
        config.VelocityThreshold = Math.Clamp(config.VelocityThreshold * safeScale, 0.2, 8.0);
        config.WidthLowPassSpeedReference = Math.Clamp(config.WidthLowPassSpeedReference * safeScale, 0.2, 12.0);
        config.PositionSmoothingSpeedReference = Math.Clamp(config.PositionSmoothingSpeedReference * safeScale, 0.2, 12.0);
        config.AdaptiveSamplingSpeedReference = Math.Clamp(config.AdaptiveSamplingSpeedReference * safeScale, 0.2, 12.0);
    }
}
