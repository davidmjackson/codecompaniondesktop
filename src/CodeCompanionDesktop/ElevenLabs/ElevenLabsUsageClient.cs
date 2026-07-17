using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CodeCompanionDesktop.ElevenLabs;

/// <summary>
/// Reads character usage from /v1/usage/character-stats.
///
/// This endpoint needs no scope beyond the text-to-speech key, so it works when
/// /v1/user/subscription is refused for lacking user_read. It reports usage only:
/// there is no limit, tier, or reset date here, and no other endpoint supplies
/// them (every workspace-level quota path returns 404).
/// </summary>
public sealed class ElevenLabsUsageClient
{
    private static readonly Uri DefaultBaseAddress = new("https://api.elevenlabs.io");

    private readonly HttpClient httpClient;

    public ElevenLabsUsageClient()
        : this(CreateDefaultHttpClient())
    {
    }

    public ElevenLabsUsageClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        if (httpClient.BaseAddress is null)
        {
            httpClient.BaseAddress = DefaultBaseAddress;
        }

        this.httpClient = httpClient;
    }

    public async Task<long> GetCharactersUsedAsync(
        string apiKey,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        // Milliseconds, not seconds. Seconds return HTTP 200 with an empty usage
        // object, which would silently report zero characters used.
        var start = startUtc.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var end = endUtc.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/v1/usage/character-stats?start_unix={start}&end_unix={end}");
        request.Headers.Add("xi-api-key", apiKey);

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"ElevenLabs usage request failed ({(int)response.StatusCode} {response.ReasonPhrase}).");
        }

        return SumUsage(body);
    }

    /// <summary>
    /// Sums the usage series. Shape: {"time":[ms,...],"usage":{"All":[chars,...]}}.
    /// Must never throw: a usage figure is a nicety, and losing it must not break
    /// the surrounding refresh.
    /// </summary>
    public static long SumUsage(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return 0;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("usage", out var usage) ||
                usage.ValueKind != JsonValueKind.Object)
            {
                return 0;
            }

            // "All" is the aggregate series. Summing every series double counts.
            if (usage.TryGetProperty("All", out var all) && all.ValueKind == JsonValueKind.Array)
            {
                return SumArray(all);
            }

            foreach (var series in usage.EnumerateObject())
            {
                if (series.Value.ValueKind == JsonValueKind.Array)
                {
                    return SumArray(series.Value);
                }
            }

            return 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static long SumArray(JsonElement array)
    {
        long total = 0;
        foreach (var entry in array.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.Number &&
                entry.TryGetDouble(out var value) &&
                value > 0)
            {
                total += (long)Math.Round(value);
            }
        }

        return total;
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        return new HttpClient { BaseAddress = DefaultBaseAddress };
    }
}
