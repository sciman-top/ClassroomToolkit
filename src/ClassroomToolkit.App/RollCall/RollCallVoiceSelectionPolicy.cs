namespace ClassroomToolkit.App;

internal readonly record struct RollCallVoiceSelectionDecision(
    bool IsVoiceSelectionEnabled,
    string SelectedVoiceId);

internal static class RollCallVoiceSelectionPolicy
{
    internal static RollCallVoiceSelectionDecision Resolve(
        IReadOnlyList<string> voiceIds,
        string? preferredVoiceId,
        string? fallbackVoiceId)
    {
        ArgumentNullException.ThrowIfNull(voiceIds);

        if (voiceIds.Count == 0)
        {
            return new RollCallVoiceSelectionDecision(
                IsVoiceSelectionEnabled: false,
                SelectedVoiceId: string.Empty);
        }

        var normalizedIds = voiceIds
            .Where(id => id != null)
            .Select(id => id.Trim())
            .ToList();
        var hasUsableVoice = normalizedIds.Any(id => !string.IsNullOrWhiteSpace(id));

        var target = !string.IsNullOrWhiteSpace(preferredVoiceId)
            ? preferredVoiceId.Trim()
            : string.Empty;
        if (string.IsNullOrWhiteSpace(target))
        {
            target = !string.IsNullOrWhiteSpace(fallbackVoiceId)
                ? fallbackVoiceId.Trim()
                : string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(target)
            && normalizedIds.Any(id => id.Equals(target, StringComparison.OrdinalIgnoreCase)))
        {
            return new RollCallVoiceSelectionDecision(
                IsVoiceSelectionEnabled: hasUsableVoice,
                SelectedVoiceId: target);
        }

        var firstUsableVoice = normalizedIds.FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        if (!string.IsNullOrWhiteSpace(firstUsableVoice))
        {
            return new RollCallVoiceSelectionDecision(
                IsVoiceSelectionEnabled: true,
                SelectedVoiceId: firstUsableVoice);
        }

        return new RollCallVoiceSelectionDecision(
            IsVoiceSelectionEnabled: false,
            SelectedVoiceId: normalizedIds[0]);
    }
}
