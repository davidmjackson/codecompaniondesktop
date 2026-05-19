namespace CodeCompanionDesktop.Settings;

public sealed class AppSettings
{
    public const string ElevenLabsProvider = "ElevenLabs";
    public const string DefaultElevenLabsVoiceId = "JBFqnCBsd6RMkjVDRZzb";
    public const string DefaultElevenLabsModelId = "eleven_multilingual_v2";
    public const string DefaultElevenLabsOutputFormat = "mp3_44100_128";
    public const int DefaultMaxQueuedBridgeSpeechRequests = 3;
    public const int MinQueuedBridgeSpeechRequests = 1;
    public const int MaxQueuedBridgeSpeechRequestLimit = 10;

    public bool StartHiddenToTray { get; set; }

    public bool QueueBridgeSpeechRequests { get; set; }

    public int MaxQueuedBridgeSpeechRequests { get; set; } = DefaultMaxQueuedBridgeSpeechRequests;

    public string SpeechProvider { get; set; } = ElevenLabsProvider;

    public string ElevenLabsVoiceId { get; set; } = DefaultElevenLabsVoiceId;

    public string ElevenLabsModelId { get; set; } = DefaultElevenLabsModelId;

    public string ElevenLabsOutputFormat { get; set; } = DefaultElevenLabsOutputFormat;

    public bool ShowElevenLabsQuotaMeter { get; set; } = true;

    public ElevenLabsQuotaSnapshotData? LastKnownElevenLabsQuota { get; set; }

    public void Normalize()
    {
        MaxQueuedBridgeSpeechRequests = Math.Clamp(
            MaxQueuedBridgeSpeechRequests,
            MinQueuedBridgeSpeechRequests,
            MaxQueuedBridgeSpeechRequestLimit);

        SpeechProvider = string.Equals(SpeechProvider?.Trim(), ElevenLabsProvider, StringComparison.OrdinalIgnoreCase)
            ? ElevenLabsProvider
            : ElevenLabsProvider;
        ElevenLabsVoiceId = string.IsNullOrWhiteSpace(ElevenLabsVoiceId)
            ? DefaultElevenLabsVoiceId
            : ElevenLabsVoiceId.Trim();
        ElevenLabsModelId = string.IsNullOrWhiteSpace(ElevenLabsModelId)
            ? DefaultElevenLabsModelId
            : ElevenLabsModelId.Trim();
        ElevenLabsOutputFormat = string.IsNullOrWhiteSpace(ElevenLabsOutputFormat)
            ? DefaultElevenLabsOutputFormat
            : ElevenLabsOutputFormat.Trim();
    }
}
