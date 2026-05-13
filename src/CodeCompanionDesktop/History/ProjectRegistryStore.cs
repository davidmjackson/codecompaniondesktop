using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CodeCompanionDesktop.History;

public sealed class ProjectRegistryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string path;

    public ProjectRegistryStore()
        : this(CreateDefaultPath())
    {
    }

    public ProjectRegistryStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        this.path = path;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public ProjectRegistrySnapshot Load()
    {
        if (!File.Exists(path))
        {
            return new ProjectRegistrySnapshot();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ProjectRegistrySnapshot>(json) ?? new ProjectRegistrySnapshot();
        }
        catch
        {
            return new ProjectRegistrySnapshot();
        }
    }

    public ProjectRegistryRecord RecordObservation(
        string projectId,
        string displayName,
        IEnumerable<string> roots,
        string environment,
        string clientName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        var snapshot = Load();
        var now = DateTimeOffset.UtcNow;
        var normalizedProjectId = projectId.Trim();
        var record = snapshot.Projects.FirstOrDefault(
            project => string.Equals(project.ProjectId, normalizedProjectId, StringComparison.OrdinalIgnoreCase));

        if (record is null)
        {
            record = new ProjectRegistryRecord
            {
                ProjectId = normalizedProjectId,
                FirstSeenUtc = now
            };
            snapshot.Projects.Add(record);
        }

        record.DisplayName = displayName.Trim();
        record.LastSeenUtc = now;
        AddDistinct(record.ObservedRoots, roots);
        AddDistinct(record.Environments, [environment]);
        AddDistinct(record.ClientNames, [clientName]);

        snapshot.Projects = snapshot.Projects
            .OrderByDescending(project => project.LastSeenUtc)
            .ThenBy(project => project.ProjectId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Save(snapshot);

        return record;
    }

    public IReadOnlyList<string> LoadRecentSummaries(int maxCount)
    {
        return LoadRecentRecords(maxCount)
            .Take(maxCount)
            .Select(FormatSummary)
            .ToList();
    }

    public IReadOnlyList<ProjectRegistryRecord> LoadRecentRecords(int maxCount)
    {
        return Load()
            .Projects
            .OrderByDescending(project => project.LastSeenUtc)
            .ThenBy(project => project.ProjectId, StringComparer.OrdinalIgnoreCase)
            .Take(maxCount)
            .ToList();
    }

    public void Save(ProjectRegistrySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static string FormatSummary(ProjectRegistryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var roots = record.ObservedRoots.Count == 0
            ? "no roots"
            : string.Join(", ", record.ObservedRoots.Take(2));
        if (record.ObservedRoots.Count > 2)
        {
            roots = $"{roots}, +{record.ObservedRoots.Count - 2} more";
        }

        var environments = record.Environments.Count == 0
            ? "unknown environment"
            : string.Join("/", record.Environments);

        return $"{record.DisplayName} ({record.ProjectId}) via {environments}; roots: {roots}";
    }

    public static string FormatDetails(ProjectRegistryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var environments = record.Environments.Count == 0
            ? "unknown"
            : string.Join(", ", record.Environments);
        var clients = record.ClientNames.Count == 0
            ? "unknown"
            : string.Join(", ", record.ClientNames);
        var roots = record.ObservedRoots.Count == 0
            ? "  - none"
            : string.Join(Environment.NewLine, record.ObservedRoots.Select(root => $"  - {root}"));

        return string.Join(
            Environment.NewLine,
            $"{record.DisplayName} ({record.ProjectId})",
            $"Environments: {environments}",
            $"Clients: {clients}",
            $"First seen UTC: {FormatTimestamp(record.FirstSeenUtc)}",
            $"Last seen UTC: {FormatTimestamp(record.LastSeenUtc)}",
            "Observed roots:",
            roots);
    }

    private static void AddDistinct(List<string> target, IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var trimmed = value.Trim();
            if (!target.Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                target.Add(trimmed);
            }
        }
    }

    private static string CreateDefaultPath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodeCompanionDesktop");
        return Path.Combine(directory, "project-registry.json");
    }

    private static string FormatTimestamp(DateTimeOffset value)
    {
        return value == default
            ? "unknown"
            : value.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

public sealed class ProjectRegistrySnapshot
{
    public List<ProjectRegistryRecord> Projects { get; set; } = [];
}

public sealed class ProjectRegistryRecord
{
    public string ProjectId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public List<string> ObservedRoots { get; set; } = [];

    public List<string> Environments { get; set; } = [];

    public List<string> ClientNames { get; set; } = [];

    public DateTimeOffset FirstSeenUtc { get; set; }

    public DateTimeOffset LastSeenUtc { get; set; }
}
