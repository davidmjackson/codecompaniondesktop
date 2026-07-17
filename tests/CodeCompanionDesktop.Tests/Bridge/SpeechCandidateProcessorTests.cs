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

    // The bug: a single playback that hangs (speakAsync never returns) latches
    // isSpeaking forever, so every later final message is rejected "busy" and
    // TTS dies for the rest of the session. The processor must time the speak
    // out, release the latch, and keep serving later messages.
    [Fact]
    public async Task ResetsTheSpeakingLatchWhenPlaybackHangsSoLaterMessagesAreNotRejectedBusy()
    {
        var runtimeState = new BridgeRuntimeState();
        runtimeState.ConfigureQueue(false, 3);

        var firstCall = true;
        var spoken = new ConcurrentQueue<string>();
        Task Speak(string text)
        {
            if (firstCall)
            {
                firstCall = false;
                // Never completes: models a wedged MediaPlayer playback.
                return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task;
            }

            spoken.Enqueue(text);
            return Task.CompletedTask;
        }

        var pipeline = new SpeechCandidatePipeline(runtimeState.SpeechProfiles);
        var queue = new BridgeSpeechQueue(Speak, runtimeState);
        var processor = new SpeechCandidateProcessor(
            Speak,
            runtimeState,
            queue,
            pipeline,
            TimeSpan.FromMilliseconds(150));

        var first = await processor.ProcessAsync(FinalMessageRequest("hang-1", "First answer."));
        Assert.Equal(HttpStatusCode.InternalServerError, first.StatusCode);
        Assert.Equal("speech_timeout", Assert.IsType<SpeechCandidateResponse>(first.Payload).Reason);
        Assert.False(runtimeState.IsSpeaking);

        var second = await processor.ProcessAsync(FinalMessageRequest("hang-2", "Second answer."));
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal("spoken", Assert.IsType<SpeechCandidateResponse>(second.Payload).Decision);
        Assert.Equal(["Second answer."], spoken);
    }

    private static SpeechCandidateRequest FinalMessageRequest(string messageId, string text)
    {
        return new SpeechCandidateRequest(
            1,
            new BridgeClient("test-client", "Code Companion Voice", "0.0.0", "windows", "windows"),
            new BridgeWorkspace("project-1", "Project One", ["D:\\project"]),
            new CodexMetadata("session-1", messageId, "2026-05-20T00:00:00.000Z"),
            new SpeechCandidate("assistant-message", "final", null, text, null));
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
