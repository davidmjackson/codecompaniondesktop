using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using CodeCompanionDesktop.Audio;
using CodeCompanionDesktop.Credentials;
using CodeCompanionDesktop.ElevenLabs;
using WpfApplication = System.Windows.Application;

namespace CodeCompanionDesktop;

public partial class MainWindow : Window
{
    private readonly TestTonePlayer testTonePlayer = new();
    private readonly AudioFilePlayer audioFilePlayer = new();
    private readonly WindowsCredentialStore credentialStore = new();
    private readonly ElevenLabsTextToSpeechClient textToSpeechClient = new();
    private bool isPlaying;

    public MainWindow()
    {
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
        if (isPlaying)
        {
            return;
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
        StatusText.Text = "Generating ElevenLabs test speech...";
        AudioPathText.Text = string.Empty;

        try
        {
            var path = await textToSpeechClient.CreateTestSpeechAsync(apiKey);
            StatusText.Text = "Playing ElevenLabs test speech...";
            AudioPathText.Text = path;
            await audioFilePlayer.PlayAsync(path);
            StatusText.Text = "ElevenLabs playback completed.";
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
