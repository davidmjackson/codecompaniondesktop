using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using CodeCompanionDesktop.Audio;
using CodeCompanionDesktop.Bridge;
using CodeCompanionDesktop.Credentials;
using CodeCompanionDesktop.ElevenLabs;
using WpfApplication = System.Windows.Application;

namespace CodeCompanionDesktop;

public partial class MainWindow : Window
{
    private readonly TestTonePlayer testTonePlayer = new();
    private readonly AudioFilePlayer audioFilePlayer = new();
    private readonly WindowsCredentialStore credentialStore = new();
    private readonly BridgeTokenStore bridgeTokenStore;
    private readonly BridgeRuntimeState bridgeRuntimeState;
    private readonly ElevenLabsTextToSpeechClient textToSpeechClient = new();
    private bool isPlaying;

    public MainWindow(BridgeTokenStore bridgeTokenStore, BridgeRuntimeState bridgeRuntimeState)
    {
        this.bridgeTokenStore = bridgeTokenStore;
        this.bridgeRuntimeState = bridgeRuntimeState;
        InitializeComponent();
    }

    public bool AllowClose { get; set; }

    public async Task PlayTestSoundAsync()
    {
        if (isPlaying)
        {
            return;
        }

        isPlaying = true;
        SetPlaybackButtonsEnabled(false);
        StatusText.Text = "Playing generated local WAV test tone...";
        AudioPathText.Text = string.Empty;

        try
        {
            var path = await testTonePlayer.PlayAsync();
            StatusText.Text = "Playback completed.";
            AudioPathText.Text = path;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Playback failed.";
            AudioPathText.Text = ex.Message;
        }
        finally
        {
            isPlaying = false;
            SetPlaybackButtonsEnabled(true);
        }
    }

    public async Task PlayElevenLabsTestSpeechAsync()
    {
        await PlayElevenLabsSpeechAsync("Code Companion desktop speech test.", "ElevenLabs test");
    }

    public async Task PlayBridgeSpeechAsync(string text)
    {
        await PlayElevenLabsSpeechAsync(text, "bridge request");
        BridgeStatusText.Text = $"Last bridge request completed. Endpoint: {LocalBridgeServer.BaseUrl}";
    }

    public void SetBridgeStatus(string status)
    {
        BridgeStatusText.Text = status;
    }

    private async Task PlayElevenLabsSpeechAsync(string text, string source)
    {
        if (isPlaying)
        {
            throw new InvalidOperationException("Speech playback is already in progress.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Speech text cannot be empty.", nameof(text));
        }

        if (text.Length > 1000)
        {
            throw new ArgumentException("Speech text cannot be longer than 1000 characters.", nameof(text));
        }

        string? apiKey;
        try
        {
            apiKey = credentialStore.ReadSecret(WindowsCredentialStore.ElevenLabsApiKeyTarget)?.Trim();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Unable to read ElevenLabs API key.";
            AudioPathText.Text = ex.Message;
            return;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            StatusText.Text = "Save an ElevenLabs API key first.";
            AudioPathText.Text = string.Empty;
            return;
        }

        isPlaying = true;
        SetPlaybackButtonsEnabled(false);
        StatusText.Text = $"Generating ElevenLabs speech from {source}...";
        AudioPathText.Text = string.Empty;

        try
        {
            var path = await textToSpeechClient.CreateSpeechAsync(apiKey, text);
            StatusText.Text = $"Playing ElevenLabs speech from {source}...";
            AudioPathText.Text = path;
            await audioFilePlayer.PlayAsync(path);
            StatusText.Text = $"ElevenLabs playback completed from {source}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "ElevenLabs playback failed.";
            AudioPathText.Text = ex.Message;
        }
        finally
        {
            isPlaying = false;
            SetPlaybackButtonsEnabled(true);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        await PlayTestSoundAsync();
    }

    private async void PlayElevenLabsButton_Click(object sender, RoutedEventArgs e)
    {
        await PlayElevenLabsTestSpeechAsync();
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        if (WpfApplication.Current is App app)
        {
            app.ExitApplication();
        }
    }

    private void SaveApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        var apiKey = ElevenLabsApiKeyBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            CredentialStatusText.Text = "Enter an ElevenLabs API key before saving.";
            return;
        }

        try
        {
            credentialStore.SaveSecret(
                WindowsCredentialStore.ElevenLabsApiKeyTarget,
                "ElevenLabs",
                apiKey);
            CredentialStatusText.Text = $"Saved key to Windows Credential Manager ({DescribeSecret(apiKey)}).";
        }
        catch (Exception ex)
        {
            CredentialStatusText.Text = $"Save failed: {ex.Message}";
        }
    }

    private void LoadApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var apiKey = credentialStore.ReadSecret(WindowsCredentialStore.ElevenLabsApiKeyTarget);
            if (apiKey is null)
            {
                ElevenLabsApiKeyBox.Clear();
                CredentialStatusText.Text = "No saved ElevenLabs API key found.";
                return;
            }

            ElevenLabsApiKeyBox.Password = apiKey;
            CredentialStatusText.Text = $"Loaded saved key from Windows Credential Manager ({DescribeSecret(apiKey)}).";
        }
        catch (Exception ex)
        {
            CredentialStatusText.Text = $"Load failed: {ex.Message}";
        }
    }

    private void ClearApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var deleted = credentialStore.DeleteSecret(WindowsCredentialStore.ElevenLabsApiKeyTarget);
            ElevenLabsApiKeyBox.Clear();
            CredentialStatusText.Text = deleted
                ? "Deleted saved ElevenLabs API key."
                : "No saved ElevenLabs API key found.";
        }
        catch (Exception ex)
        {
            CredentialStatusText.Text = $"Clear failed: {ex.Message}";
        }
    }

    private void CopyBridgeTokenButton_Click(object sender, RoutedEventArgs e)
    {
        CopyBridgeTokenToClipboard();
    }

    private void RefreshBridgeStatusButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshBridgeStatus();
    }

    public void CopyBridgeTokenToClipboard()
    {
        try
        {
            System.Windows.Clipboard.SetText(bridgeTokenStore.EnsureToken());
            BridgeStatusText.Text = $"Copied bridge token. Endpoint: {LocalBridgeServer.BaseUrl}";
        }
        catch (Exception ex)
        {
            BridgeStatusText.Text = $"Copy token failed: {ex.Message}";
        }
    }

    private void RefreshBridgeStatus()
    {
        var speaking = bridgeRuntimeState.IsSpeaking ? "speaking" : "idle";
        BridgeStatusText.Text = $"Bridge listening on {LocalBridgeServer.BaseUrl}. State: {speaking}. {bridgeRuntimeState.LastStatus}";
    }

    private static string DescribeSecret(string secret)
    {
        return $"{secret.Length} characters";
    }

    private void SetPlaybackButtonsEnabled(bool isEnabled)
    {
        PlayButton.IsEnabled = isEnabled;
        PlayElevenLabsButton.IsEnabled = isEnabled;
    }
}
