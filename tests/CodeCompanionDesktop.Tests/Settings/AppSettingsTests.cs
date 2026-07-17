using CodeCompanionDesktop.Settings;

namespace CodeCompanionDesktop.Tests.Settings;

public sealed class AppSettingsTests
{
    [Fact]
    public void NormalizeAddsDefaultSpeechProviderSettings()
    {
        var settings = new AppSettings
        {
            SpeechProvider = "",
            ElevenLabsVoiceId = "",
            ElevenLabsModelId = "",
            ElevenLabsOutputFormat = "",
            MaxQueuedBridgeSpeechRequests = 99
        };

        settings.Normalize();

        Assert.Equal(AppSettings.ElevenLabsProvider, settings.SpeechProvider);
        Assert.Equal(AppSettings.DefaultElevenLabsVoiceId, settings.ElevenLabsVoiceId);
        Assert.Equal(AppSettings.DefaultElevenLabsModelId, settings.ElevenLabsModelId);
        Assert.Equal(AppSettings.DefaultElevenLabsOutputFormat, settings.ElevenLabsOutputFormat);
        Assert.Equal(AppSettings.MaxQueuedBridgeSpeechRequestLimit, settings.MaxQueuedBridgeSpeechRequests);
    }

    [Fact]
    public void NormalizeTrimsConfiguredElevenLabsValues()
    {
        var settings = new AppSettings
        {
            SpeechProvider = " elevenlabs ",
            ElevenLabsVoiceId = " voice-id ",
            ElevenLabsModelId = " model-id ",
            ElevenLabsOutputFormat = " output-format "
        };

        settings.Normalize();

        Assert.Equal(AppSettings.ElevenLabsProvider, settings.SpeechProvider);
        Assert.Equal("voice-id", settings.ElevenLabsVoiceId);
        Assert.Equal("model-id", settings.ElevenLabsModelId);
        Assert.Equal("output-format", settings.ElevenLabsOutputFormat);
    }

    [Fact]
    public void NewAppSettingsDefaultSelfTestPlaybackToSpeak()
    {
        Assert.Equal(AppSettings.SelfTestPlaybackSpeak, new AppSettings().SelfTestPlayback);
    }

    [Fact]
    public void NormalizeKeepsSilentSelfTestPlayback()
    {
        var settings = new AppSettings { SelfTestPlayback = " silent " };

        settings.Normalize();

        Assert.Equal(AppSettings.SelfTestPlaybackSilent, settings.SelfTestPlayback);
    }

    [Fact]
    public void NormalizeFallsBackToSpeakForAnUnknownSelfTestPlaybackValue()
    {
        var settings = new AppSettings { SelfTestPlayback = "nonsense" };

        settings.Normalize();

        Assert.Equal(AppSettings.SelfTestPlaybackSpeak, settings.SelfTestPlayback);
    }
}
