using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CodeCompanionDesktop.Settings;

namespace CodeCompanionDesktop.ElevenLabs;

public sealed class ElevenLabsTextToSpeechClient
{
    private const string TestPhrase = "Code Companion desktop speech test.";

    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri("https://api.elevenlabs.io")
    };

    public async Task<string> CreateTestSpeechAsync(string apiKey)
    {
        return await CreateSpeechAsync(apiKey, TestPhrase);
    }

    public async Task<string> CreateSpeechAsync(string apiKey, string text)
    {
        return await CreateSpeechAsync(
            apiKey,
            text,
            new ElevenLabsSpeechOptions(
                AppSettings.DefaultElevenLabsVoiceId,
                AppSettings.DefaultElevenLabsModelId,
                AppSettings.DefaultElevenLabsOutputFormat));
    }

    public async Task<string> CreateSpeechAsync(string apiKey, string text, ElevenLabsSpeechOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.VoiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ModelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputFormat);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/text-to-speech/{Uri.EscapeDataString(options.VoiceId)}?output_format={Uri.EscapeDataString(options.OutputFormat)}");
        request.Headers.Add("xi-api-key", apiKey);
        request.Content = JsonContent.Create(new TextToSpeechRequest(text, options.ModelId));

        using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"ElevenLabs request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {TrimError(errorBody)}");
        }

        var path = CreateOutputPath();
        await using var audioStream = await response.Content.ReadAsStreamAsync();
        await using var outputStream = File.Create(path);
        await audioStream.CopyToAsync(outputStream);

        return path;
    }

    private static string CreateOutputPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CodeCompanionDesktop");
        Directory.CreateDirectory(directory);

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        return Path.Combine(directory, $"elevenlabs-test-{timestamp}.mp3");
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

    private sealed record TextToSpeechRequest(
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("model_id")] string ModelId);
}

public sealed record ElevenLabsSpeechOptions(
    string VoiceId,
    string ModelId,
    string OutputFormat);
