using System;
using System.Net;
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

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new ElevenLabsAccountAccessDeniedException(ExtractProviderMessage(body));
        }

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

    /// <summary>
    /// Pulls ElevenLabs' own error text out of a `detail.message` body. Their
    /// message names the exact missing scope, so it beats anything we invent and
    /// does not rot if they rename a scope. Must never throw.
    /// </summary>
    internal static string ExtractProviderMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "ElevenLabs denied access to account information for this API key.";
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            // TryGetProperty throws InvalidOperationException (not JsonException)
            // when the root is valid JSON but not an object — a bare null, number,
            // bool, array or string, which a gateway can return on a 401.
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("detail", out var detail) &&
                detail.ValueKind == JsonValueKind.Object &&
                detail.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                var text = message.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text!;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
        {
            // Best-effort by contract: this turns an untrusted body into a display
            // string and must never throw, whatever the body contains. System.Text.Json
            // signals failure with three different types here — JsonException (not
            // JSON), InvalidOperationException (wrong element kind, or a value that
            // will not decode), and ArgumentException (Parse transcoding a body that
            // is already ill-formed UTF-16). Catching the category rather than each
            // site stops this becoming whack-a-mole. It stays a filter rather than a
            // bare catch so genuine faults like OutOfMemoryException still propagate.
            // Falls through to the shared TrimError return below.
        }

        return TrimError(body);
    }
}
