using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CodeCompanionDesktop.Bridge;

public sealed class SpeechCandidateInboxWatcher : IDisposable
{
    public const string InboxDirectoryName = "candidate-inbox";

    private readonly string inboxDirectory;
    private readonly SpeechCandidateProcessor speechCandidateProcessor;
    private readonly BridgeRuntimeState runtimeState;
    private readonly ConcurrentDictionary<string, byte> pendingPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource cancellation = new();
    private FileSystemWatcher? watcher;

    public SpeechCandidateInboxWatcher(
        string inboxDirectory,
        SpeechCandidateProcessor speechCandidateProcessor,
        BridgeRuntimeState runtimeState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inboxDirectory);
        this.inboxDirectory = inboxDirectory;
        this.speechCandidateProcessor = speechCandidateProcessor;
        this.runtimeState = runtimeState;
    }

    public string InboxDirectory => inboxDirectory;

    public static string GetDefaultInboxDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "CodeCompanionDesktop", InboxDirectoryName);
    }

    public void Start()
    {
        Directory.CreateDirectory(inboxDirectory);

        watcher = new FileSystemWatcher(inboxDirectory, "*.json")
        {
            EnableRaisingEvents = true,
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };
        watcher.Created += (_, e) => QueuePath(e.FullPath);
        watcher.Changed += (_, e) => QueuePath(e.FullPath);
        watcher.Renamed += (_, e) => QueuePath(e.FullPath);

        runtimeState.RecordCandidateInboxStarted(inboxDirectory);
        foreach (var path in Directory.EnumerateFiles(inboxDirectory, "*.json"))
        {
            QueuePath(path);
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        watcher?.Dispose();
        cancellation.Dispose();
    }

    public async Task<SpeechCandidateProcessingResult> ProcessFileAsync(string path)
    {
        var request = await ReadRequestAsync(path, cancellation.Token);
        if (request is null)
        {
            runtimeState.RecordCandidateInboxError($"Invalid candidate inbox file: {Path.GetFileName(path)}.");
            MoveToRejected(path);
            return SpeechCandidateProcessingResult.BadRequest("invalid_candidate_inbox_file");
        }

        var result = await speechCandidateProcessor.ProcessAsync(request, source: "inbox");
        if (result.StatusCode == System.Net.HttpStatusCode.Accepted)
        {
            DeleteQuietly(path);
            return result;
        }

        MoveToRejected(path);
        return result;
    }

    private void QueuePath(string path)
    {
        if (!pendingPaths.TryAdd(path, 0))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250, cancellation.Token);
                await ProcessFileAsync(path);
            }
            catch (OperationCanceledException)
            {
                // Application shutdown.
            }
            catch (Exception ex)
            {
                runtimeState.RecordCandidateInboxError(ex.Message);
            }
            finally
            {
                pendingPaths.TryRemove(path, out _);
            }
        }, cancellation.Token);
    }

    private static async Task<SpeechCandidateRequest?> ReadRequestAsync(string path, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return await JsonSerializer.DeserializeAsync<SpeechCandidateRequest>(
                    stream,
                    BridgeJson.Options,
                    cancellationToken);
            }
            catch (IOException) when (attempt < 4)
            {
                await Task.Delay(100, cancellationToken);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        return null;
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // A later cleanup pass can remove stale files.
        }
    }

    private void MoveToRejected(string path)
    {
        try
        {
            var rejectedDirectory = Path.Combine(inboxDirectory, "rejected");
            Directory.CreateDirectory(rejectedDirectory);
            var targetPath = Path.Combine(
                rejectedDirectory,
                $"{Path.GetFileNameWithoutExtension(path)}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json");
            if (File.Exists(path))
            {
                File.Move(path, targetPath, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            runtimeState.RecordCandidateInboxError(ex.Message);
        }
    }
}
