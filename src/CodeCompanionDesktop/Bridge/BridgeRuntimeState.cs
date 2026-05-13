using CodeCompanionDesktop.History;

namespace CodeCompanionDesktop.Bridge;

public sealed class BridgeRuntimeState
{
    private const int MaxRecentBridgeClients = 8;
    private const int MaxRecentSpeechResults = 8;

    private readonly object syncRoot = new();
    private readonly SpeechHistoryStore? speechHistoryStore;
    private readonly List<string> recentBridgeClients = new();
    private readonly List<string> recentSpeechResults = new();
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

    public string LastClient { get; private set; } = "No bridge clients seen yet.";

    public string LastSpeechCandidate { get; private set; } = "No speech candidates received yet.";

    public string LastSpeechDecision { get; private set; } = "No speech decisions yet.";

    public string LastProviderError { get; private set; } = "No provider errors.";

    public string LastPlaybackError { get; private set; } = "No playback errors.";

    public BridgeRuntimeState(SpeechHistoryStore? speechHistoryStore = null)
    {
        this.speechHistoryStore = speechHistoryStore;

        var snapshot = speechHistoryStore?.Load();
        if (snapshot is null)
        {
            return;
        }

        recentBridgeClients.AddRange(snapshot.RecentBridgeClients.Take(MaxRecentBridgeClients));
        recentSpeechResults.AddRange(snapshot.RecentSpeechResults.Take(MaxRecentSpeechResults));
    }

    public BridgeDiagnosticsSnapshot GetDiagnosticsSnapshot()
    {
        lock (syncRoot)
        {
            return new BridgeDiagnosticsSnapshot(
                isSpeaking,
                queueBridgeSpeechRequests,
                pendingSpeechRequests,
                maxQueuedSpeechRequests,
                LastStatus,
                LastClient,
                LastSpeechCandidate,
                LastSpeechDecision,
                LastProviderError,
                LastPlaybackError,
                recentBridgeClients.ToArray(),
                recentSpeechResults.ToArray());
        }
    }

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

    public void RecordClientSeen(string clientName, string environment, string projectId)
    {
        lock (syncRoot)
        {
            LastClient = $"{clientName} from {environment} for project {projectId}.";
            LastStatus = $"Bridge client hello received from {clientName}.";
            AddRecentBridgeClient(LastClient);
        }
    }

    public void RecordCandidateInboxStarted(string inboxDirectory)
    {
        lock (syncRoot)
        {
            LastStatus = $"Candidate inbox watching: {inboxDirectory}";
        }
    }

    public void RecordCandidateInboxError(string error)
    {
        lock (syncRoot)
        {
            LastStatus = $"Candidate inbox error: {error}";
            AddRecentSpeechResult($"Candidate inbox error: {error}");
        }
    }

    public void RecordSpeechCandidate(string environment, string projectId, string messageId, string text)
    {
        lock (syncRoot)
        {
            var preview = text.Length <= 80 ? text : $"{text[..80]}...";
            LastSpeechCandidate = $"{environment} project {projectId} message {messageId}: {preview}";
            LastStatus = "Bridge speech candidate received.";
        }
    }

    public void RecordSpeechCandidateDecision(string decision, string reason)
    {
        lock (syncRoot)
        {
            LastSpeechDecision = $"{decision} ({reason})";
            LastStatus = $"Bridge speech candidate decision: {LastSpeechDecision}.";
            AddRecentSpeechResult($"Candidate {LastSpeechDecision}.");
        }
    }

    public void ClearProviderError()
    {
        lock (syncRoot)
        {
            LastProviderError = "No provider errors.";
        }
    }

    public void ClearPlaybackError()
    {
        lock (syncRoot)
        {
            LastPlaybackError = "No playback errors.";
        }
    }

    public void RecordProviderError(string error)
    {
        lock (syncRoot)
        {
            LastProviderError = error;
            AddRecentSpeechResult($"Provider error: {error}");
        }
    }

    public void RecordPlaybackError(string error)
    {
        lock (syncRoot)
        {
            LastPlaybackError = error;
            AddRecentSpeechResult($"Playback error: {error}");
        }
    }

    public void RecordPlaybackCompleted(string source)
    {
        lock (syncRoot)
        {
            ClearProviderError();
            ClearPlaybackError();
            AddRecentSpeechResult($"Playback completed from {source}.");
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

    private void AddRecentSpeechResult(string result)
    {
        var timestamp = DateTimeOffset.Now.ToString("HH:mm:ss");
        recentSpeechResults.Insert(0, $"{timestamp} {result}");
        if (recentSpeechResults.Count > MaxRecentSpeechResults)
        {
            recentSpeechResults.RemoveRange(MaxRecentSpeechResults, recentSpeechResults.Count - MaxRecentSpeechResults);
        }

        SaveHistory();
    }

    private void AddRecentBridgeClient(string client)
    {
        var timestamp = DateTimeOffset.Now.ToString("HH:mm:ss");
        var item = $"{timestamp} {client}";
        recentBridgeClients.RemoveAll(existing => existing.EndsWith(client, StringComparison.Ordinal));
        recentBridgeClients.Insert(0, item);
        if (recentBridgeClients.Count > MaxRecentBridgeClients)
        {
            recentBridgeClients.RemoveRange(MaxRecentBridgeClients, recentBridgeClients.Count - MaxRecentBridgeClients);
        }

        SaveHistory();
    }

    private void SaveHistory()
    {
        speechHistoryStore?.Save(new SpeechHistorySnapshot
        {
            RecentBridgeClients = recentBridgeClients.ToList(),
            RecentSpeechResults = recentSpeechResults.ToList()
        });
    }
}

public sealed record BridgeDiagnosticsSnapshot(
    bool IsSpeaking,
    bool QueueBridgeSpeechRequests,
    int PendingSpeechRequests,
    int MaxQueuedSpeechRequests,
    string LastStatus,
    string LastClient,
    string LastSpeechCandidate,
    string LastSpeechDecision,
    string LastProviderError,
    string LastPlaybackError,
    IReadOnlyList<string> RecentBridgeClients,
    IReadOnlyList<string> RecentSpeechResults);
