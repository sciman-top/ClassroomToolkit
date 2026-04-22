using System;

namespace ClassroomToolkit.App.Paint.Brushes;

public enum TaperCapStyle
{
    Hidden = 0,
    Exposed = 1
}

public class BrushPhysicsConfig
{
    public string PresetName { get; set; } = "CalligraphyBalanced";
    public string RenderModeTag { get; set; } = "Clarity";
    public bool EnableDebugMoveTelemetry { get; set; }
    public double MinWidthFactor { get; set; } = 0.22;
    public double MaxWidthFactor { get; set; } = 1.8;
    public double MinStrokeWidthPx { get; set; } = 2.2;
    public double MaxStrokeWidthMultiplier { get; set; } = 2.6;
    public double WidthSmoothing { get; set; } = 0.86;
    public double WidthLowPassMinAlpha { get; set; } = 0.72;
    public double WidthLowPassMaxAlpha { get; set; } = 0.94;
    public double WidthLowPassSpeedReference { get; set; } = 1.8;
    public bool SimulateStartCap { get; set; } = true;
    public bool SimulateEndTaper { get; set; } = true;
    public double VelocityThreshold { get; set; } = 1.3;
    public int VelocitySmoothWindow { get; set; } = 6;
    public int PressureSmoothWindow { get; set; } = 7;
    public double RealPressureWidthInfluence { get; set; } = 0.55;
    public double RealPressureWidthScale { get; set; } = 0.32;
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
    public double RdpCornerPreserveAngleDegrees { get; set; } = 42.0;
    public int MinUpsampleSteps { get; set; } = 2;
    public int MaxUpsampleSteps { get; set; } = 10;
    public double UpsampleTargetSpacing { get; set; } = 2.2;
    public double UpsampleCurvatureBoost { get; set; } = 0.55;
    public double UpsampleCurvatureReferenceDegrees { get; set; } = 70.0;
    public double ArcLengthResampleStepPx { get; set; } = 1.8;
    public int MaxRawPointCount { get; set; } = 10000;
    public int MaxResampledPointCount { get; set; } = 2500;
    public double WidthGamma { get; set; } = 1.0;
    public bool EnableEndpointTaperPostResample { get; set; } = true;
    public double TaperLengthPx { get; set; } = 8.0;
    public double TaperLenScale { get; set; } = 1.0;
    public double TaperRadiusScaleK { get; set; } = 2.6;
    public double TaperStrength { get; set; } = 0.72;
    public TaperCapStyle StartTaperStyle { get; set; } = TaperCapStyle.Hidden;
    public TaperCapStyle EndTaperStyle { get; set; } = TaperCapStyle.Hidden;
    // Dot-like head blend cap: larger => stronger head taper participation; smaller => thicker head.
    public double DotLikeHeadMixCap { get; set; } = 0.55;
    // Dot-like tail sharp floor: smaller => sharper tail, larger => rounder/less sharp tail.
    public double DotLikeTailSharpMin { get; set; } = 0.86;
    public double SpeedFloorPxPerMs { get; set; } = 0.10;
    public double LowSpeedWidthMaxFactor { get; set; } = 1.95;

    // 笔锋效果参数
    public double StartCapLength { get; set; } = 0.05;
    public double EndTaperLength { get; set; } = 0.3;  // v10: 增加到25%
    public int MinTaperPoints { get; set; } = 5;

    // 修复蝌蚪头：起笔阶段忽略速度
    public int StartVelocityRampUpPoints { get; set; } = 6;
    public double MinVelocityClamp { get; set; } = 0.3;
    public int StartBurstSuppressPoints { get; set; } = 4;
    public double StartBurstMaxWidthFactor { get; set; } = 1.05;
    public double StartBurstAccumulationCap { get; set; } = 0.28;

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
    public bool EnableOrientationAnisotropy { get; set; } = true;
    public double OrientationAnisotropyMix { get; set; } = 0.65;
    public double OrientationStrengthMin { get; set; } = 0.35;
    public double OrientationStrengthMax { get; set; } = 1.35;
    public double OrientationAngleOffsetDegrees { get; set; } = 90.0;

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

    // 简化物理模型（含水量 + 纸张吸收）
    public double InitialInkWetness { get; set; } = 0.62;
    public double WetnessResponse { get; set; } = 0.22;
    public double WetnessEvaporationPerSecond { get; set; } = 0.18;
    public double WetnessPressureInfluence { get; set; } = 0.4;
    public double WetnessSlowSpeedBoost { get; set; } = 0.35;
    public double PaperAbsorption { get; set; } = 0.48;
    public double DynamicCoreOpacityDry { get; set; } = 0.84;
    public double DynamicCoreOpacityWet { get; set; } = 1.0;

    public static BrushPhysicsConfig DefaultSmooth => CreateCalligraphyBalanced();

