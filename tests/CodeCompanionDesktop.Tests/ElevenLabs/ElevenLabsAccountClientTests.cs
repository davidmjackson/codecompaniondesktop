using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeCompanionDesktop.ElevenLabs;

namespace CodeCompanionDesktop.Tests.ElevenLabs;

public sealed class ElevenLabsAccountClientTests
{
    [Fact]
    public void ParseExtractsExpectedFields()
    {
        const string json = """
            {
              "tier": "creator",
              "character_count": 12345,
              "character_limit": 100000,
              "next_character_count_reset_unix": 1700000000,
              "extra_field": "ignored"
            }
            """;

        var subscription = ElevenLabsAccountClient.Parse(json);

        Assert.Equal(12345, subscription.CharacterCount);
        Assert.Equal(100000, subscription.CharacterLimit);
        Assert.Equal(1700000000, subscription.NextCharacterCountResetUnix);
        Assert.Equal("creator", subscription.Tier);
    }

    [Fact]
    public void ParseToleratesMissingFields()
    {
        var subscription = ElevenLabsAccountClient.Parse("{}");

        Assert.Equal(0, subscription.CharacterCount);
        Assert.Equal(0, subscription.CharacterLimit);
        Assert.Equal(0, subscription.NextCharacterCountResetUnix);
        Assert.Equal(string.Empty, subscription.Tier);
    }

    [Fact]
    public void ParseToleratesWrongTypes()
    {
        const string json = """
            {
              "tier": null,
              "character_count": "not-a-number"
            }
            """;

        var subscription = ElevenLabsAccountClient.Parse(json);

        Assert.Equal(0, subscription.CharacterCount);
        Assert.Equal(string.Empty, subscription.Tier);
    }

    [Fact]
    public async Task GetSubscriptionAsyncSendsApiKeyHeaderAndCorrectPath()
    {
        string? capturedHeader = null;
        Uri? capturedUri = null;
        HttpMethod? capturedMethod = null;

        var handler = new StubHandler((request, _) =>
        {
            capturedMethod = request.Method;
            capturedUri = request.RequestUri;
            capturedHeader = request.Headers.GetValues("xi-api-key").Single();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"character_count":1,"character_limit":2,"next_character_count_reset_unix":3,"tier":"free"}"""),
            });
        });

        var account = new ElevenLabsAccountClient(new HttpClient(handler));

        var subscription = await account.GetSubscriptionAsync("test-key");

        Assert.Equal(HttpMethod.Get, capturedMethod);
        Assert.NotNull(capturedUri);
        Assert.Equal("/v1/user/subscription", capturedUri!.AbsolutePath);
        Assert.Equal("test-key", capturedHeader);
        Assert.Equal(1, subscription.CharacterCount);
        Assert.Equal(2, subscription.CharacterLimit);
        Assert.Equal(3, subscription.NextCharacterCountResetUnix);
        Assert.Equal("free", subscription.Tier);
    }

    [Fact]
    public async Task GetSubscriptionAsyncThrowsOnUnauthorized()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"detail":"invalid key"}"""),
            }));

        var account = new ElevenLabsAccountClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            account.GetSubscriptionAsync("bad-key"));

        Assert.Contains("401", ex.Message);
        Assert.Contains("invalid key", ex.Message);
    }

    [Fact]
    public async Task GetSubscriptionAsyncRejectsBlankApiKey()
    {
        var account = new ElevenLabsAccountClient(new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))));

        await Assert.ThrowsAsync<ArgumentException>(() => account.GetSubscriptionAsync("   "));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            this.handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }
}
