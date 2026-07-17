using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeCompanionDesktop.ElevenLabs;

namespace CodeCompanionDesktop.Tests.ElevenLabs;

public sealed class ElevenLabsUsageClientTests
{
    [Fact]
    public void SumUsageAddsAllSeriesBuckets()
    {
        const string json = """{"time":[1,2,3],"usage":{"All":[1596.0,14679.0,11306.0]}}""";

        Assert.Equal(27581L, ElevenLabsUsageClient.SumUsage(json));
    }

    [Fact]
    public void SumUsageReturnsZeroForEmptyUsageObject()
    {
        // This is the shape returned when the window is sent in seconds:
        // HTTP 200 with no usage. It must read as zero, not throw.
        Assert.Equal(0L, ElevenLabsUsageClient.SumUsage("""{"time":[1728000000],"usage":{}}"""));
    }

    [Fact]
    public void SumUsageToleratesMissingUsageProperty()
    {
        Assert.Equal(0L, ElevenLabsUsageClient.SumUsage("""{"time":[1,2]}"""));
    }

    [Fact]
    public void SumUsageToleratesNonNumericAndNegativeEntries()
    {
        const string json = """{"usage":{"All":[100.0,"nope",null,-5.0,25.0]}}""";

        Assert.Equal(125L, ElevenLabsUsageClient.SumUsage(json));
    }

    [Fact]
    public void SumUsageToleratesUnparseableJson()
    {
        Assert.Equal(0L, ElevenLabsUsageClient.SumUsage("<html>nope</html>"));
    }

    [Fact]
    public void SumUsagePrefersAllSeriesOverPerBreakdownSeries()
    {
        // "All" is the aggregate. Summing every series would double count.
        const string json = """{"usage":{"All":[100.0],"voice-a":[60.0],"voice-b":[40.0]}}""";

        Assert.Equal(100L, ElevenLabsUsageClient.SumUsage(json));
    }

    [Fact]
    public async Task GetCharactersUsedAsyncSendsMillisecondWindowAndApiKey()
    {
        // The window MUST be milliseconds. Seconds return 200 with empty usage,
        // so this assertion is what stops a silent zero-usage bug.
        Uri? capturedUri = null;
        string? capturedHeader = null;

        var handler = new StubHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            capturedHeader = request.Headers.GetValues("xi-api-key").Single();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"time":[1],"usage":{"All":[7.0]}}"""),
            });
        });

        var client = new ElevenLabsUsageClient(new HttpClient(handler));

        var start = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var end = DateTimeOffset.FromUnixTimeSeconds(1_700_086_400);

        var used = await client.GetCharactersUsedAsync("test-key", start, end);

        Assert.Equal(7L, used);
        Assert.NotNull(capturedUri);
        Assert.Equal("/v1/usage/character-stats", capturedUri!.AbsolutePath);
        Assert.Equal("test-key", capturedHeader);

        // Milliseconds, not seconds. 1700000000 seconds -> 1700000000000 ms.
        Assert.Contains("start_unix=1700000000000", capturedUri.Query);
        Assert.Contains("end_unix=1700086400000", capturedUri.Query);
    }

    [Fact]
    public async Task GetCharactersUsedAsyncThrowsOnFailureStatus()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("boom"),
            }));

        var client = new ElevenLabsUsageClient(new HttpClient(handler));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetCharactersUsedAsync("key", DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task GetCharactersUsedAsyncRejectsBlankApiKey()
    {
        var client = new ElevenLabsUsageClient(new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.GetCharactersUsedAsync("   ", DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("5")]
    [InlineData("true")]
    [InlineData("[1,2,3]")]
    [InlineData("\"just a string\"")]
    public void SumUsageToleratesNonObjectJsonRoot(string json)
    {
        // TryGetProperty throws InvalidOperationException on a non-object root.
        Assert.Equal(0L, ElevenLabsUsageClient.SumUsage(json));
    }

    [Fact]
    public void SumUsageToleratesIllFormedUtf16()
    {
        // A real unpaired surrogate CHAR (not a \u escape). JsonDocument.Parse
        // throws ArgumentException transcoding it to UTF-8.
        var json = "{\"usage\":{\"All\":[1.0]},\"x\":\"\ud800\"}";

        Assert.Equal(0L, ElevenLabsUsageClient.SumUsage(json));
    }

    [Fact]
    public void SumUsageIgnoresNonFiniteNumbers()
    {
        // 1e400 parses to +Infinity, and (long)Math.Round(Infinity) is long.MinValue,
        // which would turn the sum sharply negative rather than skipping the entry.
        Assert.Equal(3L, ElevenLabsUsageClient.SumUsage("""{"usage":{"All":[1e400,3.0]}}"""));
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
