using CodeCompanionDesktop.Bridge;

namespace CodeCompanionDesktop.Tests.Bridge;

public sealed class ClientTrustStoreTests
{
    [Fact]
    public void RecordHelloCreatesPendingClientAndAuthorizationCanBeChanged()
    {
        var directory = Directory.CreateTempSubdirectory("code-companion-client-trust-");
        try
        {
            var store = new ClientTrustStore(Path.Combine(directory.FullName, "client-trust.json"));
            var client = new BridgeClient("test-client", "Code Companion Voice", "0.0.0", "windows", "windows");
            var workspace = new BridgeWorkspace(
                "codecompaniondesktop",
                "Code Companion Desktop",
                ["D:\\Development\\CodeCompanionDesktop"]);

            var record = store.RecordHello(client, workspace);

            Assert.Equal(ClientTrustStore.Pending, record.Authorization);
            Assert.True(store.TrySetAuthorization("test-client", ClientTrustStore.Allowed));
            Assert.Equal(ClientTrustStore.Allowed, store.Load().Clients.Single().Authorization);

            var details = Assert.Single(store.LoadClientDetails(10));
            Assert.Contains("Code Companion Voice (test-client)", details, StringComparison.Ordinal);
            Assert.Contains("Authorization: allowed", details, StringComparison.Ordinal);
            Assert.Contains("Project: Code Companion Desktop (codecompaniondesktop)", details, StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void UnknownClientAuthorizationReturnsFalse()
    {
        var directory = Directory.CreateTempSubdirectory("code-companion-client-trust-");
        try
        {
            var store = new ClientTrustStore(Path.Combine(directory.FullName, "client-trust.json"));

            Assert.False(store.TrySetAuthorization("missing-client", ClientTrustStore.Allowed));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
