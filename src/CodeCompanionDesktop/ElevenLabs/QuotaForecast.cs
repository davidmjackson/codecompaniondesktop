using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeCompanionDesktop.ElevenLabs;

/// <summary>
/// Projects when the ElevenLabs quota runs out, and decides whether that is soon
/// enough to warn about.
///
/// The warning is a PURCHASING trigger, not a slow-down trigger: this account
/// cannot extend its own character limit, so reaching it stops speech dead rather
/// than billing overage. See docs/elevenlabs-credit-early-warning-spec.md.
///
/// Pure by design - no clock, no HTTP, no WPF - so every rule below is testable.
/// </summary>
public sealed record QuotaForecast(
    double BurnRatePerDay,
    DateOnly? ProjectedDryDate,
    double SurvivalBudgetPerDay,
    bool ShouldWarn)
{
    /// <summary>
    /// Seven days spans a whole week, so quiet weekend days are represented in
    /// proportion. Daily spend has ranged from 0 to 16,216 within one billing
    /// period; a shorter window makes the warning flap.
    /// </summary>
    public const int BurnRateWindowDays = 7;

    /// <summary>
    /// Below this, a mean is noise rather than a rate. Reached immediately after
    /// a reset, when warning on one day's spend would be meaningless.
    /// </summary>
    public const int MinimumCompleteDays = 3;

    public static readonly QuotaForecast None = new(0d, null, 0d, false);

    public static QuotaForecast Create(
        IReadOnlyList<UsageDay> usageDays,
        QuotaSnapshot snapshot,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(usageDays);
        ArgumentNullException.ThrowIfNull(snapshot);

        var daysUntilReset = (snapshot.NextReset - now).TotalDays;
        if (snapshot.CharacterLimit <= 0 || daysUntilReset <= 0)
        {
            return None;
        }

        var burnRate = CalculateBurnRate(usageDays, DateOnly.FromDateTime(now.UtcDateTime));
        var survivalBudget = snapshot.Remaining / daysUntilReset;

        if (burnRate <= 0)
        {
            return new QuotaForecast(0d, null, survivalBudget, false);
        }

        var runwayDays = snapshot.Remaining / burnRate;
        var dryInstant = now.AddDays(runwayDays);

        return new QuotaForecast(
            burnRate,
            DateOnly.FromDateTime(dryInstant.UtcDateTime),
            survivalBudget,
            dryInstant < snapshot.NextReset);
    }

    /// <summary>
    /// Mean characters per day over the trailing complete days. Today is excluded:
    /// its bucket is a partial day, and including it drags the mean down and fires
    /// the warning late.
    /// </summary>
    private static double CalculateBurnRate(IReadOnlyList<UsageDay> usageDays, DateOnly today)
    {
        var completeDays = usageDays
            .Where(day => day.Date < today)
            .OrderBy(day => day.Date)
            .TakeLast(BurnRateWindowDays)
            .ToList();

        if (completeDays.Count < MinimumCompleteDays)
        {
            return 0d;
        }

        return completeDays.Average(day => (double)day.Characters);
    }
}
