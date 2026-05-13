using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CodeCompanionDesktop.Bridge;

public sealed class BridgeSpeechQueue
{
    private readonly object syncRoot = new();
    private readonly Queue<QueuedSpeechRequest> requests = new();
    private readonly Func<string, Task> speakAsync;
    private readonly BridgeRuntimeState runtimeState;
    private bool isProcessing;

    public BridgeSpeechQueue(Func<string, Task> speakAsync, BridgeRuntimeState runtimeState)
    {
        this.speakAsync = speakAsync;
        this.runtimeState = runtimeState;
    }

    public bool TryEnqueue(string text, out int position)
    {
        return TryEnqueue(text, null, out position);
    }

    public bool TryEnqueue(string text, Func<Exception?, Task>? completeAsync, out int position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        lock (syncRoot)
        {
            if (requests.Count >= runtimeState.MaxQueuedSpeechRequests)
            {
                position = 0;
                runtimeState.RejectQueueFull();
                return false;
            }

            requests.Enqueue(new QueuedSpeechRequest(text, completeAsync));
            position = requests.Count;
            runtimeState.QueueSpeechRequest(requests.Count);

            if (!isProcessing)
            {
                isProcessing = true;
                _ = Task.Run(ProcessAsync);
            }

            return true;
        }
    }

    private async Task ProcessAsync()
    {
        while (true)
        {
            QueuedSpeechRequest request;
            lock (syncRoot)
            {
                if (requests.Count == 0)
                {
                    isProcessing = false;
                    runtimeState.DequeueSpeechRequest(0);
                    return;
                }

                request = requests.Dequeue();
                runtimeState.DequeueSpeechRequest(requests.Count);
            }

            if (!runtimeState.TryBeginSpeaking())
            {
                await Task.Delay(250);

                lock (syncRoot)
                {
                    requests.Enqueue(request);
                    runtimeState.QueueSpeechRequest(requests.Count);
                }

                continue;
            }

            try
            {
                await speakAsync(request.Text);
                runtimeState.CompleteSpeaking();
                await request.CompleteAsync(null);
            }
            catch (Exception ex)
            {
                runtimeState.FailSpeaking(ex.Message);
                await request.CompleteAsync(ex);
            }
        }
    }

    private sealed record QueuedSpeechRequest(string Text, Func<Exception?, Task>? OnCompleteAsync)
    {
        public Task CompleteAsync(Exception? exception)
        {
            return OnCompleteAsync?.Invoke(exception) ?? Task.CompletedTask;
        }
    }
}
