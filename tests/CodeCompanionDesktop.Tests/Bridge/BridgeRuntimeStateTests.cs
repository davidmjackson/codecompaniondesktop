using CodeCompanionDesktop.Bridge;
using CodeCompanionDesktop.History;

namespace CodeCompanionDesktop.Tests.Bridge;

public sealed class BridgeRuntimeStateTests
{
    [Fact]
    public void DiagnosticsSnapshotIncludesCandidateDecisionAndRecentResults()
    {
        var state = new BridgeRuntimeState();
        state.ConfigureQueue(true, 5);
        var client = new BridgeClient("test-client", "Code Companion Voice", "0.0.0", "windows", "windows");
        var workspace = new BridgeWorkspace(
            "codecompaniondesktop",
            "Code Companion Desktop",
            ["D:\\Development\\CodeCompanionDesktop"]);
        state.RecordClientSeen(client, workspace);
        state.RecordSpeechCandidate(client, workspace, "message-1", "Speech diagnostics test.");
        state.RecordSpeechCandidateDecision("spoken", "accepted");
        state.RecordProviderError("provider unavailable");
        state.RecordPlaybackError("device unavailable");

        var snapshot = state.GetDiagnosticsSnapshot();

        Assert.True(snapshot.QueueBridgeSpeechRequests);
        Assert.Equal(5, snapshot.MaxQueuedSpeechRequests);
        Assert.Contains("Code Companion Voice", snapshot.LastClient, StringComparison.Ordinal);
        Assert.Contains("message-1", snapshot.LastSpeechCandidate, StringComparison.Ordinal);
        Assert.Equal("spoken (accepted)", snapshot.LastSpeechDecision);
        Assert.Equal("provider unavailable", snapshot.LastProviderError);
        Assert.Equal("device unavailable", snapshot.LastPlaybackError);
        Assert.Contains(snapshot.RecentProjects, result => result.Contains("codecompaniondesktop", StringComparison.Ordinal));
        Assert.Contains(snapshot.RecentProjects, result => result.Contains("D:\\Development\\CodeCompanionDesktop", StringComparison.Ordinal));
        Assert.Contains(snapshot.RecentBridgeClients, result => result.Contains("Code Companion Voice", StringComparison.Ordinal));
        Assert.Contains(snapshot.RecentSpeechResults, result => result.Contains("Candidate spoken (accepted).", StringComparison.Ordinal));
        Assert.Contains(snapshot.RecentSpeechResults, result => result.Contains("Provider error: provider unavailable", StringComparison.Ordinal));
        Assert.Contains(snapshot.RecentSpeechResults, result => result.Contains("Playback error: device unavailable", StringComparison.Ordinal));
    }

    [Fact]
    public void ProjectRegistryMergesObservedRootsByProjectId()
    {
        var directory = Directory.CreateTempSubdirectory("code-companion-project-registry-");
        try
        {
            var registryPath = Path.Combine(directory.FullName, "project-registry.json");
            var registry = new ProjectRegistryStore(registryPath);
            var state = new BridgeRuntimeState(projectRegistryStore: registry);
            var client = new BridgeClient("test-client", "Code Companion Voice", "0.0.0", "wsl", "wsl");
            var windowsWorkspace = new BridgeWorkspace(
                "codecompaniondesktop",
                "Code Companion Desktop",
                ["D:\\Development\\CodeCompanionDesktop"]);
            var wslWorkspace = new BridgeWorkspace(
                "codecompaniondesktop",
                "Code Companion Desktop",
                ["/mnt/d/Development/CodeCompanionDesktop"]);

            state.RecordClientSeen(client, windowsWorkspace);
            state.RecordClientSeen(client, wslWorkspace);

            var snapshot = registry.Load();
            var project = Assert.Single(snapshot.Projects);
            Assert.Equal("codecompaniondesktop", project.ProjectId);
            Assert.Contains("D:\\Development\\CodeCompanionDesktop", project.ObservedRoots);
            Assert.Contains("/mnt/d/Development/CodeCompanionDesktop", project.ObservedRoots);
            Assert.Contains("wsl", project.Environments);

            var details = Assert.Single(state.LoadProjectRegistryDetails(8));
            Assert.Contains("Code Companion Desktop (codecompaniondesktop)", details, StringComparison.Ordinal);
            Assert.Contains("Observed roots:", details, StringComparison.Ordinal);
            Assert.Contains("D:\\Development\\CodeCompanionDesktop", details, StringComparison.Ordinal);
            Assert.Contains("/mnt/d/Development/CodeCompanionDesktop", details, StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ProjectRegistryAliasManagementUpdatesObservedRoots()
    {
        var directory = Directory.CreateTempSubdirectory("code-companion-project-aliases-");
        try
        {
            var registryPath = Path.Combine(directory.FullName, "project-registry.json");
            var registry = new ProjectRegistryStore(registryPath);
            var state = new BridgeRuntimeState(projectRegistryStore: registry);
            var client = new BridgeClient("test-client", "Code Companion Voice", "0.0.0", "windows", "windows");
            var workspace = new BridgeWorkspace(
                "codecompaniondesktop",
                "Code Companion Desktop",
                ["D:\\Development\\CodeCompanionDesktop"]);

            state.RecordClientSeen(client, workspace);

            Assert.True(state.TryAddProjectRootAlias("codecompaniondesktop", "/mnt/d/Development/CodeCompanionDesktop"));
            Assert.Contains(
                registry.Load().Projects.Single().ObservedRoots,
                root => root == "/mnt/d/Development/CodeCompanionDesktop");

            Assert.True(state.TryRemoveProjectRootAlias("codecompaniondesktop", "/mnt/d/Development/CodeCompanionDesktop"));
            Assert.DoesNotContain(
                registry.Load().Projects.Single().ObservedRoots,
                root => root == "/mnt/d/Development/CodeCompanionDesktop");
            Assert.False(state.TryAddProjectRootAlias("missing-project", "D:\\Development\\Missing"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
