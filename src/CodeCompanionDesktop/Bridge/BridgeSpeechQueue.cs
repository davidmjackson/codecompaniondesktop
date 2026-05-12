using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CodeCompanionDesktop.Bridge;

public sealed class BridgeSpeechQueue
{
    private readonly object syncRoot = new();
    private readonly Queue<string> requests = new();
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
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        lock (syncRoot)
        {
            if (requests.Count >= runtimeState.MaxQueuedSpeechRequests)
            {
                position = 0;
                runtimeState.RejectQueueFull();
                return false;
            }

            requests.Enqueue(text);
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
            string text;
            lock (syncRoot)
            {
                if (requests.Count == 0)
                {
                    isProcessing = false;
                    runtimeState.DequeueSpeechRequest(0);
                    return;
                }

                text = requests.Dequeue();
                runtimeState.DequeueSpeechRequest(requests.Count);
            }

            if (!runtimeState.TryBeginSpeaking())
            {
                await Task.Delay(250);

                lock (syncRoot)
                {
                    requests.Enqueue(text);
                    runtimeState.QueueSpeechRequest(requests.Count);
                }

                continue;
            }

            try
            {
                await speakAsync(text);
                runtimeState.CompleteSpeaking();
            }
            catch (Exception ex)
            {
                runtimeState.FailSpeaking(ex.Message);
            }
        }
    }
}