    public static BrushPhysicsConfig CreateCalligraphySharp()
    {
        return new BrushPhysicsConfig
        {
            PresetName = "CalligraphySharp",
            RenderModeTag = "Clarity",
            StartCapLength = 0.1,
            MinStrokeWidthPx = 2.1,
            MaxStrokeWidthMultiplier = 2.7,
            WidthSmoothing = 0.92,
            WidthLowPassMinAlpha = 0.7,
            WidthLowPassMaxAlpha = 0.93,
            WidthLowPassSpeedReference = 1.9,
            VelocityThreshold = 1.25,
            RealPressureWidthInfluence = 0.64,
            RealPressureWidthScale = 0.4,
            PositionSmoothingMinAlpha = 0.38,
            PositionSmoothingMaxAlpha = 0.9,
            AdaptiveSamplingMinFactor = 0.06,
            AdaptiveSamplingMaxFactor = 0.15,
            RdpEpsilonFactor = 0.07,
            MinUpsampleSteps = 2,
            MaxUpsampleSteps = 11,
            UpsampleTargetSpacing = 1.9,
            UpsampleCurvatureBoost = 0.7,
            UpsampleCurvatureReferenceDegrees = 60.0,
            ArcLengthResampleStepPx = 1.4,
            WidthGamma = 0.92,
            TaperLengthPx = 10.0,
            TaperStrength = 0.8,
            StartTaperStyle = TaperCapStyle.Exposed,
            EndTaperStyle = TaperCapStyle.Exposed,
            SpeedFloorPxPerMs = 0.11,
            LowSpeedWidthMaxFactor = 1.9,
            StartVelocityRampUpPoints = 6,
            StartBurstSuppressPoints = 3,
            StartBurstMaxWidthFactor = 1.0,
            StartBurstAccumulationCap = 0.24,
            VelocityWidthFactor = 0.75,
            EndTaperStartProgress = 0.64,
            EndVelocityDecoupleStart = 0.8,
            TaperMinWidthFactor = 0.45,
            AnisotropyStrength = 0.045,
            OrientationAnisotropyMix = 0.78,
            OrientationStrengthMin = 0.4,
            OrientationStrengthMax = 1.45,
            DynamicCoreOpacityDry = 0.8,
            DynamicCoreOpacityWet = 0.98
        };
    }

    public static BrushPhysicsConfig CreateCalligraphySoft()
    {
        return new BrushPhysicsConfig
        {
            PresetName = "CalligraphySoft",
            RenderModeTag = "InkFeel",
            StartCapLength = 0.08,
            MinStrokeWidthPx = 2.5,
            MaxStrokeWidthMultiplier = 2.45,
            WidthSmoothing = 0.95,
            WidthLowPassMinAlpha = 0.76,
            WidthLowPassMaxAlpha = 0.95,
            WidthLowPassSpeedReference = 1.6,
            VelocityThreshold = 1.6,
            RealPressureWidthInfluence = 0.48,
            RealPressureWidthScale = 0.24,
            PositionSmoothingMinAlpha = 0.26,
            PositionSmoothingMaxAlpha = 0.8,
            AdaptiveSamplingMinFactor = 0.08,
            AdaptiveSamplingMaxFactor = 0.2,
            RdpEpsilonFactor = 0.085,
            MinUpsampleSteps = 2,
            MaxUpsampleSteps = 9,
            UpsampleTargetSpacing = 2.6,
            UpsampleCurvatureBoost = 0.45,
            UpsampleCurvatureReferenceDegrees = 75.0,
            ArcLengthResampleStepPx = 2.2,
            WidthGamma = 1.16,
            TaperLengthPx = 14.0,
            TaperStrength = 0.88,
            StartTaperStyle = TaperCapStyle.Exposed,
            EndTaperStyle = TaperCapStyle.Exposed,
            SpeedFloorPxPerMs = 0.08,
            LowSpeedWidthMaxFactor = 2.25,
            StartVelocityRampUpPoints = 13,
            StartBurstSuppressPoints = 4,
            StartBurstMaxWidthFactor = 1.08,
            StartBurstAccumulationCap = 0.32,
            VelocityWidthFactor = 0.53,
            EndTaperStartProgress = 0.78,
            EndVelocityDecoupleStart = 0.86,
            TaperMinWidthFactor = 0.68,
            AnisotropyStrength = 0.035,
            OrientationAnisotropyMix = 0.55,
            OrientationStrengthMin = 0.3,
            OrientationStrengthMax = 1.18,
            FlyingWhiteThreshold = 0.95,
            FlyingWhiteNoiseIntensity = 0.008,
            FiberNoiseIntensity = 0.0015,
            MultiRibbonOffsetJitter = 0.006,
            MultiRibbonWidthJitter = 0.012,
            DynamicCoreOpacityDry = 0.86,
            DynamicCoreOpacityWet = 1.0
        };
    }

