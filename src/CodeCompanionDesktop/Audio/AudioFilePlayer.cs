using System;
using System.Threading.Tasks;
using System.Windows.Media;

namespace CodeCompanionDesktop.Audio;

public sealed class AudioFilePlayer
{
    public Task PlayAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var player = new MediaPlayer();

        EventHandler? endedHandler = null;
        EventHandler<ExceptionEventArgs>? failedHandler = null;

        void Cleanup()
        {
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
            Cleanup();
            completion.TrySetResult();
        };

        failedHandler = (_, e) =>
        {
            Cleanup();
            completion.TrySetException(e.ErrorException ?? new InvalidOperationException("Audio playback failed."));
        };

        player.MediaEnded += endedHandler;
        player.MediaFailed += failedHandler;
        player.Open(new Uri(path, UriKind.Absolute));
        player.Play();

        return completion.Task;
    }
}
