namespace ClassroomToolkit.Infra.Logging;

public sealed record LogRetentionOptions(
    int RetentionDays = 14,
    long MaxHistoricalFileBytes = 10 * 1024 * 1024)
{
    public int EffectiveRetentionDays => Math.Max(1, RetentionDays);

    public long EffectiveMaxHistoricalFileBytes => Math.Max(1, MaxHistoricalFileBytes);
}
