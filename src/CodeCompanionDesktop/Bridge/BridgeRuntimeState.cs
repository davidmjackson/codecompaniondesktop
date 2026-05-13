using CodeCompanionDesktop.History;

namespace CodeCompanionDesktop.Bridge;

public sealed class BridgeRuntimeState
{
    private const int MaxRecentBridgeClients = 8;
    private const int MaxRecentSpeechResults = 8;
    private const int MaxRecentProjects = 8;
    private const int MaxRecentProjectSpeech = 40;

    private readonly object syncRoot = new();
    private readonly SpeechHistoryStore? speechHistoryStore;
    private readonly ProjectRegistryStore? projectRegistryStore;
    private readonly List<string> recentBridgeClients = new();
    private readonly List<string> recentSpeechResults = new();
    private readonly List<string> recentProjects = new();
    private readonly List<ProjectSpeechHistoryRecord> recentProjectSpeech = new();
    private bool isSpeaking;
    private bool queueBridgeSpeechRequests;
    private int pendingSpeechRequests;
    private int maxQueuedSpeechRequests = 3;
    private PendingSpeechCandidate? pendingSpeechCandidate;

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

    public BridgeRuntimeState(
        SpeechHistoryStore? speechHistoryStore = null,
        ProjectRegistryStore? projectRegistryStore = null)
    {
        this.speechHistoryStore = speechHistoryStore;
        this.projectRegistryStore = projectRegistryStore;

        var snapshot = speechHistoryStore?.Load();
        if (snapshot is not null)
        {
            recentBridgeClients.AddRange(snapshot.RecentBridgeClients.Take(MaxRecentBridgeClients));
            recentSpeechResults.AddRange(snapshot.RecentSpeechResults.Take(MaxRecentSpeechResults));
            recentProjectSpeech.AddRange(snapshot.RecentProjectSpeech.Take(MaxRecentProjectSpeech));
        }

        if (projectRegistryStore is not null)
        {
            recentProjects.AddRange(projectRegistryStore.LoadRecentSummaries(MaxRecentProjects));
        }
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
                recentProjects.ToArray(),
                recentBridgeClients.ToArray(),
                recentSpeechResults.ToArray(),
                recentProjectSpeech.ToArray());
        }
    }

    public IReadOnlyList<string> LoadProjectRegistryDetails(int maxCount)
    {
        lock (syncRoot)
        {
            if (projectRegistryStore is null)
            {
                return recentProjects.ToArray();
            }

            return projectRegistryStore
                .LoadRecentRecords(maxCount)
                .Select(ProjectRegistryStore.FormatDetails)
                .ToList();
        }
    }

    public IReadOnlyList<string> LoadProjectSpeechHistoryDetails(int maxProjects)
    {
        lock (syncRoot)
        {
            if (recentProjectSpeech.Count == 0)
            {
                return [];
            }

            return recentProjectSpeech
                .GroupBy(record => record.ProjectId, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Max(record => record.TimestampUtc))
                .Take(maxProjects)
                .Select(FormatProjectSpeechHistoryGroup)
                .ToList();
        }
    }

    public bool TryAddProjectRootAlias(string projectId, string root)
    {
        lock (syncRoot)
        {
            if (projectRegistryStore?.TryAddRootAlias(projectId, root) != true)
            {
                return false;
            }

            ReloadRecentProjects();
            LastStatus = $"Project root alias added for {projectId.Trim()}.";
            return true;
        }
    }

    public bool TryRemoveProjectRootAlias(string projectId, string root)
    {
        lock (syncRoot)
        {
            if (projectRegistryStore?.TryRemoveRootAlias(projectId, root) != true)
            {
                return false;
            }

            ReloadRecentProjects();
            LastStatus = $"Project root alias removed for {projectId.Trim()}.";
            return true;
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

    public void RecordClientSeen(BridgeClient client, BridgeWorkspace workspace)
    {
        lock (syncRoot)
        {
            RecordProjectSeen(client, workspace);
            LastClient = $"{client.Name} from {client.Environment} for project {workspace.ProjectId}.";
            LastStatus = $"Bridge client hello received from {client.Name}.";
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

    public void RecordSpeechCandidate(BridgeClient client, BridgeWorkspace workspace, string messageId, string text)
    {
        lock (syncRoot)
        {
            RecordProjectSeen(client, workspace);
            var preview = text.Length <= 80 ? text : $"{text[..80]}...";
            pendingSpeechCandidate = new PendingSpeechCandidate(
                workspace.ProjectId,
                workspace.DisplayName,
                client.Name,
                client.Environment,
                messageId,
                preview);
            LastSpeechCandidate = $"{client.Environment} project {workspace.ProjectId} message {messageId}: {preview}";
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
            AddRecentProjectSpeechResult(decision, reason);
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

    private void AddRecentProjectSpeechResult(string decision, string reason)
    {
        if (pendingSpeechCandidate is null)
        {
            return;
        }

        recentProjectSpeech.Insert(0, new ProjectSpeechHistoryRecord
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            ProjectId = pendingSpeechCandidate.ProjectId,
            DisplayName = pendingSpeechCandidate.DisplayName,
            ClientName = pendingSpeechCandidate.ClientName,
            Environment = pendingSpeechCandidate.Environment,
            MessageId = pendingSpeechCandidate.MessageId,
            Preview = pendingSpeechCandidate.Preview,
            Decision = decision,
            Reason = reason
        });
        if (recentProjectSpeech.Count > MaxRecentProjectSpeech)
        {
            recentProjectSpeech.RemoveRange(MaxRecentProjectSpeech, recentProjectSpeech.Count - MaxRecentProjectSpeech);
        }

        SaveHistory();
    }

    private void RecordProjectSeen(BridgeClient client, BridgeWorkspace workspace)
    {
        var record = projectRegistryStore?.RecordObservation(
            workspace.ProjectId,
            workspace.DisplayName,
            workspace.Roots,
            client.Environment,
            client.Name);

        var summary = record is null
            ? ProjectRegistryStore.FormatSummary(new ProjectRegistryRecord
            {
                ProjectId = workspace.ProjectId,
                DisplayName = workspace.DisplayName,
                ObservedRoots = workspace.Roots.ToList(),
                Environments = [client.Environment],
                ClientNames = [client.Name]
            })
            : ProjectRegistryStore.FormatSummary(record);

        recentProjects.RemoveAll(existing => existing.Contains($"({workspace.ProjectId})", StringComparison.OrdinalIgnoreCase));
        recentProjects.Insert(0, summary);
        if (recentProjects.Count > MaxRecentProjects)
        {
            recentProjects.RemoveRange(MaxRecentProjects, recentProjects.Count - MaxRecentProjects);
        }
    }

    private void ReloadRecentProjects()
    {
        if (projectRegistryStore is null)
        {
            return;
        }

        recentProjects.Clear();
        recentProjects.AddRange(projectRegistryStore.LoadRecentSummaries(MaxRecentProjects));
    }

    private void SaveHistory()
    {
        speechHistoryStore?.Save(new SpeechHistorySnapshot
        {
            RecentBridgeClients = recentBridgeClients.ToList(),
            RecentSpeechResults = recentSpeechResults.ToList(),
            RecentProjectSpeech = recentProjectSpeech.ToList()
        });
    }

    private static string FormatProjectSpeechHistoryGroup(IGrouping<string, ProjectSpeechHistoryRecord> group)
    {
        var latest = group.OrderByDescending(record => record.TimestampUtc).First();
        var items = group
            .OrderByDescending(record => record.TimestampUtc)
            .Take(8)
            .Select(record =>
                $"  - {FormatTimestamp(record.TimestampUtc)} {record.Decision}/{record.Reason} {record.Environment} {record.MessageId}: {record.Preview}");

        return string.Join(
            Environment.NewLine,
            $"{latest.DisplayName} ({latest.ProjectId})",
            string.Join(Environment.NewLine, items));
    }

    private static string FormatTimestamp(DateTimeOffset value)
    {
        return value == default
            ? "unknown"
            : value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }
}

internal sealed record PendingSpeechCandidate(
    string ProjectId,
    string DisplayName,
    string ClientName,
    string Environment,
    string MessageId,
    string Preview);

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
    IReadOnlyList<string> RecentProjects,
    IReadOnlyList<string> RecentBridgeClients,
    IReadOnlyList<string> RecentSpeechResults,
    IReadOnlyList<ProjectSpeechHistoryRecord> RecentProjectSpeech);
