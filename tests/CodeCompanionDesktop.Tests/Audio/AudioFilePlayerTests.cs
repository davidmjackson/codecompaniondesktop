using System;
using System.Threading.Tasks;
using CodeCompanionDesktop.Audio;

namespace CodeCompanionDesktop.Tests.Audio;

public sealed class AudioFilePlayerTests
{
    // A WPF MediaPlayer can silently raise neither MediaEnded nor MediaFailed
    // (asleep audio endpoint, missing codec). Without a watchdog the playback
    // task hangs forever and wedges the speaking latch for the whole session.
    [Fact]
    public async Task PlaybackThatNeverSignalsCompletionTimesOutAndCleansUp()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanedUp = false;

        await Assert.ThrowsAsync<TimeoutException>(() => AudioFilePlayer.AwaitPlaybackAsync(
            completion.Task,
            TimeSpan.FromMilliseconds(50),
            () => cleanedUp = true));

        Assert.True(cleanedUp);
    }

    [Fact]
    public async Task PlaybackThatSignalsCompletionDoesNotTriggerTheTimeoutCleanup()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timedOutCleanup = false;

        var awaiting = AudioFilePlayer.AwaitPlaybackAsync(
            completion.Task,
            TimeSpan.FromSeconds(30),
            () => timedOutCleanup = true);

        completion.SetResult();
        await awaiting;

        Assert.False(timedOutCleanup);
    }
}
