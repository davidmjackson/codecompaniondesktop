namespace CodeCompanionDesktop.Settings;

public sealed class ElevenLabsQuotaSnapshotData
{
    public long CharacterCount { get; set; }

    public long CharacterLimit { get; set; }

    public long NextResetUnix { get; set; }

    public string Tier { get; set; } = string.Empty;

    public long AsOfUnix { get; set; }
}
