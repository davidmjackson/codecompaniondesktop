using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CodeCompanionDesktop.Bridge;

public sealed class ClientTrustStore
{
    public const string Pending = "pending";
    public const string Allowed = "allowed";
    public const string Denied = "denied";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string path;

    public ClientTrustStore()
        : this(CreateDefaultPath())
    {
    }

    public ClientTrustStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        this.path = path;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public ClientTrustSnapshot Load()
    {
        if (!File.Exists(path))
        {
            return new ClientTrustSnapshot();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ClientTrustSnapshot>(json) ?? new ClientTrustSnapshot();
        }
        catch
        {
            return new ClientTrustSnapshot();
        }
    }

    public ClientTrustRecord RecordHello(BridgeClient client, BridgeWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(workspace);

        var snapshot = Load();
        var now = DateTimeOffset.UtcNow;
        var record = FindClient(snapshot, client.ClientId);
        if (record is null)
        {
            record = new ClientTrustRecord
            {
                ClientId = client.ClientId.Trim(),
                Authorization = Pending,
                FirstSeenUtc = now
            };
            snapshot.Clients.Add(record);
        }

        record.Name = client.Name.Trim();
        record.Version = client.Version.Trim();
        record.Host = client.Host.Trim();
        record.Environment = client.Environment.Trim();
        record.ProjectId = workspace.ProjectId.Trim();
        record.ProjectDisplayName = workspace.DisplayName.Trim();
        record.LastSeenUtc = now;

        Sort(snapshot);
        Save(snapshot);
        return record;
    }

    public bool TrySetAuthorization(string clientId, string authorization)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        var normalizedAuthorization = NormalizeAuthorization(authorization);
        var snapshot = Load();
        var record = FindClient(snapshot, clientId);
        if (record is null)
        {
            return false;
        }

        record.Authorization = normalizedAuthorization;
        Save(snapshot);
        return true;
    }

    public bool TrySetMostRecentPendingAuthorization(string authorization, out ClientTrustRecord? updatedClient)
    {
        var normalizedAuthorization = NormalizeAuthorization(authorization);
        var snapshot = Load();
        var record = snapshot.Clients
            .Where(client => string.Equals(client.Authorization, Pending, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(client => client.LastSeenUtc)
            .ThenBy(client => client.ClientId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (record is null)
        {
            updatedClient = null;
            return false;
        }

        record.Authorization = normalizedAuthorization;
        updatedClient = record;
        Save(snapshot);
        return true;
    }

    public IReadOnlyList<string> LoadClientDetails(int maxCount)
    {
        return Load()
            .Clients
            .OrderBy(client => AuthorizationRank(client.Authorization))
            .ThenByDescending(client => client.LastSeenUtc)
            .ThenBy(client => client.ClientId, StringComparer.OrdinalIgnoreCase)
            .Take(maxCount)
            .Select(FormatDetails)
            .ToList();
    }

    public void Save(ClientTrustSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static string FormatDetails(ClientTrustRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return string.Join(
            Environment.NewLine,
            $"{record.Name} ({record.ClientId})",
            $"Authorization: {record.Authorization}",
            $"Version: {record.Version}",
            $"Host: {record.Host}",
            $"Environment: {record.Environment}",
            $"Project: {record.ProjectDisplayName} ({record.ProjectId})",
            $"First seen UTC: {FormatTimestamp(record.FirstSeenUtc)}",
            $"Last seen UTC: {FormatTimestamp(record.LastSeenUtc)}");
    }

    private static ClientTrustRecord? FindClient(ClientTrustSnapshot snapshot, string clientId)
    {
        var normalizedClientId = clientId.Trim();
        return snapshot.Clients.FirstOrDefault(
            client => string.Equals(client.ClientId, normalizedClientId, StringComparison.OrdinalIgnoreCase));
    }

    private static void Sort(ClientTrustSnapshot snapshot)
    {
        snapshot.Clients = snapshot.Clients
            .OrderByDescending(client => client.LastSeenUtc)
            .ThenBy(client => client.ClientId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int AuthorizationRank(string authorization)
    {
        return authorization.Trim().ToLowerInvariant() switch
        {
            Pending => 0,
            Allowed => 1,
            Denied => 2,
            _ => 3
        };
    }

    private static string NormalizeAuthorization(string authorization)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorization);

        var normalizedAuthorization = authorization.Trim().ToLowerInvariant();
        if (normalizedAuthorization is not (Pending or Allowed or Denied))
        {
            throw new ArgumentException("Unsupported client authorization.", nameof(authorization));
        }

        return normalizedAuthorization;
    }

    private static string CreateDefaultPath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodeCompanionDesktop");
        return Path.Combine(directory, "client-trust.json");
    }

    private static string FormatTimestamp(DateTimeOffset value)
    {
        return value == default
            ? "unknown"
            : value.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

public sealed class ClientTrustSnapshot
{
    public List<ClientTrustRecord> Clients { get; set; } = [];
}

public sealed class ClientTrustRecord
{
    public string ClientId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public string Environment { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string ProjectDisplayName { get; set; } = string.Empty;

    public string Authorization { get; set; } = ClientTrustStore.Pending;

    public DateTimeOffset FirstSeenUtc { get; set; }

    public DateTimeOffset LastSeenUtc { get; set; }
}
