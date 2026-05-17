namespace CodeCompanionDesktop.Bridge;

public enum SpeechProfile
{
    Standard,
    Demo
}

public sealed class SpeechProfileState
{
    private readonly object syncRoot = new();
    private SpeechProfile activeProfile = SpeechProfile.Standard;
    private string lastProfileChange = "Speech profile is Standard.";

    public SpeechProfile ActiveProfile
    {
        get
        {
            lock (syncRoot)
            {
                return activeProfile;
            }
        }
    }

    public string ActiveProfileName
    {
        get
        {
            lock (syncRoot)
            {
                return activeProfile.ToString();
            }
        }
    }

    public string LastProfileChange
    {
        get
        {
            lock (syncRoot)
            {
                return lastProfileChange;
            }
        }
    }

    public void EnableDemoMode()
    {
        lock (syncRoot)
        {
            activeProfile = SpeechProfile.Demo;
            lastProfileChange = "Demo Mode enabled for this Desktop session.";
        }
    }

    public void DisableDemoMode()
    {
        lock (syncRoot)
        {
            activeProfile = SpeechProfile.Standard;
            lastProfileChange = "Standard speech policy restored.";
        }
    }

    public bool IsDemoModeActive()
    {
        lock (syncRoot)
        {
            return activeProfile == SpeechProfile.Demo;
        }
    }
}
