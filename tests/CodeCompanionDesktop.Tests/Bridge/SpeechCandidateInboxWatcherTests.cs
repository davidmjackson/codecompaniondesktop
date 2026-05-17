using System.Collections.Concurrent;
using System.Text;
using CodeCompanionDesktop.Bridge;

namespace CodeCompanionDesktop.Tests.Bridge;

public sealed class SpeechCandidateInboxWatcherTests
{
    [Fact]
    public async Task ProcessFileSpeaksValidCandidateAndDeletesInboxFile()
    {
        using var fixture = await InboxFixture.CreateAsync();
        var candidatePath = await fixture.WriteCandidateAsync("message-inbox", "The inbox candidate is ready.");

        var result = await fixture.Watcher.ProcessFileAsync(candidatePath);

        Assert.Equal(System.Net.HttpStatusCode.Accepted, result.StatusCode);
        Assert.False(File.Exists(candidatePath));
        Assert.Equal(["The inbox candidate is ready."], fixture.SpokenTexts);
        Assert.Contains("message-inbox", fixture.RuntimeState.LastSpeechCandidate, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessFileMovesInvalidJsonToRejectedDirectory()
    {
        using var fixture = await InboxFixture.CreateAsync();
        var candidatePath = Path.Combine(fixture.InboxDirectory, "invalid.json");
        await File.WriteAllTextAsync(candidatePath, "{not-json", Encoding.UTF8);

        var result = await fixture.Watcher.ProcessFileAsync(candidatePath);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, result.StatusCode);
        Assert.False(File.Exists(candidatePath));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(fixture.InboxDirectory, "rejected"), "*.json"));
    }

    [Fact]
    public async Task ProcessFileCanEnableDemoMode()
    {
        using var fixture = await InboxFixture.CreateAsync();
        var candidatePath = await fixture.WriteCandidateAsync("message-demo-mode", "Demo Mode");

        var result = await fixture.Watcher.ProcessFileAsync(candidatePath);

        Assert.Equal(System.Net.HttpStatusCode.Accepted, result.StatusCode);
        Assert.True(fixture.RuntimeState.SpeechProfiles.IsDemoModeActive());
        Assert.Equal(["Demo Mode is on. I will speak more often during this session."], fixture.SpokenTexts);
    }

    private sealed class InboxFixture : IDisposable
    {
        private InboxFixture(
            string inboxDirectory,
            SpeechCandidateInboxWatcher watcher,
            BridgeRuntimeState runtimeState,
            ConcurrentQueue<string> spokenTexts)
        {
            InboxDirectory = inboxDirectory;
            Watcher = watcher;
            RuntimeState = runtimeState;
            SpokenTexts = spokenTexts;
        }

        public string InboxDirectory { get; }

        public SpeechCandidateInboxWatcher Watcher { get; }

        public BridgeRuntimeState RuntimeState { get; }

        public ConcurrentQueue<string> SpokenTexts { get; }

        public static async Task<InboxFixture> CreateAsync()
        {
            var inboxDirectory = Path.Combine(Path.GetTempPath(), $"code-companion-inbox-{Guid.NewGuid():N}");
            Directory.CreateDirectory(inboxDirectory);
            var runtimeState = new BridgeRuntimeState();
            runtimeState.ConfigureQueue(false, 3);
            var spokenTexts = new ConcurrentQueue<string>();
            var queue = new BridgeSpeechQueue(text =>
            {
                spokenTexts.Enqueue(text);
                return Task.CompletedTask;
            }, runtimeState);
            var processor = new SpeechCandidateProcessor(
                text =>
                {
                    spokenTexts.Enqueue(text);
                    return Task.CompletedTask;
                },
                runtimeState,
                queue,
                new SpeechCandidatePipeline(runtimeState.SpeechProfiles));
            var watcher = new SpeechCandidateInboxWatcher(inboxDirectory, processor, runtimeState);
            await Task.Yield();
            return new InboxFixture(inboxDirectory, watcher, runtimeState, spokenTexts);
        }

        public async Task<string> WriteCandidateAsync(string messageId, string text)
        {
            var candidatePath = Path.Combine(InboxDirectory, $"{messageId}.json");
            await File.WriteAllTextAsync(candidatePath, $$"""
                {
                  "schemaVersion": 1,
                  "client": {
                    "clientId": "test-client",
                    "name": "Code Companion Voice",
                    "version": "0.0.0",
                    "host": "windows",
                    "environment": "windows"
                  },
                  "workspace": {
                    "projectId": "codecompaniondesktop",
                    "displayName": "Code Companion Desktop",
                    "roots": ["D:\\Development\\CodeCompanionDesktop"]
                  },
                  "codex": {
                    "sessionId": "session-1",
                    "messageId": "{{messageId}}",
                    "timestamp": "2026-05-13T00:00:00Z"
                  },
                  "candidate": {
                    "kind": "assistant-message",
                    "phase": "final",
                    "text": "{{text}}",
                    "source": "candidate-inbox"
                  }
                }
                """, Encoding.UTF8);
            return candidatePath;
        }

        public void Dispose()
        {
            Watcher.Dispose();
            Directory.Delete(InboxDirectory, recursive: true);
        }
    }
}
