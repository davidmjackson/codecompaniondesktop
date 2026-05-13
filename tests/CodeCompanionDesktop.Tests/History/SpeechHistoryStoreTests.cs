using CodeCompanionDesktop.Bridge;
using CodeCompanionDesktop.History;

namespace CodeCompanionDesktop.Tests.History;

public sealed class SpeechHistoryStoreTests
{
    [Fact]
    public void SaveAndLoadRoundTripsRecentClientsAndSpeechResults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"code-companion-history-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SpeechHistoryStore(path);
            store.Save(new SpeechHistorySnapshot
            {
                RecentBridgeClients = ["client one"],
                RecentSpeechResults = ["speech one"],
                RecentProjectSpeech =
                [
                    new ProjectSpeechHistoryRecord
                    {
                        TimestampUtc = DateTimeOffset.UtcNow,
                        ProjectId = "codecompaniondesktop",
                        DisplayName = "Code Companion Desktop",
                        ClientName = "Code Companion Voice",
                        Environment = "windows",
                        MessageId = "message-1",
                        Decision = "spoken",
                        Reason = "accepted",
                        Preview = "Speech history test."
                    }
                ]
            });

            var loaded = store.Load();

            Assert.Equal(["client one"], loaded.RecentBridgeClients);
            Assert.Equal(["speech one"], loaded.RecentSpeechResults);
            var projectSpeech = Assert.Single(loaded.RecentProjectSpeech);
            Assert.Equal("codecompaniondesktop", projectSpeech.ProjectId);
            Assert.Equal("spoken", projectSpeech.Decision);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void RuntimeStateLoadsPersistedRecentHistory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"code-companion-history-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SpeechHistoryStore(path);
            store.Save(new SpeechHistorySnapshot
            {
                RecentBridgeClients = ["persisted client"],
                RecentSpeechResults = ["persisted speech"],
                RecentProjectSpeech =
                [
                    new ProjectSpeechHistoryRecord
                    {
                        TimestampUtc = DateTimeOffset.UtcNow,
                        ProjectId = "codecompaniondesktop",
                        DisplayName = "Code Companion Desktop",
                        ClientName = "Code Companion Voice",
                        Environment = "windows",
                        MessageId = "message-1",
                        Decision = "spoken",
                        Reason = "accepted",
                        Preview = "Persisted project speech."
                    }
                ]
            });

            var state = new BridgeRuntimeState(store);
            var snapshot = state.GetDiagnosticsSnapshot();

            Assert.Equal(["persisted client"], snapshot.RecentBridgeClients);
            Assert.Equal(["persisted speech"], snapshot.RecentSpeechResults);
            var projectSpeech = Assert.Single(snapshot.RecentProjectSpeech);
            Assert.Equal("codecompaniondesktop", projectSpeech.ProjectId);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
