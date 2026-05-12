namespace CodeCompanionDesktop.Settings;

public sealed class AppSettings
{
    public const int DefaultMaxQueuedBridgeSpeechRequests = 3;
    public const int MinQueuedBridgeSpeechRequests = 1;
    public const int MaxQueuedBridgeSpeechRequestLimit = 10;

    public bool StartHiddenToTray { get; set; }

    public bool QueueBridgeSpeechRequests { get; set; }

    public int MaxQueuedBridgeSpeechRequests { get; set; } = DefaultMaxQueuedBridgeSpeechRequests;

    public void Normalize()
    {
        MaxQueuedBridgeSpeechRequests = Math.Clamp(
            MaxQueuedBridgeSpeechRequests,
            MinQueuedBridgeSpeechRequests,
            MaxQueuedBridgeSpeechRequestLimit);
    }
}
