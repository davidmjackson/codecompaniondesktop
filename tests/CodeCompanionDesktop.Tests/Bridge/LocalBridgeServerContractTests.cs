using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
    public async Task SpeechCandidateAcceptsValidPayloadWithPlaceholderDecision()
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
        Assert.Equal("ignored", root.GetProperty("decision").GetString());
        Assert.Equal("speech_pipeline_not_implemented", root.GetProperty("reason").GetString());
        Assert.Equal(0, root.GetProperty("queuePosition").GetInt32());
        Assert.Contains("message-1", fixture.RuntimeState.LastSpeechCandidate, StringComparison.Ordinal);
    }

    private static string ValidSpeechCandidateJson()
    {
        return """
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
              "codex": {
                "sessionId": "session-1",
                "messageId": "message-1",
                "timestamp": "2026-05-13T00:00:00Z"
              },
              "candidate": {
                "kind": "assistant-message",
                "phase": "final",
                "text": "The bridge contract test candidate is ready.",
                "source": "codex-jsonl"
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

        private BridgeFixture(LocalBridgeServer server, BridgeRuntimeState runtimeState)
        {
            this.server = server;
            RuntimeState = runtimeState;
            Client = new HttpClient
            {
                BaseAddress = new Uri(server.LocalBaseUrl)
            };
        }

        public HttpClient Client { get; }

        public BridgeRuntimeState RuntimeState { get; }

        public static BridgeFixture Start()
        {
            var runtimeState = new BridgeRuntimeState();
            runtimeState.ConfigureQueue(false, 3);
            var queue = new BridgeSpeechQueue(_ => Task.CompletedTask, runtimeState);
            var server = new LocalBridgeServer(Token, _ => Task.CompletedTask, runtimeState, queue, port: 0);
            server.Start();

            return new BridgeFixture(server, runtimeState);
        }

        public void Dispose()
        {
            Client.Dispose();
            server.Dispose();
        }
    }
}
