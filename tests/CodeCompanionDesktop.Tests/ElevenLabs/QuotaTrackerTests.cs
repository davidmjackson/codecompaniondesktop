using System;
using CodeCompanionDesktop.ElevenLabs;

namespace CodeCompanionDesktop.Tests.ElevenLabs;

public sealed class QuotaTrackerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SnapshotStartsNull()
    {
        var tracker = new QuotaTracker();
        Assert.Null(tracker.Snapshot);
    }

    [Fact]
    public void UpdateFromSubscriptionFillsSnapshotAndComputedFields()
    {
        var tracker = new QuotaTracker();
        var subscription = new ElevenLabsSubscription(
            CharacterCount: 8200,
            CharacterLimit: 10000,
            NextCharacterCountResetUnix: 1717200000,
            Tier: "creator");

        tracker.UpdateFromSubscription(subscription, Now);

        var snapshot = tracker.Snapshot;
        Assert.NotNull(snapshot);
        Assert.Equal(8200, snapshot!.CharacterCount);
        Assert.Equal(10000, snapshot.CharacterLimit);
        Assert.Equal(1800, snapshot.Remaining);
        Assert.Equal(0.82, snapshot.FractionUsed, 3);
        Assert.Equal(82, snapshot.PercentUsed);
        Assert.Equal("creator", snapshot.Tier);
        Assert.Equal(Now, snapshot.AsOf);
        Assert.Equal(QuotaSnapshotSource.Server, snapshot.Source);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1717200000), snapshot.NextReset);
    }

    [Fact]
    public void RecordSpentCharactersIncrementsAndMarksLocallyPredicted()
    {
        var tracker = new QuotaTracker();
        tracker.UpdateFromSubscription(
            new ElevenLabsSubscription(1000, 10000, 0, "free"),
            Now);

        tracker.RecordSpentCharacters(250, Now.AddMinutes(1));

        var snapshot = tracker.Snapshot!;
        Assert.Equal(1250, snapshot.CharacterCount);
        Assert.Equal(QuotaSnapshotSource.LocallyPredicted, snapshot.Source);
        Assert.Equal(Now.AddMinutes(1), snapshot.AsOf);
    }

    [Fact]
    public void RecordSpentCharactersNoopWhenSnapshotIsNull()
    {
        var tracker = new QuotaTracker();
        var fired = 0;
        tracker.StateChanged += (_, _) => fired++;

        tracker.RecordSpentCharacters(500, Now);

        Assert.Null(tracker.Snapshot);
        Assert.Equal(0, fired);
    }

    [Fact]
    public void RecordSpentCharactersNoopForZeroOrNegative()
    {
        var tracker = new QuotaTracker();
        tracker.UpdateFromSubscription(
            new ElevenLabsSubscription(1000, 10000, 0, "free"),
            Now);
        var beforeFired = 0;
        tracker.StateChanged += (_, _) => beforeFired++;

        tracker.RecordSpentCharacters(0, Now);
        tracker.RecordSpentCharacters(-50, Now);

        Assert.Equal(1000, tracker.Snapshot!.CharacterCount);
        Assert.Equal(0, beforeFired);
    }

    [Fact]
    public void RemainingNeverGoesNegative()
    {
        var tracker = new QuotaTracker();
        tracker.UpdateFromSubscription(
            new ElevenLabsSubscription(9500, 10000, 0, "free"),
            Now);

        tracker.RecordSpentCharacters(1000, Now);

        var snapshot = tracker.Snapshot!;
        Assert.Equal(10500, snapshot.CharacterCount);
        Assert.Equal(0, snapshot.Remaining);
        Assert.Equal(100, snapshot.PercentUsed);
        Assert.Equal(1.0, snapshot.FractionUsed);
    }

    [Fact]
    public void PercentUsedZeroWhenLimitIsZero()
    {
        var tracker = new QuotaTracker();
        tracker.UpdateFromSubscription(
            new ElevenLabsSubscription(123, 0, 0, "unknown"),
            Now);

        var snapshot = tracker.Snapshot!;
        Assert.Equal(0, snapshot.PercentUsed);
        Assert.Equal(0d, snapshot.FractionUsed);
        Assert.Equal(0, snapshot.Remaining);
    }

    [Fact]
    public void StateChangedFiresOnUpdateRestoreSpendAndClear()
    {
        var tracker = new QuotaTracker();
        var fired = 0;
        tracker.StateChanged += (_, _) => fired++;

        tracker.UpdateFromSubscription(
            new ElevenLabsSubscription(100, 1000, 0, "free"),
            Now);
        Assert.Equal(1, fired);

        tracker.RecordSpentCharacters(50, Now);
        Assert.Equal(2, fired);

        var persisted = tracker.Snapshot!;
        tracker.Clear();
        Assert.Equal(3, fired);

        tracker.RestoreFromPersisted(persisted);
        Assert.Equal(4, fired);

        tracker.Clear();
        Assert.Equal(5, fired);

        tracker.Clear();
        Assert.Equal(5, fired);
    }

    [Fact]
    public void RestoreFromPersistedSetsSnapshotExactly()
    {
        var tracker = new QuotaTracker();
        var snapshot = new QuotaSnapshot(
            CharacterCount: 4321,
            CharacterLimit: 10000,
            NextReset: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            Tier: "starter",
            AsOf: new DateTimeOffset(2026, 5, 19, 9, 0, 0, TimeSpan.Zero),
            Source: QuotaSnapshotSource.Server);

        tracker.RestoreFromPersisted(snapshot);

        Assert.Equal(snapshot, tracker.Snapshot);
    }
}
