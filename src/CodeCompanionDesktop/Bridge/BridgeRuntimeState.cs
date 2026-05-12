namespace CodeCompanionDesktop.Bridge;

public sealed class BridgeRuntimeState
{
    private readonly object syncRoot = new();
    private bool isSpeaking;
    private bool queueBridgeSpeechRequests;
    private int pendingSpeechRequests;
    private int maxQueuedSpeechRequests = 3;

    public bool IsSpeaking
    {
        get
        {
            lock (syncRoot)
            {
                return isSpeaking;
            }
        }
    }

    public bool QueueBridgeSpeechRequests
    {
        get
        {
            lock (syncRoot)
            {
                return queueBridgeSpeechRequests;
            }
        }
    }

    public int PendingSpeechRequests
    {
        get
        {
            lock (syncRoot)
            {
                return pendingSpeechRequests;
            }
        }
    }

    public int MaxQueuedSpeechRequests
    {
        get
        {
            lock (syncRoot)
            {
                return maxQueuedSpeechRequests;
            }
        }
    }

    public string LastStatus { get; private set; } = "No bridge requests yet.";

    public void ConfigureQueue(bool isEnabled, int maxQueuedRequests)
    {
        lock (syncRoot)
        {
            queueBridgeSpeechRequests = isEnabled;
            maxQueuedSpeechRequests = maxQueuedRequests;
            LastStatus = isEnabled
                ? $"Bridge speech queue enabled. Limit: {maxQueuedRequests}."
                : "Bridge speech queue disabled. Busy requests are rejected.";
        }
    }

    public bool TryBeginSpeaking()
    {
        lock (syncRoot)
        {
            if (isSpeaking)
            {
                LastStatus = "Bridge rejected request: speech already in progress.";
                return false;
            }

            isSpeaking = true;
            LastStatus = "Bridge speech request started.";
            return true;
        }
    }

    public void QueueSpeechRequest(int pendingCount)
    {
        lock (syncRoot)
        {
            pendingSpeechRequests = pendingCount;
            LastStatus = $"Bridge speech request queued. Pending: {pendingCount}.";
        }
    }

    public void DequeueSpeechRequest(int pendingCount)
    {
        lock (syncRoot)
        {
            pendingSpeechRequests = pendingCount;
            LastStatus = $"Bridge speech request dequeued. Pending: {pendingCount}.";
        }
    }

    public void RejectQueueFull()
    {
        lock (syncRoot)
        {
            LastStatus = $"Bridge rejected request: speech queue is full at {pendingSpeechRequests}/{maxQueuedSpeechRequests}.";
        }
    }

    public void CompleteSpeaking()
    {
        lock (syncRoot)
        {
            isSpeaking = false;
            LastStatus = "Bridge speech request completed.";
        }
    }

    public void FailSpeaking(string error)
    {
        lock (syncRoot)
        {
            isSpeaking = false;
            LastStatus = $"Bridge speech request failed: {error}";
        }
    }
}
