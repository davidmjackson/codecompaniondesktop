using System;

namespace CodeCompanionDesktop.ElevenLabs;

public sealed class QuotaTracker
{
    private readonly object syncRoot = new();
    private QuotaSnapshot? current;

    public event EventHandler? StateChanged;

    public QuotaSnapshot? Snapshot
    {
        get
        {
            lock (syncRoot)
            {
                return current;
            }
        }
    }

    public void UpdateFromSubscription(ElevenLabsSubscription subscription, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        QuotaSnapshot next;
        lock (syncRoot)
        {
            next = new QuotaSnapshot(
                subscription.CharacterCount,
                subscription.CharacterLimit,
                DateTimeOffset.FromUnixTimeSeconds(subscription.NextCharacterCountResetUnix),
                subscription.Tier ?? string.Empty,
                asOf,
                QuotaSnapshotSource.Server);
            current = next;
        }

        RaiseStateChanged();
    }

    public void RestoreFromPersisted(QuotaSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (syncRoot)
        {
            current = snapshot;
        }

        RaiseStateChanged();
    }

    public void RecordSpentCharacters(int characters, DateTimeOffset asOf)
    {
        if (characters <= 0)
        {
            return;
        }

        bool changed = false;
        lock (syncRoot)
        {
            if (current is null)
            {
                return;
            }

            current = current with
            {
                CharacterCount = current.CharacterCount + characters,
                AsOf = asOf,
                Source = QuotaSnapshotSource.LocallyPredicted,
            };
            changed = true;
        }

        if (changed)
        {
            RaiseStateChanged();
        }
    }

    public void Clear()
    {
        bool changed = false;
        lock (syncRoot)
        {
            if (current is not null)
            {
                current = null;
                changed = true;
            }
        }

        if (changed)
        {
            RaiseStateChanged();
        }
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record QuotaSnapshot(
    long CharacterCount,
    long CharacterLimit,
    DateTimeOffset NextReset,
    string Tier,
    DateTimeOffset AsOf,
    QuotaSnapshotSource Source)
{
    public long Remaining => Math.Max(0, CharacterLimit - CharacterCount);

    public double FractionUsed => CharacterLimit > 0
        ? Math.Clamp((double)CharacterCount / CharacterLimit, 0d, 1d)
        : 0d;

    public int PercentUsed => (int)Math.Round(FractionUsed * 100);
}

public enum QuotaSnapshotSource
{
    Server,
    LocallyPredicted,
}
