using System;
using System.IO;
using System.Text.Json;

namespace CodeCompanionDesktop.History;

public sealed class SpeechHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string path;

    public SpeechHistoryStore()
        : this(CreateDefaultPath())
    {
    }

    public SpeechHistoryStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        this.path = path;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public SpeechHistorySnapshot Load()
    {
        if (!File.Exists(path))
        {
            return new SpeechHistorySnapshot();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SpeechHistorySnapshot>(json) ?? new SpeechHistorySnapshot();
        }
        catch
        {
            return new SpeechHistorySnapshot();
        }
    }

    public void Save(SpeechHistorySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static string CreateDefaultPath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodeCompanionDesktop");
        return Path.Combine(directory, "speech-history.json");
    }
}

public sealed class SpeechHistorySnapshot
{
    public List<string> RecentBridgeClients { get; set; } = [];

    public List<string> RecentSpeechResults { get; set; } = [];
}
