namespace CodeCompanionDesktop.Bridge;

public sealed class BridgeRuntimeState
{
    private readonly object syncRoot = new();
    private bool isSpeaking;

    public bool IsSpeaking
    {
        get
        {
            lock (syncRoot)
            {
                return isSpeaking;
            }
        }
    }

    public string LastStatus { get; private set; } = "No bridge requests yet.";

    public bool TryBeginSpeaking()
    {
        lock (syncRoot)
        {
            if (isSpeaking)
            {
                LastStatus = "Bridge rejected request: speech already in progress.";
                return false;
            }

            isSpeaking = true;
            LastStatus = "Bridge speech request started.";
            return true;
        }
    }

    public void CompleteSpeaking()
    {
        lock (syncRoot)
        {
            isSpeaking = false;
            LastStatus = "Bridge speech request completed.";
        }
    }

    public void FailSpeaking(string error)
    {
        lock (syncRoot)
        {
            isSpeaking = false;
            LastStatus = $"Bridge speech request failed: {error}";
        }
    }
}
