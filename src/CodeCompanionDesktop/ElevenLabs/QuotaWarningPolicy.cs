using System;

namespace CodeCompanionDesktop.ElevenLabs;

/// <summary>
/// Decides whether the credit warning fires, and writes its wording.
///
/// The warning fires once per billing period. It is keyed on the period's reset
/// timestamp so a new period re-arms it automatically: no expiry logic, and a
/// restart mid-period does not warn again.
/// </summary>
public static class QuotaWarningPolicy
{
    public static bool ShouldFire(
        QuotaForecast forecast,
        long periodResetUnix,
        long? lastWarnedPeriodResetUnix)
    {
        ArgumentNullException.ThrowIfNull(forecast);

        return forecast.ShouldWarn && lastWarnedPeriodResetUnix != periodResetUnix;
    }

    public static string BuildBalloonMessage(QuotaForecast forecast, DateTimeOffset resetLocal)
    {
        ArgumentNullException.ThrowIfNull(forecast);

        return $"Credits run out {DescribeDryDate(forecast)}, before your "
            + $"{resetLocal:d MMM} reset. Top up to avoid speech stopping.";
    }

    public static string BuildSpokenMessage(QuotaForecast forecast, DateTimeOffset resetLocal)
    {
        ArgumentNullException.ThrowIfNull(forecast);

        // Front-loaded: the engine speaks the opening of a message, so the
        // headline and the action come before the detail.
        return $"Voice credits run out {DescribeDryDate(forecast)}, before the "
            + $"{resetLocal:d MMMM} reset. Top up when you can.";
    }

    private static string DescribeDryDate(QuotaForecast forecast)
    {
        return forecast.ProjectedDryDate is DateOnly dry
            ? $"around {dry:d MMMM}"
            : "soon";
    }
}
