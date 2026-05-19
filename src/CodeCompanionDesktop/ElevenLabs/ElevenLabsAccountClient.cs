using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CodeCompanionDesktop.ElevenLabs;

public sealed class ElevenLabsAccountClient
{
    private static readonly Uri DefaultBaseAddress = new("https://api.elevenlabs.io");

    private readonly HttpClient httpClient;

    public ElevenLabsAccountClient()
        : this(CreateDefaultHttpClient())
    {
    }

    public ElevenLabsAccountClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        if (httpClient.BaseAddress is null)
        {
            httpClient.BaseAddress = DefaultBaseAddress;
        }

        this.httpClient = httpClient;
    }

    public async Task<ElevenLabsSubscription> GetSubscriptionAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/user/subscription");
        request.Headers.Add("xi-api-key", apiKey);

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"ElevenLabs subscription request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {TrimError(body)}");
        }

        return Parse(body);
    }

    public static ElevenLabsSubscription Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var characterCount = ReadLong(root, "character_count");
        var characterLimit = ReadLong(root, "character_limit");
        var reset = ReadLong(root, "next_character_count_reset_unix");
        var tier = ReadString(root, "tier");

        return new ElevenLabsSubscription(characterCount, characterLimit, reset, tier);
    }

    private static long ReadLong(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return 0;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt64(out var value) => value,
            _ => 0,
        };
    }

    private static string ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return string.Empty;
        }

        return element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : string.Empty;
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        return new HttpClient { BaseAddress = DefaultBaseAddress };
    }

    private static string TrimError(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "no response body";
        }

        return value.Length <= 500
            ? value
            : $"{value[..500]}...";
    }
}
