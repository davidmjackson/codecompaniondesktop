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
                RecentSpeechResults = ["speech one"]
            });

            var loaded = store.Load();

            Assert.Equal(["client one"], loaded.RecentBridgeClients);
            Assert.Equal(["speech one"], loaded.RecentSpeechResults);
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
                RecentSpeechResults = ["persisted speech"]
            });

            var state = new BridgeRuntimeState(store);
            var snapshot = state.GetDiagnosticsSnapshot();

            Assert.Equal(["persisted client"], snapshot.RecentBridgeClients);
            Assert.Equal(["persisted speech"], snapshot.RecentSpeechResults);
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
