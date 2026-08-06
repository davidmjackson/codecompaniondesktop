using System;
using CodeCompanionDesktop.ElevenLabs;

namespace CodeCompanionDesktop.Tests.ElevenLabs;

public sealed class QuotaWarningPolicyTests
{
    private const long PeriodReset = 1_786_964_373;

    private static readonly QuotaForecast Warning =
        new(9_919d, new DateOnly(2026, 8, 10), 3_444d, ShouldWarn: true);

    private static readonly DateTimeOffset ResetLocal =
        new(2026, 8, 17, 12, 59, 33, TimeSpan.FromHours(1));

    [Fact]
    public void FiresWhenTheForecastWarnsAndNothingHasFiredYet()
    {
        Assert.True(QuotaWarningPolicy.ShouldFire(Warning, PeriodReset, null));
    }

    [Fact]
    public void DoesNotFireTwiceInTheSamePeriod()
    {
        Assert.False(QuotaWarningPolicy.ShouldFire(Warning, PeriodReset, PeriodReset));
    }

    [Fact]
    public void ReArmsWhenTheBillingPeriodChanges()
    {
        const long nextPeriod = 1_789_642_773;

        Assert.True(QuotaWarningPolicy.ShouldFire(Warning, nextPeriod, PeriodReset));
    }

    [Fact]
    public void DoesNotFireWhenTheForecastDoesNotWarn()
    {
        Assert.False(QuotaWarningPolicy.ShouldFire(QuotaForecast.None, PeriodReset, null));
    }

    [Fact]
    public void BalloonMessageNamesBothDatesAndTheAction()
    {
        var message = QuotaWarningPolicy.BuildBalloonMessage(Warning, ResetLocal);

        Assert.Contains("10 Aug", message, StringComparison.Ordinal);
        Assert.Contains("17 Aug", message, StringComparison.Ordinal);
        Assert.Contains("Top up", message, StringComparison.Ordinal);
    }

    [Fact]
    public void SpokenMessageFitsTheSpokenBudget()
    {
        var message = QuotaWarningPolicy.BuildSpokenMessage(Warning, ResetLocal);

        Assert.InRange(message.Length, 60, 160);
    }

    [Fact]
    public void SpokenMessageLeadsWithTheHeadline()
    {
        var message = QuotaWarningPolicy.BuildSpokenMessage(Warning, ResetLocal);

        // The engine speaks the opening of a message, so the action comes first.
        Assert.StartsWith("Voice credits", message, StringComparison.Ordinal);
    }

    [Fact]
    public void MessagesFallBackToTheResetDateWhenNoDryDateExists()
    {
        var noDryDate = new QuotaForecast(0d, null, 0d, ShouldWarn: true);

        var balloon = QuotaWarningPolicy.BuildBalloonMessage(noDryDate, ResetLocal);
        var spoken = QuotaWarningPolicy.BuildSpokenMessage(noDryDate, ResetLocal);

        Assert.Contains("soon", balloon, StringComparison.Ordinal);
        Assert.Contains("soon", spoken, StringComparison.Ordinal);
    }
}
