using CodeCompanionDesktop.Bridge;

namespace CodeCompanionDesktop.Tests.Bridge;

public sealed class SpeechCandidatePipelineTests
{
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
    public void DoesNotRewriteUrlsOrBridgeRoutes()
    {
        var pipeline = new SpeechCandidatePipeline();

        var result = pipeline.Prepare(new SpeechCandidatePipelineInput(
            "message-route",
            "assistant-message",
            "final",
            null,
            "Bridge health is at http://127.0.0.1:47321/health and candidates use /v1/speech/candidates."));

        Assert.Equal("accepted", result.Decision);
        Assert.Equal("accepted", result.Reason);
        Assert.Equal(
            "Bridge health is at http://127.0.0.1:47321/health and candidates use /v1/speech/candidates.",
            result.SpeechText);
    }
}
