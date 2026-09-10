using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ClassroomToolkit.App.Ink;

/// <summary>
/// Owner-scoped cache for frozen ink opacity masks.
/// Rendering callers are dispatcher-confined; keeping the cache owner-scoped avoids
/// sharing mutable WPF state across windows while bounding retained Freezables.
/// </summary>
internal sealed class InkOpacityMaskCache
{
    internal const int PaintTextureVariant = 1;
    internal const int ExportTextureVariant = 2;

    private readonly int _capacity;
    private readonly Dictionary<InkOpacityMaskCacheKey, DrawingBrush> _entries = new();

    internal InkOpacityMaskCache(int capacity)
    {
        _capacity = Math.Max(1, capacity);
    }

    internal int Count => _entries.Count;

    internal DrawingBrush? GetOrCreate(
        Rect bounds,
        double inkFlow,
        Vector? strokeDirection,
        double brushSize,
        int seed,
        int textureVariant,
        Func<DrawingBrush?> factory)
    {
        return GetOrCreate(
            bounds,
            inkFlow,
            strokeDirection,
            brushSize,
            seed,
            wetnessStart: null,
            wetnessEnd: null,
            textureVariant: textureVariant,
            factory: factory);
    }

    internal DrawingBrush? GetOrCreate(
        Rect bounds,
        double inkFlow,
        Vector? strokeDirection,
        double brushSize,
        int seed,
        double? wetnessStart,
        double? wetnessEnd,
        int textureVariant,
        Func<DrawingBrush?> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (bounds.IsEmpty)
        {
            return null;
        }

        var key = InkOpacityMaskCacheKey.Create(
            bounds,
            inkFlow,
            strokeDirection,
            brushSize,
            seed,
            wetnessStart,
            wetnessEnd,
            textureVariant);
        if (_entries.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var created = factory();
        if (created == null)
        {
            return null;
        }

        if (created.CanFreeze && !created.IsFrozen)
        {
            created.Freeze();
        }

        if (_entries.Count >= _capacity)
        {
            _entries.Clear();
        }
        _entries[key] = created;
        return created;
    }

    private readonly record struct InkOpacityMaskCacheKey(
        int TextureVariant,
        Rect Bounds,
        long InkFlowBits,
        long DirectionXBits,
        long DirectionYBits,
        long BrushSizeBits,
        int Seed,
        long WetnessStartBits,
        long WetnessEndBits)
    {
        private const long NullBits = long.MinValue;

        internal static InkOpacityMaskCacheKey Create(
            Rect bounds,
            double inkFlow,
            Vector? strokeDirection,
            double brushSize,
            int seed,
            double? wetnessStart,
            double? wetnessEnd,
            int textureVariant)
        {
            var direction = NormalizeDirection(strokeDirection);
            return new InkOpacityMaskCacheKey(
                textureVariant,
                bounds,
                BitConverter.DoubleToInt64Bits(inkFlow),
                BitConverter.DoubleToInt64Bits(direction.X),
                BitConverter.DoubleToInt64Bits(direction.Y),
                BitConverter.DoubleToInt64Bits(brushSize),
                seed,
                EncodeNullable(wetnessStart),
                EncodeNullable(wetnessEnd));
        }

        private static long EncodeNullable(double? value)
        {
            return value.HasValue ? BitConverter.DoubleToInt64Bits(value.Value) : NullBits;
        }

        private static Vector NormalizeDirection(Vector? strokeDirection)
        {
            var direction = strokeDirection ?? new Vector(1, 0);
            if (!double.IsFinite(direction.X)
                || !double.IsFinite(direction.Y)
                || direction.LengthSquared < 0.0001)
            {
                return new Vector(1, 0);
            }

            direction.Normalize();
            return direction;
        }
    }
}
