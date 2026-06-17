using System;
using System.Threading.Tasks;
using System.Windows.Media;

namespace CodeCompanionDesktop.Audio;

public sealed class AudioFilePlayer
{
    private static readonly TimeSpan PlaybackWarmupDuration = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan PlaybackRestartDelay = TimeSpan.FromMilliseconds(25);

    // A safety net, not a normal limit. A 500-character spoken opening is well
    // under a minute of audio; this only fires if the media stack stalls and
    // raises neither MediaEnded nor MediaFailed, which would otherwise hang the
    // playback task forever and wedge speech for the rest of the session.
    private static readonly TimeSpan DefaultPlaybackTimeout = TimeSpan.FromMinutes(2);

    private readonly TimeSpan playbackTimeout;

    public AudioFilePlayer()
        : this(DefaultPlaybackTimeout)
    {
    }

    internal AudioFilePlayer(TimeSpan playbackTimeout)
    {
        this.playbackTimeout = playbackTimeout;
    }

    public Task PlayAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var player = new MediaPlayer();

        var playbackStarted = false;
        var cleanedUp = false;
        EventHandler? endedHandler = null;
        EventHandler<ExceptionEventArgs>? failedHandler = null;
        EventHandler? openedHandler = null;

        void Cleanup()
        {
            if (cleanedUp)
            {
                return;
            }

            cleanedUp = true;

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

        // Cleanup touches the MediaPlayer, which is owned by the dispatcher
        // thread that created it; marshal the timeout cleanup back onto it.
        return AwaitPlaybackAsync(
            completion.Task,
            playbackTimeout,
            () => player.Dispatcher.InvokeAsync(Cleanup));
    }

    // Completes when the playback task does; otherwise faults with
    // TimeoutException after the timeout and runs onTimeout so the caller can
    // tear down a stalled player. Kept free of MediaPlayer so the watchdog is
    // unit-testable on its own.
    internal static async Task AwaitPlaybackAsync(Task completion, TimeSpan timeout, Action onTimeout)
    {
        try
        {
            await completion.WaitAsync(timeout);
        }
        catch (TimeoutException)
        {
            onTimeout();
            throw;
        }
    }
}