    public static BrushPhysicsConfig CreateCalligraphyBalanced()
    {
        return new BrushPhysicsConfig
        {
            PresetName = "CalligraphyBalanced",
            RenderModeTag = "Clarity",
            StartCapLength = 0.2,
            MinStrokeWidthPx = 2.3,
            MaxStrokeWidthMultiplier = 2.55,
            WidthSmoothing = 0.955,
            WidthLowPassMinAlpha = 0.73,
            WidthLowPassMaxAlpha = 0.94,
            WidthLowPassSpeedReference = 1.75,
            VelocityThreshold = 1.4,
            PressureSmoothWindow = 8,
            RealPressureWidthInfluence = 0.5,
            RealPressureWidthScale = 0.28,
            PositionSmoothingMinAlpha = 0.32,
            PositionSmoothingMaxAlpha = 0.88,
            AdaptiveSamplingMinFactor = 0.07,
            AdaptiveSamplingMaxFactor = 0.18,
            RdpEpsilonFactor = 0.075,
            MinUpsampleSteps = 2,
            MaxUpsampleSteps = 10,
            UpsampleTargetSpacing = 2.2,
            UpsampleCurvatureBoost = 0.58,
            UpsampleCurvatureReferenceDegrees = 68.0,
            ArcLengthResampleStepPx = 1.8,
            WidthGamma = 1.0,
            TaperLengthPx = 10.0,
            TaperStrength = 0.78,
            StartTaperStyle = TaperCapStyle.Hidden,
            EndTaperStyle = TaperCapStyle.Hidden,
            SpeedFloorPxPerMs = 0.10,
            LowSpeedWidthMaxFactor = 2.0,
            StartVelocityRampUpPoints = 15,
            StartBurstSuppressPoints = 4,
            StartBurstMaxWidthFactor = 1.04,
            StartBurstAccumulationCap = 0.28,
            VelocityWidthFactor = 0.58,
            EndTaperStartProgress = 0.68,
            EndVelocityDecoupleStart = 0.83,
            TaperMinWidthFactor = 0.46,
            DunBiSpreadRate = 0.44,
            DunBiMaxAccumulation = 1.08,
            AnisotropyStrength = 0.045,
            OrientationAnisotropyMix = 0.65,
            OrientationStrengthMin = 0.35,
            OrientationStrengthMax = 1.35,
            DynamicCoreOpacityDry = 0.84,
            DynamicCoreOpacityWet = 1.0
        };
    }

    public static BrushPhysicsConfig CreateCalligraphyClarity()
    {
        var config = CreateCalligraphyBalanced();
        config.PresetName = "CalligraphyClarity";
        config.RenderModeTag = "Clarity";
        config.ArcLengthResampleStepPx = 1.6;
        config.WidthGamma = 0.96;
        config.TaperLengthPx = 8.0;
        config.TaperLenScale = 1.0;
        config.TaperStrength = 0.72;
        config.StartTaperStyle = TaperCapStyle.Hidden;
        config.EndTaperStyle = TaperCapStyle.Hidden;
        config.DotLikeHeadMixCap = 0.38;
        config.DotLikeTailSharpMin = 0.92;
        config.SpeedFloorPxPerMs = 0.10;
        config.LowSpeedWidthMaxFactor = 1.95;
        config.DunBiMaxAccumulation = Math.Min(config.DunBiMaxAccumulation, 1.03);
        config.MultiRibbonOffsetJitter = Math.Min(config.MultiRibbonOffsetJitter, 0.01);
        config.MultiRibbonWidthJitter = Math.Min(config.MultiRibbonWidthJitter, 0.02);
        return config;
    }

    public static BrushPhysicsConfig CreateCalligraphyInkFeel()
    {
        var config = CreateCalligraphySoft();
        config.PresetName = "CalligraphyInkFeel";
        config.RenderModeTag = "InkFeel";
        config.ArcLengthResampleStepPx = 2.1;
        config.WidthGamma = 1.12;
        config.TaperLengthPx = 14.0;
        config.TaperLenScale = 1.2;
        config.TaperStrength = 0.88;
        config.StartTaperStyle = TaperCapStyle.Exposed;
        config.EndTaperStyle = TaperCapStyle.Exposed;
        config.DotLikeHeadMixCap = 0.55;
        config.DotLikeTailSharpMin = 0.86;
        config.SpeedFloorPxPerMs = 0.08;
        config.LowSpeedWidthMaxFactor = 2.25;
        config.EndTaperStartProgress = Math.Min(config.EndTaperStartProgress, 0.72);
        config.TaperMinWidthFactor = Math.Min(config.TaperMinWidthFactor, 0.58);
        config.DunBiMaxAccumulation = Math.Max(config.DunBiMaxAccumulation, 1.16);
        config.DunBiSpreadRate = Math.Max(config.DunBiSpreadRate, 0.62);
        return config;
    }
}
