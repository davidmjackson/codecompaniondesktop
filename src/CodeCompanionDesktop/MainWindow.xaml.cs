using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using CodeCompanionDesktop.Audio;
using CodeCompanionDesktop.Credentials;
using WpfApplication = System.Windows.Application;

namespace CodeCompanionDesktop;

public partial class MainWindow : Window
{
    private readonly TestTonePlayer testTonePlayer = new();
    private readonly WindowsCredentialStore credentialStore = new();
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
        PlayButton.IsEnabled = false;
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
            PlayButton.IsEnabled = true;
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
}
