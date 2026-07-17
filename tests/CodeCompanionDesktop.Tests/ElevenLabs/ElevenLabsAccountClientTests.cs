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

    [Theory]
    [InlineData("null")]
    [InlineData("5")]
    [InlineData("true")]
    [InlineData("[1,2,3]")]
    [InlineData("\"just a string\"")]
    public void ParseToleratesNonObjectJsonRoot(string json)
    {
        var subscription = ElevenLabsAccountClient.Parse(json);

        Assert.Equal(0, subscription.CharacterCount);
        Assert.Equal(0, subscription.CharacterLimit);
        Assert.Equal(0, subscription.NextCharacterCountResetUnix);
        Assert.Equal(string.Empty, subscription.Tier);
    }

    [Fact]
    public void ExtractProviderMessageToleratesIllFormedUtf16()
    {
        // A real unpaired surrogate CHAR (not a \u escape): JsonDocument.Parse throws
        // ArgumentException transcoding it to UTF-8. Called directly, because
        // StringContent would sanitise this to U+FFFD before the client ever saw it.
        var body = "{\"detail\":{\"message\":\"x\ud800y\"}}";

        var message = ElevenLabsAccountClient.ExtractProviderMessage(body);

        Assert.False(string.IsNullOrWhiteSpace(message));
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
    public async Task GetSubscriptionAsyncThrowsAccessDeniedOnUnauthorized()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(
                    """{"detail":{"type":"authentication_error","code":"unauthorized","message":"The API key you used is missing the permission user_read to execute this operation."}}"""),
            }));

        var account = new ElevenLabsAccountClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<ElevenLabsAccountAccessDeniedException>(() =>
            account.GetSubscriptionAsync("tts-only-key"));

        Assert.Contains("user_read", ex.Message);
    }

    [Fact]
    public async Task GetSubscriptionAsyncThrowsAccessDeniedOnForbidden()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("""{"detail":{"message":"forbidden here"}}"""),
            }));

        var account = new ElevenLabsAccountClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<ElevenLabsAccountAccessDeniedException>(() =>
            account.GetSubscriptionAsync("key"));

        Assert.Equal("forbidden here", ex.Message);
    }

    [Fact]
    public async Task GetSubscriptionAsyncAccessDeniedToleratesUnparseableBody()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("<html>gateway blew up</html>"),
            }));

        var account = new ElevenLabsAccountClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<ElevenLabsAccountAccessDeniedException>(() =>
            account.GetSubscriptionAsync("key"));

        Assert.Contains("gateway blew up", ex.Message);
    }

    [Fact]
    public async Task GetSubscriptionAsyncAccessDeniedToleratesEmptyBody()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(string.Empty),
            }));

        var account = new ElevenLabsAccountClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<ElevenLabsAccountAccessDeniedException>(() =>
            account.GetSubscriptionAsync("key"));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public async Task GetSubscriptionAsyncAccessDeniedToleratesUnpairedSurrogateInMessage()
    {
        // A lone high surrogate is valid JSON syntax but will not decode to a
        // string. It must fall back, not escape as InvalidOperationException.
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"detail":{"message":"\ud800"}}"""),
            }));

        var account = new ElevenLabsAccountClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<ElevenLabsAccountAccessDeniedException>(() =>
            account.GetSubscriptionAsync("key"));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public async Task GetSubscriptionAsyncStillThrowsInvalidOperationOnServerError()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"detail":{"message":"boom"}}"""),
            }));

        var account = new ElevenLabsAccountClient(new HttpClient(handler));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            account.GetSubscriptionAsync("key"));
    }

    [Fact]
    public async Task GetSubscriptionAsyncRejectsBlankApiKey()
    {
        var account = new ElevenLabsAccountClient(new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))));

        await Assert.ThrowsAsync<ArgumentException>(() => account.GetSubscriptionAsync("   "));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("5")]
    [InlineData("true")]
    [InlineData("[1,2,3]")]
    [InlineData("\"just a string\"")]
    public async Task GetSubscriptionAsyncAccessDeniedToleratesNonObjectJsonRoot(string body)
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(body),
            }));

        var account = new ElevenLabsAccountClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<ElevenLabsAccountAccessDeniedException>(() =>
            account.GetSubscriptionAsync("key"));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
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
