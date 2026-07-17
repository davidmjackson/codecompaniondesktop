using System;
using System.Net;
using System.Threading.Tasks;

namespace CodeCompanionDesktop.Bridge;

public sealed class SpeechCandidateProcessor
{
    // A safety net for the synchronous (non-queued) speak path. Playback of a
    // 500-character opening is well under a minute; this only trips if a speak
    // hangs and never returns, which must not be allowed to latch isSpeaking
    // and reject every later message as "busy".
    private static readonly TimeSpan DefaultSpeakTimeout = TimeSpan.FromMinutes(3);

    private readonly Func<string, Task> speakAsync;
    private readonly BridgeRuntimeState runtimeState;
    private readonly BridgeSpeechQueue speechQueue;
    private readonly SpeechCandidatePipeline speechCandidatePipeline;
    private readonly TimeSpan speakTimeout;

    public SpeechCandidateProcessor(
        Func<string, Task> speakAsync,
        BridgeRuntimeState runtimeState,
        BridgeSpeechQueue speechQueue,
        SpeechCandidatePipeline? speechCandidatePipeline = null,
        TimeSpan? speakTimeout = null)
    {
        this.speakAsync = speakAsync;
        this.runtimeState = runtimeState;
        this.speechQueue = speechQueue;
        this.speechCandidatePipeline = speechCandidatePipeline ?? new SpeechCandidatePipeline(runtimeState.SpeechProfiles);
        this.speakTimeout = speakTimeout ?? DefaultSpeakTimeout;
    }

    // source is the delivery channel: "bridge" for the live HTTP path,
    // "inbox" for the candidate-inbox fallback (Reliability spec, Task 5).
    public async Task<SpeechCandidateProcessingResult> ProcessAsync(
        SpeechCandidateRequest candidateRequest,
        string source = "bridge")
    {
        ArgumentNullException.ThrowIfNull(candidateRequest);

        var error = BridgeContractValidator.ValidateSpeechCandidate(candidateRequest);
        if (error is not null)
        {
            return SpeechCandidateProcessingResult.BadRequest(error);
        }

        var candidateContext = runtimeState.RecordSpeechCandidate(
            candidateRequest.Client,
            candidateRequest.Workspace,
            candidateRequest.Codex.MessageId,
            candidateRequest.Candidate.Text,
            source);

        var pipelineResult = speechCandidatePipeline.Prepare(new SpeechCandidatePipelineInput(
            candidateRequest.Codex.MessageId,
            candidateRequest.Candidate.Kind,
            candidateRequest.Candidate.Phase,
            candidateRequest.Candidate.SpeechHint,
            candidateRequest.Candidate.Text));

        if (pipelineResult.Decision == "ignored" || pipelineResult.Decision == "duplicate")
        {
            runtimeState.RecordSpeechCandidateDecision(candidateContext, pipelineResult.Decision, pipelineResult.Reason);
            return SpeechCandidateProcessingResult.Accepted(
                new SpeechCandidateResponse("accepted", pipelineResult.Decision, pipelineResult.Reason, 0));
        }

        if (pipelineResult.SpeechText is null || pipelineResult.Reservation is null)
        {
            runtimeState.RecordSpeechCandidateDecision(candidateContext, "rejected", "invalid_pipeline_result");
            return SpeechCandidateProcessingResult.InternalError(
                new SpeechCandidateResponse("rejected", "rejected", "invalid_pipeline_result", 0));
        }

        // A self-test candidate has now cleared the full pipeline - validation,
        // normalization, policy, and reservation. When self-test playback is
        // Silent, accept it without speaking; the user confirms it arrived via
        // the Speech History panel instead (Reliability spec, Task 4).
        if (SpeechCandidatePipeline.IsSelfTestKind(candidateRequest.Candidate.Kind)
            && runtimeState.SelfTestPlaybackSilent)
        {
            runtimeState.RecordSpeechCandidateDecision(candidateContext, "silent", pipelineResult.Reason);
            return SpeechCandidateProcessingResult.Accepted(
                new SpeechCandidateResponse("accepted", "silent", pipelineResult.Reason, 0));
        }

        if (runtimeState.QueueBridgeSpeechRequests)
        {
            if (!speechQueue.TryEnqueue(
                pipelineResult.SpeechText,
                exception =>
                {
                    if (exception is not null)
                    {
                        speechCandidatePipeline.Release(pipelineResult.Reservation);
                    }

                    return Task.CompletedTask;
                },
                out var position))
            {
                speechCandidatePipeline.Release(pipelineResult.Reservation);
                runtimeState.RecordSpeechCandidateDecision(candidateContext, "rejected", "queue_full");
                return SpeechCandidateProcessingResult.Conflict(
                    new SpeechCandidateResponse("rejected", "rejected", "queue_full", 0));
            }

            runtimeState.RecordSpeechCandidateDecision(candidateContext, "queued", pipelineResult.Reason);
            return SpeechCandidateProcessingResult.Accepted(
                new SpeechCandidateResponse("accepted", "queued", pipelineResult.Reason, position));
        }

        if (!runtimeState.TryBeginSpeaking())
        {
            speechCandidatePipeline.Release(pipelineResult.Reservation);
            runtimeState.RecordSpeechCandidateDecision(candidateContext, "rejected", "busy");
            return SpeechCandidateProcessingResult.Conflict(
                new SpeechCandidateResponse("rejected", "rejected", "busy", 0));
        }

        runtimeState.RecordSpeechCandidatePlaybackStarted(candidateContext, pipelineResult.Reason);

        try
        {
            await speakAsync(pipelineResult.SpeechText).WaitAsync(speakTimeout);
            runtimeState.CompleteSpeaking();
            runtimeState.RecordSpeechCandidateDecision(candidateContext, "spoken", pipelineResult.Reason);
            return SpeechCandidateProcessingResult.Accepted(
                new SpeechCandidateResponse("accepted", "spoken", pipelineResult.Reason, 0));
        }
        catch (TimeoutException)
        {
            speechCandidatePipeline.Release(pipelineResult.Reservation);
            runtimeState.FailSpeaking("speech_timeout");
            runtimeState.RecordSpeechCandidateDecision(candidateContext, "rejected", "speech_timeout");
            return SpeechCandidateProcessingResult.InternalError(
                new SpeechCandidateResponse("rejected", "rejected", "speech_timeout", 0));
        }
        catch (Exception ex)
        {
            speechCandidatePipeline.Release(pipelineResult.Reservation);
            runtimeState.FailSpeaking(ex.Message);
            return SpeechCandidateProcessingResult.InternalError(new ErrorResponse(ex.Message));
        }
    }
}

public sealed record SpeechCandidateProcessingResult(HttpStatusCode StatusCode, object Payload)
{
    public static SpeechCandidateProcessingResult Accepted(SpeechCandidateResponse response)
    {
        return new SpeechCandidateProcessingResult(HttpStatusCode.Accepted, response);
    }

    public static SpeechCandidateProcessingResult BadRequest(string error)
    {
        return new SpeechCandidateProcessingResult(HttpStatusCode.BadRequest, new ErrorResponse(error));
    }

    public static SpeechCandidateProcessingResult Conflict(SpeechCandidateResponse response)
    {
        return new SpeechCandidateProcessingResult(HttpStatusCode.Conflict, response);
    }

    public static SpeechCandidateProcessingResult InternalError(object payload)
    {
        return new SpeechCandidateProcessingResult(HttpStatusCode.InternalServerError, payload);
    }
}
