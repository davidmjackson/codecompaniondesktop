namespace CodeCompanionDesktop.ElevenLabs;

public sealed record ElevenLabsSubscription(
    long CharacterCount,
    long CharacterLimit,
    long NextCharacterCountResetUnix,
    string Tier);
