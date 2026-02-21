using System;

namespace ClassroomToolkit.App.Paint.Brushes;

public class BrushPhysicsConfig
{
    public double MinWidthFactor { get; set; } = 0.22;
    public double MaxWidthFactor { get; set; } = 1.8;
    public double MinStrokeWidthPx { get; set; } = 2.2;
    public double MaxStrokeWidthMultiplier { get; set; } = 2.6;
    public double WidthSmoothing { get; set; } = 0.86;
    public bool SimulateStartCap { get; set; } = true;
    public bool SimulateEndTaper { get; set; } = true;
    public double VelocityThreshold { get; set; } = 1.3;
    public int VelocitySmoothWindow { get; set; } = 6;
    public int PressureSmoothWindow { get; set; } = 7;
    public double CapIgnoreVelocityRatio { get; set; } = 0.1;
    public double PositionSmoothingMinAlpha { get; set; } = 0.45;
    public double PositionSmoothingMaxAlpha { get; set; } = 0.9;
    public double PositionSmoothingSpeedReference { get; set; } = 2.0;
    public bool EnableAdaptiveSampling { get; set; } = true;
    public double AdaptiveSamplingMinFactor { get; set; } = 0.1;
    public double AdaptiveSamplingMaxFactor { get; set; } = 0.24;
    public double AdaptiveSamplingSpeedReference { get; set; } = 2.4;
    public bool EnableRdpSimplify { get; set; } = true;
    public double RdpEpsilonFactor { get; set; } = 0.12;
    public double RdpMinEpsilon { get; set; } = 0.6;

    // 笔锋效果参数
    public double StartCapLength { get; set; } = 0.05;
    public double EndTaperLength { get; set; } = 0.3;  // v10: 增加到25%
    public int MinTaperPoints { get; set; } = 5;

    // 修复蝌蚪头：起笔阶段忽略速度
    public int StartVelocityRampUpPoints { get; set; } = 6;
    public double MinVelocityClamp { get; set; } = 0.3;

    // 数据验证参数
    public double MaxPointJumpDistance { get; set; } = 100.0;

    // v10 新增参数
    public double VelocityWidthFactor { get; set; } = 0.82;  // kWidth: 速度影响强度
    public double EndTaperStartProgress { get; set; } = 0.68;  // 收笔开始位置
    public double EndVelocityDecoupleStart { get; set; } = 0.82;  // 速度解耦开始位置
    public double FlyingWhiteThreshold { get; set; } = 0.86;  // 飞白速度阈值
    public double FlyingWhiteNoiseIntensity { get; set; } = 0.02;  // 飞白噪声强度

    // v11 新增参数
    public double DunBiSpeedThreshold { get; set; } = 0.4;  // 顿笔速度阈值
    public double DunBiSpreadRate { get; set; } = 0.55;     // 墨水扩散速率
    public double DunBiMaxAccumulation { get; set; } = 1.2; // 最大累积倍数
    public double DunBiDecayRate { get; set; } = 0.2;     // 累积衰减速率
    public double FlyingWhiteNoiseFrequency { get; set; } = 4.2;  // 噪声频率
    public double FlyingWhiteNoiseReductionProgress { get; set; } = 0.78;  // 噪声减少起点
    public double CapRoundThreshold { get; set; } = 0.72;   // 圆笔锋阈值（相对于 baseSize）
    public double TaperMinWidthFactor { get; set; } = 0.5; // 笔锋最小宽度因子
    public double FiberNoiseIntensity { get; set; } = 0.003; // 纸张纤维噪声强度
    public double FiberNoiseFrequency { get; set; } = 0.35; // 纸张纤维噪声频率
    public double AnisotropyStrength { get; set; } = 0.08; // 笔锋方向性强度
    public double BrushAngleDegrees { get; set; } = -45.0; // 笔锋默认角度

