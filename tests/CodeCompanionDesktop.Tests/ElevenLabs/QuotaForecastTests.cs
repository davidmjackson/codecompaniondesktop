using System;
using System.Collections.Generic;
using CodeCompanionDesktop.ElevenLabs;

namespace CodeCompanionDesktop.Tests.ElevenLabs;

public sealed class QuotaForecastTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 6, 10, 30, 0, TimeSpan.Zero);

    private static QuotaSnapshot Snapshot(long used = 157_404, long limit = 195_494)
    {
        return new QuotaSnapshot(
            used,
            limit,
            new DateTimeOffset(2026, 8, 17, 11, 59, 33, TimeSpan.Zero),
            "creator",
            Now,
            QuotaSnapshotSource.Server);
    }

    // The seven complete days before 2026-08-06, taken from real API data.
    private static List<UsageDay> SevenRealDays()
    {
        return new List<UsageDay>
        {
            new(new DateOnly(2026, 7, 30), 10_201),
            new(new DateOnly(2026, 7, 31), 13_435),
            new(new DateOnly(2026, 8, 1), 6_267),
            new(new DateOnly(2026, 8, 2), 8_394),
            new(new DateOnly(2026, 8, 3), 7_383),
            new(new DateOnly(2026, 8, 4), 7_535),
            new(new DateOnly(2026, 8, 5), 16_216),
        };
    }

    [Fact]
    public void BurnRateIsTheMeanOfTheTrailingCompleteDays()
    {
        var forecast = QuotaForecast.Create(SevenRealDays(), Snapshot(), Now);

        // 69,431 / 7
        Assert.Equal(9918.71, forecast.BurnRatePerDay, 2);
    }

    [Fact]
    public void TodaysPartialBucketIsExcludedFromTheBurnRate()
    {
        var days = SevenRealDays();
        days.Add(new UsageDay(new DateOnly(2026, 8, 6), 1_806));

        var forecast = QuotaForecast.Create(days, Snapshot(), Now);

        // Unchanged: today's low partial figure must not drag the mean down.
        Assert.Equal(9918.71, forecast.BurnRatePerDay, 2);
    }

    [Fact]
    public void OnlyTheMostRecentSevenCompleteDaysCount()
    {
        var days = new List<UsageDay> { new(new DateOnly(2026, 7, 29), 1_000_000) };
        days.AddRange(SevenRealDays());

        var forecast = QuotaForecast.Create(days, Snapshot(), Now);

        Assert.Equal(9918.71, forecast.BurnRatePerDay, 2);
    }

    [Fact]
    public void ZeroDaysAreIncludedInTheMean()
    {
        var days = new List<UsageDay>
        {
            new(new DateOnly(2026, 8, 3), 0),
            new(new DateOnly(2026, 8, 4), 0),
            new(new DateOnly(2026, 8, 5), 300),
        };

        var forecast = QuotaForecast.Create(days, Snapshot(), Now);

        Assert.Equal(100d, forecast.BurnRatePerDay, 2);
    }

    [Fact]
    public void WarnsWhenProjectedDryDateFallsBeforeTheReset()
    {
        var forecast = QuotaForecast.Create(SevenRealDays(), Snapshot(), Now);

        Assert.True(forecast.ShouldWarn);
        Assert.Equal(new DateOnly(2026, 8, 10), forecast.ProjectedDryDate);
    }

    [Fact]
    public void DoesNotWarnWhenTheProjectionReachesTheReset()
    {
        // 500/day against 38,090 remaining lasts far beyond 17 Aug.
        var days = new List<UsageDay>
        {
            new(new DateOnly(2026, 8, 3), 500),
            new(new DateOnly(2026, 8, 4), 500),
            new(new DateOnly(2026, 8, 5), 500),
        };

        var forecast = QuotaForecast.Create(days, Snapshot(), Now);

        Assert.False(forecast.ShouldWarn);
        Assert.NotNull(forecast.ProjectedDryDate);
    }

    [Fact]
    public void SurvivalBudgetIsRemainingOverDaysUntilReset()
    {
        var forecast = QuotaForecast.Create(SevenRealDays(), Snapshot(), Now);

        // 38,090 remaining over 11.0621875 days = 3443.26
        Assert.Equal(3443d, forecast.SurvivalBudgetPerDay, 0);
    }

    [Fact]
    public void FewerThanThreeCompleteDaysProducesNoWarning()
    {
        var days = new List<UsageDay>
        {
            new(new DateOnly(2026, 8, 4), 20_000),
            new(new DateOnly(2026, 8, 5), 20_000),
        };

        var forecast = QuotaForecast.Create(days, Snapshot(), Now);

        Assert.False(forecast.ShouldWarn);
        Assert.Null(forecast.ProjectedDryDate);
        Assert.Equal(0d, forecast.BurnRatePerDay);
    }

    [Fact]
    public void NoUsageDataProducesNoWarning()
    {
        var forecast = QuotaForecast.Create(Array.Empty<UsageDay>(), Snapshot(), Now);

        Assert.False(forecast.ShouldWarn);
        Assert.Null(forecast.ProjectedDryDate);
    }

    [Fact]
    public void ZeroLimitProducesNoWarning()
    {
        var forecast = QuotaForecast.Create(SevenRealDays(), Snapshot(limit: 0), Now);

        Assert.False(forecast.ShouldWarn);
        Assert.Null(forecast.ProjectedDryDate);
    }

    [Fact]
    public void ZeroBurnProducesNoDryDateAndNoWarning()
    {
        var days = new List<UsageDay>
        {
            new(new DateOnly(2026, 8, 3), 0),
            new(new DateOnly(2026, 8, 4), 0),
            new(new DateOnly(2026, 8, 5), 0),
        };

        var forecast = QuotaForecast.Create(days, Snapshot(), Now);

        Assert.Equal(0d, forecast.BurnRatePerDay);
        Assert.Null(forecast.ProjectedDryDate);
        Assert.False(forecast.ShouldWarn);
    }

    [Fact]
    public void AResetDateInThePastProducesNoWarning()
    {
        var stale = new QuotaSnapshot(
            157_404,
            195_494,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            "creator",
            Now,
            QuotaSnapshotSource.Server);

        var forecast = QuotaForecast.Create(SevenRealDays(), stale, Now);

        Assert.False(forecast.ShouldWarn);
    }

    [Fact]
    public void AnExhaustedQuotaStillWarnsBecauseTheActionIsStillToTopUp()
    {
        // Remaining is 0, so the runway is 0 and the dry date is today - which is
        // before the reset, so it warns. That is deliberate: at zero the required
        // action ("buy credits") is exactly what it is at 10%. The spoken warning
        // is separately suppressed here by the headroom check in
        // MainWindow.QuotaWarning.cs, because there are no credits left to speak it.
        var forecast = QuotaForecast.Create(SevenRealDays(), Snapshot(used: 195_494), Now);

        Assert.True(forecast.ShouldWarn);
        Assert.Equal(new DateOnly(2026, 8, 6), forecast.ProjectedDryDate);
    }
}
