using System;
using System.Threading.Tasks;
using System.Windows.Media;

namespace CodeCompanionDesktop.Audio;

public sealed class AudioFilePlayer
{
    private static readonly TimeSpan PlaybackWarmupDuration = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan PlaybackRestartDelay = TimeSpan.FromMilliseconds(25);

    public Task PlayAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var player = new MediaPlayer();

        var playbackStarted = false;
        EventHandler? endedHandler = null;
        EventHandler<ExceptionEventArgs>? failedHandler = null;
        EventHandler? openedHandler = null;

        void Cleanup()
        {
            if (openedHandler is not null)
            {
                player.MediaOpened -= openedHandler;
            }

            if (endedHandler is not null)
            {
                player.MediaEnded -= endedHandler;
            }

            if (failedHandler is not null)
            {
                player.MediaFailed -= failedHandler;
            }

            player.Close();
        }

        endedHandler = (_, _) =>
        {
            if (!playbackStarted)
            {
                return;
            }

            Cleanup();
            completion.TrySetResult();
        };

        failedHandler = (_, e) =>
        {
            Cleanup();
            completion.TrySetException(e.ErrorException ?? new InvalidOperationException("Audio playback failed."));
        };

        openedHandler = async (_, _) =>
        {
            try
            {
                // Warming the same player prevents the first phoneme being clipped while Windows audio wakes up.
                var playbackVolume = player.Volume;
                player.Volume = 0;
                player.Play();

                await Task.Delay(PlaybackWarmupDuration);

                player.Pause();
                player.Position = TimeSpan.Zero;
                await Task.Delay(PlaybackRestartDelay);

                playbackStarted = true;
                player.Volume = playbackVolume;
                player.Play();
            }
            catch (Exception ex)
            {
                Cleanup();
                completion.TrySetException(ex);
            }
        };

        player.MediaOpened += openedHandler;
        player.MediaEnded += endedHandler;
        player.MediaFailed += failedHandler;
        player.Open(new Uri(path, UriKind.Absolute));

        return completion.Task;
    }
}
