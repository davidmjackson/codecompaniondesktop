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

    public async Task<IReadOnlyList<UsageDay>> GetDailyUsageAsync(
        string apiKey,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        // Milliseconds, not seconds. Seconds return HTTP 200 with an empty usage
        // object, which would silently report no usage at all.
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

        return ParseDailyUsage(body);
    }

    /// <summary>
    /// Pairs the "time" array with the usage series, element by element.
    /// Shares SumUsage's never-throw contract: a malformed body yields an empty
    /// list, because losing the chart must not break the surrounding refresh.
    /// </summary>
    public static IReadOnlyList<UsageDay> ParseDailyUsage(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<UsageDay>();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("time", out var time) ||
                time.ValueKind != JsonValueKind.Array ||
                !root.TryGetProperty("usage", out var usage) ||
                usage.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<UsageDay>();
            }

            var series = SelectSeries(usage);
            return series is null ? Array.Empty<UsageDay>() : PairDays(time, series.Value);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
        {
            // Same three failure types as SumUsage. See that method for why the
            // category is caught rather than each individual site.
            return Array.Empty<UsageDay>();
        }
    }

    private static JsonElement? SelectSeries(JsonElement usage)
    {
        // "All" is the aggregate series. Any other single series is a breakdown.
        if (usage.TryGetProperty("All", out var all) && all.ValueKind == JsonValueKind.Array)
        {
            return all;
        }

        foreach (var candidate in usage.EnumerateObject())
        {
            if (candidate.Value.ValueKind == JsonValueKind.Array)
            {
                return candidate.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the shorter of the two arrays. A length mismatch must lose days, not
    /// throw: the series is a nicety and the refresh around it is not.
    /// </summary>
    private static List<UsageDay> PairDays(JsonElement time, JsonElement series)
    {
        var count = Math.Min(time.GetArrayLength(), series.GetArrayLength());
        var days = new List<UsageDay>(count);

        for (var index = 0; index < count; index++)
        {
            var stamp = time[index];
            if (stamp.ValueKind != JsonValueKind.Number || !stamp.TryGetInt64(out var milliseconds))
            {
                continue;
            }

            var date = DateOnly.FromDateTime(
                DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime);
            days.Add(new UsageDay(date, ReadCharacters(series[index])));
        }

        return days;
    }

    private static long ReadCharacters(JsonElement entry)
    {
        if (entry.ValueKind == JsonValueKind.Number &&
            entry.TryGetDouble(out var value) &&
            double.IsFinite(value) &&
            value > 0)
        {
            return (long)Math.Round(value);
        }

        return 0;
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
            var root = document.RootElement;

            // TryGetProperty throws InvalidOperationException (not JsonException)
            // when the element is not an object, so guard the kind first.
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("usage", out var usage) ||
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
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
        {
            // Best-effort by contract: this must never throw, whatever the body
            // contains. System.Text.Json signals failure with three different types
            // here — JsonException (not JSON), InvalidOperationException (wrong
            // element kind), and ArgumentException (Parse transcoding an already
            // ill-formed UTF-16 string). Catching the category rather than each site
            // stops this becoming whack-a-mole. It stays a filter rather than a bare
            // catch so genuine faults like OutOfMemoryException still propagate.
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
                double.IsFinite(value) &&
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
