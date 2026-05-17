using System;
using System.Net;
using System.Threading.Tasks;

namespace CodeCompanionDesktop.Bridge;

public sealed class SpeechCandidateProcessor
{
    private readonly Func<string, Task> speakAsync;
    private readonly BridgeRuntimeState runtimeState;
    private readonly BridgeSpeechQueue speechQueue;
    private readonly SpeechCandidatePipeline speechCandidatePipeline;

    public SpeechCandidateProcessor(
        Func<string, Task> speakAsync,
        BridgeRuntimeState runtimeState,
        BridgeSpeechQueue speechQueue,
        SpeechCandidatePipeline? speechCandidatePipeline = null)
    {
        this.speakAsync = speakAsync;
        this.runtimeState = runtimeState;
        this.speechQueue = speechQueue;
        this.speechCandidatePipeline = speechCandidatePipeline ?? new SpeechCandidatePipeline(runtimeState.SpeechProfiles);
    }

    public async Task<SpeechCandidateProcessingResult> ProcessAsync(SpeechCandidateRequest candidateRequest)
    {
        ArgumentNullException.ThrowIfNull(candidateRequest);

        var error = BridgeContractValidator.ValidateSpeechCandidate(candidateRequest);
        if (error is not null)
        {
            return SpeechCandidateProcessingResult.BadRequest(error);
        }

        runtimeState.RecordSpeechCandidate(
            candidateRequest.Client,
            candidateRequest.Workspace,
            candidateRequest.Codex.MessageId,
            candidateRequest.Candidate.Text);

        var pipelineResult = speechCandidatePipeline.Prepare(new SpeechCandidatePipelineInput(
            candidateRequest.Codex.MessageId,
            candidateRequest.Candidate.Kind,
            candidateRequest.Candidate.Phase,
            candidateRequest.Candidate.SpeechHint,
            candidateRequest.Candidate.Text));

        if (pipelineResult.Decision == "ignored" || pipelineResult.Decision == "duplicate")
        {
            runtimeState.RecordSpeechCandidateDecision(pipelineResult.Decision, pipelineResult.Reason);
            return SpeechCandidateProcessingResult.Accepted(
                new SpeechCandidateResponse("accepted", pipelineResult.Decision, pipelineResult.Reason, 0));
        }

        if (pipelineResult.SpeechText is null || pipelineResult.Reservation is null)
        {
            runtimeState.RecordSpeechCandidateDecision("rejected", "invalid_pipeline_result");
            return SpeechCandidateProcessingResult.InternalError(
                new SpeechCandidateResponse("rejected", "rejected", "invalid_pipeline_result", 0));
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
                runtimeState.RecordSpeechCandidateDecision("rejected", "queue_full");
                return SpeechCandidateProcessingResult.Conflict(
                    new SpeechCandidateResponse("rejected", "rejected", "queue_full", 0));
            }

            runtimeState.RecordSpeechCandidateDecision("queued", pipelineResult.Reason);
            return SpeechCandidateProcessingResult.Accepted(
                new SpeechCandidateResponse("accepted", "queued", pipelineResult.Reason, position));
        }

        if (!runtimeState.TryBeginSpeaking())
        {
            speechCandidatePipeline.Release(pipelineResult.Reservation);
            runtimeState.RecordSpeechCandidateDecision("rejected", "busy");
            return SpeechCandidateProcessingResult.Conflict(
                new SpeechCandidateResponse("rejected", "rejected", "busy", 0));
        }

        try
        {
            await speakAsync(pipelineResult.SpeechText);
            runtimeState.CompleteSpeaking();
            runtimeState.RecordSpeechCandidateDecision("spoken", pipelineResult.Reason);
            return SpeechCandidateProcessingResult.Accepted(
                new SpeechCandidateResponse("accepted", "spoken", pipelineResult.Reason, 0));
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
