namespace ClassroomToolkit.App.Paint;

/// <summary>
/// 墨迹种子 FNV-1a 32 位核：哈希值进入 InkNoiseTileCache 等 key，各调用点必须逐位一致，
/// 不得单独调整乘数或初值。
/// </summary>
internal static class InkSeedHash
{
    internal static uint Combine(uint hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            hash *= 16777619u;
        }

        return hash;
    }

    internal static uint Combine(uint hash, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Combine(hash, 0);
        }

        unchecked
        {
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }
        }

        return hash;
    }
}
