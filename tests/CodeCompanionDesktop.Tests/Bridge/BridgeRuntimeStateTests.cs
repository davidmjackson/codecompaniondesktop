using CodeCompanionDesktop.Bridge;

namespace CodeCompanionDesktop.Tests.Bridge;

public sealed class BridgeRuntimeStateTests
{
    [Fact]
    public void DiagnosticsSnapshotIncludesCandidateDecisionAndRecentResults()
    {
        var state = new BridgeRuntimeState();
        state.ConfigureQueue(true, 5);
        state.RecordClientSeen("Code Companion Voice", "windows", "codecompaniondesktop");
        state.RecordSpeechCandidate("windows", "codecompaniondesktop", "message-1", "Speech diagnostics test.");
        state.RecordSpeechCandidateDecision("spoken", "accepted");
        state.RecordProviderError("provider unavailable");
        state.RecordPlaybackError("device unavailable");

        var snapshot = state.GetDiagnosticsSnapshot();

        Assert.True(snapshot.QueueBridgeSpeechRequests);
        Assert.Equal(5, snapshot.MaxQueuedSpeechRequests);
        Assert.Contains("Code Companion Voice", snapshot.LastClient, StringComparison.Ordinal);
        Assert.Contains("message-1", snapshot.LastSpeechCandidate, StringComparison.Ordinal);
        Assert.Equal("spoken (accepted)", snapshot.LastSpeechDecision);
        Assert.Equal("provider unavailable", snapshot.LastProviderError);
        Assert.Equal("device unavailable", snapshot.LastPlaybackError);
        Assert.Contains(snapshot.RecentSpeechResults, result => result.Contains("Candidate spoken (accepted).", StringComparison.Ordinal));
        Assert.Contains(snapshot.RecentSpeechResults, result => result.Contains("Provider error: provider unavailable", StringComparison.Ordinal));
        Assert.Contains(snapshot.RecentSpeechResults, result => result.Contains("Playback error: device unavailable", StringComparison.Ordinal));
    }
}