    // 多毫叠加参数
    public bool EnableMultiRibbon { get; set; } = true;
    public int MultiRibbonCount { get; set; } = 3;
    public double MultiRibbonOffsetFactor { get; set; } = 0.09;
    public double MultiRibbonOffsetJitter { get; set; } = 0.02;
    public double MultiRibbonWidthJitter { get; set; } = 0.04;
    public double MultiRibbonWidthFalloff { get; set; } = 0.25;

    // 顿笔墨团参数
    public bool InkBloomEnabled { get; set; } = true;
    public double InkBloomOpacity { get; set; } = 0.24;
    public double InkBloomRadiusFactor { get; set; } = 0.7;
    public double InkBloomTangentFactor { get; set; } = 0.55;
    public double InkBloomMinSpacingFactor { get; set; } = 0.35;
    public int InkBloomMaxCount { get; set; } = 12;

    public static BrushPhysicsConfig DefaultSmooth => CreateCalligraphyBalanced();

    public static BrushPhysicsConfig CreateCalligraphySharp()
    {
        return new BrushPhysicsConfig
        {
            StartCapLength = 0.1,
            MinStrokeWidthPx = 2.1,
            MaxStrokeWidthMultiplier = 2.7,
            WidthSmoothing = 0.92,
            VelocityThreshold = 1.25,
            PositionSmoothingMinAlpha = 0.38,
            PositionSmoothingMaxAlpha = 0.9,
            AdaptiveSamplingMinFactor = 0.06,
            AdaptiveSamplingMaxFactor = 0.15,
            RdpEpsilonFactor = 0.07,
            StartVelocityRampUpPoints = 6,
            VelocityWidthFactor = 0.75,
            EndTaperStartProgress = 0.64,
            EndVelocityDecoupleStart = 0.8,
            TaperMinWidthFactor = 0.45,
            AnisotropyStrength = 0.045
        };
    }

    public static BrushPhysicsConfig CreateCalligraphySoft()
    {
        return new BrushPhysicsConfig
        {
            StartCapLength = 0.08,
            MinStrokeWidthPx = 2.5,
            MaxStrokeWidthMultiplier = 2.45,
            WidthSmoothing = 0.95,
            VelocityThreshold = 1.6,
            PositionSmoothingMinAlpha = 0.26,
            PositionSmoothingMaxAlpha = 0.8,
            AdaptiveSamplingMinFactor = 0.08,
            AdaptiveSamplingMaxFactor = 0.2,
            RdpEpsilonFactor = 0.085,
            StartVelocityRampUpPoints = 13,
            VelocityWidthFactor = 0.53,
            EndTaperStartProgress = 0.78,
            EndVelocityDecoupleStart = 0.86,
            TaperMinWidthFactor = 0.68,
            AnisotropyStrength = 0.035,
            FlyingWhiteThreshold = 0.95,
            FlyingWhiteNoiseIntensity = 0.008,
            FiberNoiseIntensity = 0.0015,
            MultiRibbonOffsetJitter = 0.006,
            MultiRibbonWidthJitter = 0.012
        };
    }

    public static BrushPhysicsConfig CreateCalligraphyBalanced()
    {
        return new BrushPhysicsConfig
        {
            StartCapLength = 0.2,
            MinStrokeWidthPx = 2.3,
            MaxStrokeWidthMultiplier = 2.55,
            WidthSmoothing = 0.955,
            VelocityThreshold = 1.4,
            PositionSmoothingMinAlpha = 0.32,
            PositionSmoothingMaxAlpha = 0.88,
            AdaptiveSamplingMinFactor = 0.07,
            AdaptiveSamplingMaxFactor = 0.18,
            RdpEpsilonFactor = 0.075,
            StartVelocityRampUpPoints = 15,
            VelocityWidthFactor = 0.58,
            EndTaperStartProgress = 0.71,
            EndVelocityDecoupleStart = 0.83,
            TaperMinWidthFactor = 0.52,
            AnisotropyStrength = 0.045
        };
    }
}
