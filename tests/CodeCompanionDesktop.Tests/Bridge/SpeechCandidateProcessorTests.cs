using System.Collections.Concurrent;
using System.Net;
using CodeCompanionDesktop.Bridge;

namespace CodeCompanionDesktop.Tests.Bridge;

public sealed class SpeechCandidateProcessorTests
{
    [Fact]
    public async Task SpeaksASelfTestCandidateWhenPlaybackIsSpeak()
    {
        var fixture = ProcessorFixture.Create();

        var result = await fixture.Processor.ProcessAsync(SelfTestRequest("self-test-speak"));

        Assert.Equal(HttpStatusCode.Accepted, result.StatusCode);
        var response = Assert.IsType<SpeechCandidateResponse>(result.Payload);
        Assert.Equal("spoken", response.Decision);
        Assert.Equal("self-test", response.Reason);
        Assert.Equal(["Code Companion pipeline self-test."], fixture.SpokenTexts);
    }

    [Fact]
    public async Task AcceptsASelfTestCandidateSilentlyWhenPlaybackIsSilent()
    {
        var fixture = ProcessorFixture.Create();
        fixture.RuntimeState.ConfigureSelfTestPlayback(isSilent: true);

        var result = await fixture.Processor.ProcessAsync(SelfTestRequest("self-test-silent"));

        Assert.Equal(HttpStatusCode.Accepted, result.StatusCode);
        var response = Assert.IsType<SpeechCandidateResponse>(result.Payload);
        Assert.Equal("silent", response.Decision);
        Assert.Equal("self-test", response.Reason);

        // The whole pipeline ran, but silent playback skips the audio.
        Assert.Empty(fixture.SpokenTexts);
    }

    private static SpeechCandidateRequest SelfTestRequest(string messageId)
    {
        return new SpeechCandidateRequest(
            1,
            new BridgeClient("test-client", "Code Companion Voice", "0.0.0", "windows", "windows"),
            new BridgeWorkspace("project-1", "Project One", ["D:\\project"]),
            new CodexMetadata("self-test", messageId, "2026-05-20T00:00:00.000Z"),
            new SpeechCandidate("self-test", "final", null, "Code Companion pipeline self-test.", null));
    }

    private sealed class ProcessorFixture
    {
        private ProcessorFixture(
            SpeechCandidateProcessor processor,
            BridgeRuntimeState runtimeState,
            ConcurrentQueue<string> spokenTexts)
        {
            Processor = processor;
            RuntimeState = runtimeState;
            SpokenTexts = spokenTexts;
        }

        public SpeechCandidateProcessor Processor { get; }

        public BridgeRuntimeState RuntimeState { get; }

        public ConcurrentQueue<string> SpokenTexts { get; }

        public static ProcessorFixture Create()
        {
            var runtimeState = new BridgeRuntimeState();
            runtimeState.ConfigureQueue(false, 3);
            var spokenTexts = new ConcurrentQueue<string>();
            Task Speak(string text)
            {
                spokenTexts.Enqueue(text);
                return Task.CompletedTask;
            }

            var queue = new BridgeSpeechQueue(Speak, runtimeState);
            var processor = new SpeechCandidateProcessor(
                Speak,
                runtimeState,
                queue,
                new SpeechCandidatePipeline(runtimeState.SpeechProfiles));
            return new ProcessorFixture(processor, runtimeState, spokenTexts);
        }
    }
}
