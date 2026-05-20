using CodeCompanionDesktop.Bridge;

namespace CodeCompanionDesktop.Tests.Bridge;

public sealed class SpeechCandidatePipelineTests
{
    [Fact]
    public void DemoModeCommandEnablesDemoProfileAndReturnsAcknowledgement()
    {
        var profiles = new SpeechProfileState();
        var pipeline = new SpeechCandidatePipeline(profiles);

        var result = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-demo-mode",
            "assistant-message",
            "final",
            null,
            "  demo mode  "));

        Assert.Equal("accepted", result.Decision);
        Assert.Equal("demo-mode-enabled", result.Reason);
        Assert.Equal("Demo Mode is on. I will speak more often during this session.", result.SpeechText);
        Assert.True(profiles.IsDemoModeActive());
    }

    [Fact]
    public void EndDemoCommandRestoresStandardProfile()
    {
        var profiles = new SpeechProfileState();
        profiles.EnableDemoMode();
        var pipeline = new SpeechCandidatePipeline(profiles);

        var result = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-end-demo",
            "assistant-message",
            "final",
            null,
            "End Demo"));

        Assert.Equal("accepted", result.Decision);
        Assert.Equal("demo-mode-ended", result.Reason);
        Assert.Equal("Demo Mode is off. Standard speech policy is restored.", result.SpeechText);
        Assert.False(profiles.IsDemoModeActive());
    }

    [Fact]
    public void SimilarDemoPhrasesDoNotToggleProfile()
    {
        var profiles = new SpeechProfileState();
        var pipeline = new SpeechCandidatePipeline(profiles);

        var result = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-similar-demo",
            "assistant-message",
            "commentary",
            null,
            "Please demo this mode."));

        Assert.Equal("ignored", result.Decision);
        Assert.Equal("non_final_candidate", result.Reason);
        Assert.False(profiles.IsDemoModeActive());
    }

    [Fact]
    public void DemoModeAcceptsNonFinalAssistantProgress()
    {
        var profiles = new SpeechProfileState();
        profiles.EnableDemoMode();
        var pipeline = new SpeechCandidatePipeline(profiles);

        var result = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-demo-progress",
            "assistant-message",
            "commentary",
            null,
            "I am checking the bridge and speech policy now."));

        Assert.Equal("accepted", result.Decision);
        Assert.Equal("demo-mode-progress", result.Reason);
        Assert.Equal("I am checking the bridge and speech policy now.", result.SpeechText);
    }

    [Fact]
    public void DemoModeStillPrivacyFiltersBeforeSpeaking()
    {
        var profiles = new SpeechProfileState();
        profiles.EnableDemoMode();
        var pipeline = new SpeechCandidatePipeline(profiles);

        var result = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-demo-privacy",
            "assistant-message",
            "commentary",
            null,
            "Using token: abcdefghijklmnop and email david@example.com."));

        Assert.Equal("accepted", result.Decision);
        Assert.Equal("demo-mode-progress", result.Reason);
        Assert.NotNull(result.SpeechText);
        Assert.Contains("[redacted]", result.SpeechText, StringComparison.Ordinal);
        Assert.Contains("[redacted email]", result.SpeechText, StringComparison.Ordinal);
        Assert.DoesNotContain("abcdefghijklmnop", result.SpeechText, StringComparison.Ordinal);
        Assert.DoesNotContain("david@example.com", result.SpeechText, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateDemoCommandDoesNotRepeatSpeech()
    {
        var profiles = new SpeechProfileState();
        var pipeline = new SpeechCandidatePipeline(profiles);

        var first = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-demo-duplicate",
            "assistant-message",
            "final",
            null,
            "Demo Mode"));
        var second = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-demo-duplicate",
            "assistant-message",
            "final",
            null,
            "Demo Mode"));

        Assert.Equal("accepted", first.Decision);
        Assert.Equal("duplicate", second.Decision);
        Assert.Equal("duplicate_candidate", second.Reason);
        Assert.True(profiles.IsDemoModeActive());
    }

    [Fact]
    public void DemoModeCanBeEnabledAgainAfterEndDemo()
    {
        var profiles = new SpeechProfileState();
        var pipeline = new SpeechCandidatePipeline(profiles);

        var firstEnable = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-demo-first",
            "assistant-message",
            "final",
            null,
            "Demo Mode"));
        var endDemo = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-demo-end",
            "assistant-message",
            "final",
            null,
            "end demo"));
        var secondEnable = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-demo-second",
            "assistant-message",
            "final",
            null,
            "Demo Mode"));

        Assert.Equal("accepted", firstEnable.Decision);
        Assert.Equal("accepted", endDemo.Decision);
        Assert.Equal("accepted", secondEnable.Decision);
        Assert.Equal("demo-mode-enabled", secondEnable.Reason);
        Assert.True(profiles.IsDemoModeActive());
    }

    [Fact]
    public void NewProfileStateStartsInStandardProfile()
    {
        var profiles = new SpeechProfileState();
        profiles.EnableDemoMode();

        var nextSessionProfiles = new SpeechProfileState();

        Assert.True(profiles.IsDemoModeActive());
        Assert.False(nextSessionProfiles.IsDemoModeActive());
        Assert.Equal(nameof(SpeechProfile.Standard), nextSessionProfiles.ActiveProfileName);
    }

    [Fact]
    public void RewritesWindowsDirectoryPathsForSpeech()
    {
        var pipeline = new SpeechCandidatePipeline();

        var result = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-windows-path",
            "assistant-message",
            "final",
            null,
            "Open D:\\Development\\CodeCompanionDesktop\\src\\CodeCompanionDesktop before testing."));

        Assert.Equal("accepted", result.Decision);
        Assert.Equal("speech_rewritten", result.Reason);
        Assert.Equal("Open that location before testing.", result.SpeechText);
    }

    [Fact]
    public void RewritesWslDirectoryPathsForSpeech()
    {
        var pipeline = new SpeechCandidatePipeline();

        var result = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-wsl-path",
            "assistant-message",
            "final",
            null,
            "The Retrospective app is in /var/www/retrospective/public/client.js:42."));

        Assert.Equal("accepted", result.Decision);
        Assert.Equal("speech_rewritten", result.Reason);
        Assert.Equal("The Retrospective app is in that location.", result.SpeechText);
    }

    [Fact]
    public void RewritesUncDirectoryPathsForSpeech()
    {
        var pipeline = new SpeechCandidatePipeline();

        var result = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-unc-path",
            "assistant-message",
            "final",
            null,
            "Use \\\\wsl.localhost\\Ubuntu-24.04\\var\\www\\retrospective for the project."));

        Assert.Equal("accepted", result.Decision);
        Assert.Equal("speech_rewritten", result.Reason);
        Assert.Equal("Use that location for the project.", result.SpeechText);
    }

    [Fact]
    public void ReplacesBareUrlsWithASpokenPlaceholder()
    {
        var pipeline = new SpeechCandidatePipeline();

        var result = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-url",
            "assistant-message",
            "final",
            null,
            "Bridge health is at http://127.0.0.1:47321/health."));

        Assert.Equal("accepted", result.Decision);
        Assert.Equal("Bridge health is at a link.", result.SpeechText);
    }

    [Fact]
    public void ReplacesMarkdownLinksWithTheirVisibleLabel()
    {
        var pipeline = new SpeechCandidatePipeline();

        var result = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-md-link",
            "assistant-message",
            "final",
            null,
            "Confirmed - main CI is **green**. See [run 26160030582](https://github.com/davidmjackson/codecompanion/actions/runs/26160030582)."));

        Assert.Equal("accepted", result.Decision);
        Assert.Equal("Confirmed - main CI is green. See run 26160030582.", result.SpeechText);
    }

    [Fact]
    public void StripsInlineCodeBackticksButKeepsTheWording()
    {
        var pipeline = new SpeechCandidatePipeline();

        var result = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-inline-code",
            "assistant-message",
            "final",
            null,
            "`npm ci` is clean - the `scrumpoker` service is the remaining step."));

        Assert.Equal("accepted", result.Decision);
        Assert.Equal("npm ci is clean - the scrumpoker service is the remaining step.", result.SpeechText);
    }

    [Fact]
    public void DropsFencedCodeBlocks()
    {
        var pipeline = new SpeechCandidatePipeline();

        var result = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-fenced",
            "assistant-message",
            "final",
            null,
            "Here is the fix:\n```\nsudo systemctl restart scrumpoker\n```\nThat should restore the service."));

        Assert.Equal("accepted", result.Decision);
        Assert.Equal("Here is the fix: That should restore the service.", result.SpeechText);
    }

    [Fact]
    public void StripsEmphasisAndLineLeadingListMarkers()
    {
        var pipeline = new SpeechCandidatePipeline();

        var result = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-list",
            "assistant-message",
            "final",
            null,
            "Status:\n- **Done**: deploy verified\n- Pending: smoke test"));

        Assert.Equal("accepted", result.Decision);
        Assert.Equal("Status: Done: deploy verified Pending: smoke test", result.SpeechText);
    }

    [Fact]
    public void ShortensALongCandidateWithoutSentenceBreaksToAHardCut()
    {
        var pipeline = new SpeechCandidatePipeline();
        var longText = string.Join(" ", Enumerable.Repeat("website planning summary", 120));

        var result = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-long-final",
            "assistant-message",
            "final",
            null,
            longText));

        Assert.Equal("accepted", result.Decision);
        Assert.Equal("shortened", result.Reason);
        Assert.NotNull(result.SpeechText);
        Assert.True(result.SpeechText!.Length <= SpeechCandidatePipeline.MaxSpeechTextLength);
        Assert.EndsWith("...", result.SpeechText);
    }

    [Fact]
    public void SpeaksOnlyTheOpeningSentencesOfALongFinalAnswer()
    {
        var pipeline = new SpeechCandidatePipeline();
        var longText =
            "The deployment is complete and verified on production. " +
            "All four pull requests are merged into the main branch. " +
            "The systemd service is enabled and restarts automatically on boot. " +
            "Access keys are now stored as salted hashes on disk. " +
            "Smoke tests pass cleanly across every area that was checked.";

        var result = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-long-opening",
            "assistant-message",
            "final",
            null,
            longText));

        Assert.Equal("accepted", result.Decision);
        Assert.Equal("shortened", result.Reason);
        Assert.NotNull(result.SpeechText);
        Assert.True(result.SpeechText!.Length <= SpeechCandidatePipeline.MaxSpeechTextLength);
        Assert.StartsWith("The deployment is complete and verified on production.", result.SpeechText);
        Assert.EndsWith(".", result.SpeechText);
        Assert.DoesNotContain("Smoke tests", result.SpeechText!, StringComparison.Ordinal);
    }

    [Fact]
    public void DropsMarkdownTables()
    {
        var pipeline = new SpeechCandidatePipeline();
        var text =
            "Here is the summary of the work.\n" +
            "\n" +
            "| Phase | Outcome |\n" +
            "|---|---|\n" +
            "| **Review** | Full codebase review completed |\n" +
            "| **Deploy** | Live on production |\n" +
            "\n" +
            "That wraps up the task.";

        var result = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-table",
            "assistant-message",
            "final",
            null,
            text));

        Assert.Equal("accepted", result.Decision);
        Assert.Equal("Here is the summary of the work. That wraps up the task.", result.SpeechText);
    }
}
