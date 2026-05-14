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

    [Fact]
    public void MostRecentPendingAuthorizationUpdatesLatestPendingClient()
    {
        var directory = Directory.CreateTempSubdirectory("code-companion-client-trust-");
        try
        {
            var store = new ClientTrustStore(Path.Combine(directory.FullName, "client-trust.json"));
            store.Save(new ClientTrustSnapshot
            {
                Clients =
                [
                    new ClientTrustRecord
                    {
                        ClientId = "already-approved",
                        Name = "Code Companion Voice",
                        Authorization = ClientTrustStore.Allowed,
                        LastSeenUtc = DateTimeOffset.Parse("2026-05-14T09:00:00Z")
                    },
                    new ClientTrustRecord
                    {
                        ClientId = "older-pending",
                        Name = "Code Companion Voice",
                        Authorization = ClientTrustStore.Pending,
                        LastSeenUtc = DateTimeOffset.Parse("2026-05-14T07:00:00Z")
                    },
                    new ClientTrustRecord
                    {
                        ClientId = "latest-pending",
                        Name = "Code Companion Voice",
                        Authorization = ClientTrustStore.Pending,
                        LastSeenUtc = DateTimeOffset.Parse("2026-05-14T08:00:00Z")
                    }
                ]
            });

            Assert.True(store.TrySetMostRecentPendingAuthorization(ClientTrustStore.Allowed, out var updatedClient));

            Assert.NotNull(updatedClient);
            Assert.Equal("latest-pending", updatedClient.ClientId);
            var clients = store.Load().Clients.ToDictionary(client => client.ClientId);
            Assert.Equal(ClientTrustStore.Allowed, clients["latest-pending"].Authorization);
            Assert.Equal(ClientTrustStore.Pending, clients["older-pending"].Authorization);
            Assert.Equal(ClientTrustStore.Allowed, clients["already-approved"].Authorization);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void MostRecentPendingAuthorizationReturnsFalseWhenNoClientIsPending()
    {
        var directory = Directory.CreateTempSubdirectory("code-companion-client-trust-");
        try
        {
            var store = new ClientTrustStore(Path.Combine(directory.FullName, "client-trust.json"));
            store.Save(new ClientTrustSnapshot
            {
                Clients =
                [
                    new ClientTrustRecord
                    {
                        ClientId = "already-approved",
                        Authorization = ClientTrustStore.Allowed,
                        LastSeenUtc = DateTimeOffset.Parse("2026-05-14T09:00:00Z")
                    }
                ]
            });

            Assert.False(store.TrySetMostRecentPendingAuthorization(ClientTrustStore.Allowed, out var updatedClient));
            Assert.Null(updatedClient);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
