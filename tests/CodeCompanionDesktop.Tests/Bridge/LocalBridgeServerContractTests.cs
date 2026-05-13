using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using CodeCompanionDesktop.Bridge;

namespace CodeCompanionDesktop.Tests.Bridge;

public sealed class LocalBridgeServerContractTests
{
    private const string Token = "test-token";

    [Fact]
    public async Task HealthIncludesVersionAndQueueState()
    {
        using var fixture = BridgeFixture.Start();

        using var response = await fixture.Client.GetAsync("health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("listening", root.GetProperty("bridge").GetString());
        Assert.Equal("0.2.0", root.GetProperty("version").GetString());
        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.False(root.GetProperty("speaking").GetBoolean());
        Assert.False(root.GetProperty("queueEnabled").GetBoolean());
        Assert.Equal(0, root.GetProperty("queued").GetInt32());
        Assert.Equal(3, root.GetProperty("queueLimit").GetInt32());
    }

    [Fact]
    public async Task ClientHelloAcceptsClientAndWorkspaceMetadata()
    {
        using var fixture = BridgeFixture.Start();

        using var response = await fixture.Client.PostAsync(
            "v1/client/hello",
            JsonContent("""
                {
                  "schemaVersion": 1,
                  "client": {
                    "clientId": "test-client",
                    "name": "Code Companion Voice",
                    "version": "0.0.0",
                    "host": "windows",
                    "environment": "windows"
                  },
                  "workspace": {
                    "projectId": "codecompaniondesktop",
                    "displayName": "Code Companion Desktop",
                    "roots": ["D:\\Development\\CodeCompanionDesktop"]
                  }
                }
                """));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("allowed", root.GetProperty("authorization").GetString());
        Assert.Equal("compatibility-token", root.GetProperty("mode").GetString());
        Assert.Equal("0.2.0", root.GetProperty("bridgeVersion").GetString());
        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Contains("Code Companion Voice", fixture.RuntimeState.LastClient, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClientHelloRecordsPendingClientWhenTrustStoreIsEnabled()
    {
        using var fixture = BridgeFixture.StartWithClientTrust();

        using var response = await fixture.Client.PostAsync(
            "v1/client/hello",
            JsonContent(ValidClientHelloJson()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;
        Assert.Equal("pending", root.GetProperty("authorization").GetString());
        Assert.Equal("desktop-pairing", root.GetProperty("mode").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("sessionToken").ValueKind);

        var client = Assert.Single(fixture.ClientTrustStore!.Load().Clients);
        Assert.Equal("test-client", client.ClientId);
        Assert.Equal("pending", client.Authorization);
        Assert.Equal("codecompaniondesktop", client.ProjectId);
    }

    [Fact]
    public async Task ClientHelloReturnsAllowedForTrustedClient()
    {
        using var fixture = BridgeFixture.StartWithClientTrust();
        fixture.ClientTrustStore!.Save(new ClientTrustSnapshot
        {
            Clients =
            [
                new ClientTrustRecord
                {
                    ClientId = "test-client",
                    Authorization = ClientTrustStore.Allowed,
                    FirstSeenUtc = DateTimeOffset.UtcNow,
                    LastSeenUtc = DateTimeOffset.UtcNow
                }
            ]
        });

        using var response = await fixture.Client.PostAsync(
            "v1/client/hello",
            JsonContent(ValidClientHelloJson()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;
        Assert.Equal("allowed", root.GetProperty("authorization").GetString());
        Assert.Equal("desktop-pairing", root.GetProperty("mode").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("sessionToken").GetString()));
        Assert.True(DateTimeOffset.TryParse(root.GetProperty("sessionExpiresAtUtc").GetString(), out _));
    }

    [Fact]
    public async Task SpeechCandidateAcceptsApprovedClientSessionToken()
    {
        using var fixture = BridgeFixture.StartWithClientTrust();
        fixture.ClientTrustStore!.Save(new ClientTrustSnapshot
        {
            Clients =
            [
                new ClientTrustRecord
                {
                    ClientId = "test-client",
                    Authorization = ClientTrustStore.Allowed,
                    FirstSeenUtc = DateTimeOffset.UtcNow,
                    LastSeenUtc = DateTimeOffset.UtcNow
                }
            ]
        });

        using var helloResponse = await fixture.Client.PostAsync(
            "v1/client/hello",
            JsonContent(ValidClientHelloJson()));
        using var helloDocument = await ReadJsonAsync(helloResponse);
        var sessionToken = helloDocument.RootElement.GetProperty("sessionToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(sessionToken));

        fixture.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
        using var response = await fixture.Client.PostAsync(
            "v1/speech/candidates",
            JsonContent(ValidSpeechCandidateJson()));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        Assert.Equal("spoken", document.RootElement.GetProperty("decision").GetString());
        Assert.Equal(["The bridge contract test candidate is ready."], fixture.SpokenTexts);
    }

    [Fact]
    public async Task SpeechCandidateRejectsSessionTokenForDifferentClient()
    {
        using var fixture = BridgeFixture.StartWithClientTrust();
        fixture.ClientTrustStore!.Save(new ClientTrustSnapshot
        {
            Clients =
            [
                new ClientTrustRecord
                {
                    ClientId = "trusted-client",
                    Authorization = ClientTrustStore.Allowed,
                    FirstSeenUtc = DateTimeOffset.UtcNow,
                    LastSeenUtc = DateTimeOffset.UtcNow
                }
            ]
        });

        using var helloResponse = await fixture.Client.PostAsync(
            "v1/client/hello",
            JsonContent(ValidClientHelloJson("trusted-client")));
        using var helloDocument = await ReadJsonAsync(helloResponse);
        var sessionToken = helloDocument.RootElement.GetProperty("sessionToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(sessionToken));

        fixture.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
        using var response = await fixture.Client.PostAsync(
            "v1/speech/candidates",
            JsonContent(ValidSpeechCandidateJson(clientId: "different-client")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertErrorAsync(response, "unauthorized");
        Assert.Empty(fixture.SpokenTexts);
    }

    [Fact]
    public async Task ClientHelloRejectsUnsupportedSchemaVersion()
    {
        using var fixture = BridgeFixture.Start();

        using var response = await fixture.Client.PostAsync(
            "v1/client/hello",
            JsonContent("""
                {
                  "schemaVersion": 99,
                  "client": {
                    "clientId": "test-client",
                    "name": "Code Companion Voice",
                    "version": "0.0.0",
                    "host": "windows",
                    "environment": "windows"
                  },
                  "workspace": {
                    "projectId": "codecompaniondesktop",
                    "displayName": "Code Companion Desktop",
                    "roots": ["D:\\Development\\CodeCompanionDesktop"]
                  }
                }
                """));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorAsync(response, "unsupported_schema_version");
    }

    [Fact]
    public async Task SpeechCandidateRequiresBearerToken()
    {
        using var fixture = BridgeFixture.Start();

        using var response = await fixture.Client.PostAsync(
            "v1/speech/candidates",
            JsonContent(ValidSpeechCandidateJson()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertErrorAsync(response, "unauthorized");
    }

    [Fact]
    public async Task SpeechCandidateRejectsInvalidMetadata()
    {
        using var fixture = BridgeFixture.Start();
        fixture.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await fixture.Client.PostAsync(
            "v1/speech/candidates",
            JsonContent("""
                {
                  "schemaVersion": 1,
                  "client": {
                    "clientId": "test-client",
                    "name": "Code Companion Voice",
                    "version": "0.0.0",
                    "host": "windows",
                    "environment": "windows"
                  },
                  "workspace": {
                    "projectId": "codecompaniondesktop",
                    "displayName": "Code Companion Desktop",
                    "roots": ["D:\\Development\\CodeCompanionDesktop"]
                  },
                  "candidate": {
                    "kind": "assistant-message",
                    "phase": "final",
                    "text": "This candidate is missing Codex metadata.",
                    "source": "codex-jsonl"
                  }
                }
                """));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorAsync(response, "invalid_codex_metadata");
    }

    [Fact]
    public async Task SpeechCandidateSpeaksValidPayload()
    {
        using var fixture = BridgeFixture.Start();
        fixture.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await fixture.Client.PostAsync(
            "v1/speech/candidates",
            JsonContent(ValidSpeechCandidateJson()));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;
        Assert.Equal("accepted", root.GetProperty("status").GetString());
        Assert.Equal("spoken", root.GetProperty("decision").GetString());
        Assert.Equal("accepted", root.GetProperty("reason").GetString());
        Assert.Equal(0, root.GetProperty("queuePosition").GetInt32());
        Assert.Contains("message-1", fixture.RuntimeState.LastSpeechCandidate, StringComparison.Ordinal);
        Assert.Equal(["The bridge contract test candidate is ready."], fixture.SpokenTexts);
    }

    [Fact]
    public async Task SpeechCandidateSpeaksVoiceCheckInWhenPhaseIsNotFinal()
    {
        using var fixture = BridgeFixture.Start();
        fixture.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await fixture.Client.PostAsync(
            "v1/speech/candidates",
            JsonContent(ValidSpeechCandidateJson(
                messageId: "message-voice-check",
                text: "The voice check candidate is ready.",
                phase: "commentary",
                speechHint: "voice-check-in")));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;
        Assert.Equal("spoken", root.GetProperty("decision").GetString());
        Assert.Equal("voice-check-in", root.GetProperty("reason").GetString());
        Assert.Equal(["The voice check candidate is ready."], fixture.SpokenTexts);
    }

    [Fact]
    public async Task SpeechCandidateIgnoresNonFinalPayloadWithoutSpeechHint()
    {
        using var fixture = BridgeFixture.Start();
        fixture.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await fixture.Client.PostAsync(
            "v1/speech/candidates",
            JsonContent(ValidSpeechCandidateJson(
                messageId: "message-commentary",
                text: "The commentary candidate should not speak.",
                phase: "commentary")));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;
        Assert.Equal("ignored", root.GetProperty("decision").GetString());
        Assert.Equal("non_final_candidate", root.GetProperty("reason").GetString());
        Assert.Empty(fixture.SpokenTexts);
    }

    [Fact]
    public async Task SpeechCandidateIgnoresDuplicateMessageId()
    {
        using var fixture = BridgeFixture.Start();
        fixture.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var first = await fixture.Client.PostAsync(
            "v1/speech/candidates",
            JsonContent(ValidSpeechCandidateJson()));
        using var second = await fixture.Client.PostAsync(
            "v1/speech/candidates",
            JsonContent(ValidSpeechCandidateJson()));

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);

        using var document = await ReadJsonAsync(second);
        var root = document.RootElement;
        Assert.Equal("duplicate", root.GetProperty("decision").GetString());
        Assert.Equal("duplicate_candidate", root.GetProperty("reason").GetString());
        Assert.Single(fixture.SpokenTexts);
    }

    [Fact]
    public async Task SpeechCandidateIgnoresDuplicateNormalizedText()
    {
        using var fixture = BridgeFixture.Start();
        fixture.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var first = await fixture.Client.PostAsync(
            "v1/speech/candidates",
            JsonContent(ValidSpeechCandidateJson("message-1", "The bridge contract test candidate is ready.")));
        using var second = await fixture.Client.PostAsync(
            "v1/speech/candidates",
            JsonContent(ValidSpeechCandidateJson("message-2", "  the   bridge contract test candidate is ready.  ")));

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);

        using var document = await ReadJsonAsync(second);
        var root = document.RootElement;
        Assert.Equal("duplicate", root.GetProperty("decision").GetString());
        Assert.Equal("duplicate_candidate", root.GetProperty("reason").GetString());
        Assert.Single(fixture.SpokenTexts);
    }

    [Fact]
    public async Task SpeechCandidatePrivacyFiltersBeforeSpeaking()
    {
        using var fixture = BridgeFixture.Start();
        fixture.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await fixture.Client.PostAsync(
            "v1/speech/candidates",
            JsonContent(ValidSpeechCandidateJson(
                "message-privacy",
                "Use Authorization: Bearer abcdefghijklmnop and email david@example.com.")));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;
        Assert.Equal("spoken", root.GetProperty("decision").GetString());
        Assert.Equal("privacy_filtered", root.GetProperty("reason").GetString());
        var spoken = Assert.Single(fixture.SpokenTexts);
        Assert.Contains("[redacted]", spoken, StringComparison.Ordinal);
        Assert.Contains("[redacted email]", spoken, StringComparison.Ordinal);
        Assert.DoesNotContain("abcdefghijklmnop", spoken, StringComparison.Ordinal);
        Assert.DoesNotContain("david@example.com", spoken, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpeechCandidateQueuesWhenQueueIsEnabled()
    {
        using var fixture = BridgeFixture.Start(queueEnabled: true);
        fixture.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await fixture.Client.PostAsync(
            "v1/speech/candidates",
            JsonContent(ValidSpeechCandidateJson()));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;
        Assert.Equal("queued", root.GetProperty("decision").GetString());
        Assert.Equal(1, root.GetProperty("queuePosition").GetInt32());
    }

    private static string ValidSpeechCandidateJson(
        string messageId = "message-1",
        string text = "The bridge contract test candidate is ready.",
        string phase = "final",
        string? speechHint = null,
        string clientId = "test-client")
    {
        var speechHintJson = speechHint is null
            ? ""
            : $"                \"speechHint\": \"{JsonEncodedText.Encode(speechHint)}\",\n";

        return $$"""
            {
              "schemaVersion": 1,
              "client": {
                "clientId": "{{JsonEncodedText.Encode(clientId)}}",
                "name": "Code Companion Voice",
                "version": "0.0.0",
                "host": "windows",
                "environment": "windows"
              },
              "workspace": {
                "projectId": "codecompaniondesktop",
                "displayName": "Code Companion Desktop",
                "roots": ["D:\\Development\\CodeCompanionDesktop"]
              },
              "codex": {
                "sessionId": "session-1",
                "messageId": "{{messageId}}",
                "timestamp": "2026-05-13T00:00:00Z"
              },
              "candidate": {
                "kind": "assistant-message",
                "phase": "{{JsonEncodedText.Encode(phase)}}",
                {{speechHintJson}}
                "text": "{{JsonEncodedText.Encode(text)}}",
                "source": "codex-jsonl"
              }
            }
            """;
    }

    private static string ValidClientHelloJson(string clientId = "test-client")
    {
        return $$"""
            {
              "schemaVersion": 1,
              "client": {
                "clientId": "{{JsonEncodedText.Encode(clientId)}}",
                "name": "Code Companion Voice",
                "version": "0.0.0",
                "host": "windows",
                "environment": "windows"
              },
              "workspace": {
                "projectId": "codecompaniondesktop",
                "displayName": "Code Companion Desktop",
                "roots": ["D:\\Development\\CodeCompanionDesktop"]
              }
            }
            """;
    }

    private static StringContent JsonContent(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, string expectedError)
    {
        using var document = await ReadJsonAsync(response);
        Assert.Equal(expectedError, document.RootElement.GetProperty("error").GetString());
    }

    private sealed class BridgeFixture : IDisposable
    {
        private readonly LocalBridgeServer server;

        private BridgeFixture(
            LocalBridgeServer server,
            BridgeRuntimeState runtimeState,
            ConcurrentQueue<string> spokenTexts,
            ClientTrustStore? clientTrustStore = null,
            string? clientTrustDirectory = null)
        {
            this.server = server;
            RuntimeState = runtimeState;
            SpokenTexts = spokenTexts;
            ClientTrustStore = clientTrustStore;
            ClientTrustDirectory = clientTrustDirectory;
            Client = new HttpClient
            {
                BaseAddress = new Uri(server.LocalBaseUrl)
            };
        }

        public HttpClient Client { get; }

        public BridgeRuntimeState RuntimeState { get; }

        public ConcurrentQueue<string> SpokenTexts { get; }

        public ClientTrustStore? ClientTrustStore { get; }

        private string? ClientTrustDirectory { get; }

        public static BridgeFixture Start(bool queueEnabled = false, Func<string, Task>? speakAsync = null)
        {
            return Start(queueEnabled, speakAsync, null, null);
        }

        public static BridgeFixture StartWithClientTrust()
        {
            var directory = Directory.CreateTempSubdirectory("code-companion-client-trust-");
            var store = new ClientTrustStore(Path.Combine(directory.FullName, "client-trust.json"));
            return Start(queueEnabled: false, speakAsync: null, store, directory.FullName);
        }

        private static BridgeFixture Start(
            bool queueEnabled,
            Func<string, Task>? speakAsync,
            ClientTrustStore? clientTrustStore,
            string? clientTrustDirectory)
        {
            var runtimeState = new BridgeRuntimeState();
            runtimeState.ConfigureQueue(queueEnabled, 3);
            var spokenTexts = new ConcurrentQueue<string>();
            var captureSpeechAsync = speakAsync ?? (text =>
            {
                spokenTexts.Enqueue(text);
                return Task.CompletedTask;
            });
            var queue = new BridgeSpeechQueue(captureSpeechAsync, runtimeState);
            var server = new LocalBridgeServer(
                Token,
                captureSpeechAsync,
                runtimeState,
                queue,
                new SpeechCandidatePipeline(),
                clientTrustStore: clientTrustStore,
                port: 0);
            server.Start();

            return new BridgeFixture(server, runtimeState, spokenTexts, clientTrustStore, clientTrustDirectory);
        }

        public void Dispose()
        {
            Client.Dispose();
            server.Dispose();
            if (ClientTrustDirectory is not null && Directory.Exists(ClientTrustDirectory))
            {
                Directory.Delete(ClientTrustDirectory, recursive: true);
            }
        }
    }
}
