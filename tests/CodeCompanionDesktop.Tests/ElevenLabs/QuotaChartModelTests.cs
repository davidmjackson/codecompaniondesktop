using System;
using System.Collections.Generic;
using CodeCompanionDesktop.ElevenLabs;

namespace CodeCompanionDesktop.Tests.ElevenLabs;

public sealed class QuotaChartModelTests
{
    private static List<UsageDay> Days()
    {
        return new List<UsageDay>
        {
            new(new DateOnly(2026, 8, 3), 3_000),
            new(new DateOnly(2026, 8, 4), 5_000),
            new(new DateOnly(2026, 8, 5), 16_216),
        };
    }

    [Fact]
    public void BarsKeepDateOrderAndCharacters()
    {
        var model = QuotaChartModel.Create(Days(), 5_000d);

        Assert.Equal(3, model.Bars.Count);
        Assert.Equal(new DateOnly(2026, 8, 3), model.Bars[0].Date);
        Assert.Equal(16_216, model.Bars[2].Characters);
    }

    [Fact]
    public void BarsAreSortedByDateEvenWhenTheSeriesIsNot()
    {
        var unsorted = new List<UsageDay>
        {
            new(new DateOnly(2026, 8, 5), 1),
            new(new DateOnly(2026, 8, 3), 2),
        };

        var model = QuotaChartModel.Create(unsorted, null);

        Assert.Equal(new DateOnly(2026, 8, 3), model.Bars[0].Date);
    }

    [Fact]
    public void BelowBudgetIsNormalAtOrAboveIsWarningDoubleIsCritical()
    {
        var model = QuotaChartModel.Create(Days(), 5_000d);

        Assert.Equal(QuotaChartBarLevel.Normal, model.Bars[0].Level);
        Assert.Equal(QuotaChartBarLevel.Warning, model.Bars[1].Level);
        Assert.Equal(QuotaChartBarLevel.Critical, model.Bars[2].Level);
    }

    [Fact]
    public void EveryBarIsNormalWhenThereIsNoBudgetLine()
    {
        var model = QuotaChartModel.Create(Days(), null);

        Assert.All(model.Bars, bar => Assert.Equal(QuotaChartBarLevel.Normal, bar.Level));
        Assert.Null(model.BudgetLine);
    }

    [Fact]
    public void ANonPositiveBudgetIsTreatedAsNoBudgetLine()
    {
        var model = QuotaChartModel.Create(Days(), 0d);

        Assert.Null(model.BudgetLine);
        Assert.All(model.Bars, bar => Assert.Equal(QuotaChartBarLevel.Normal, bar.Level));
    }

    [Fact]
    public void AnEmptySeriesProducesNoBars()
    {
        var model = QuotaChartModel.Create(Array.Empty<UsageDay>(), 5_000d);

        Assert.Empty(model.Bars);
    }

    [Fact]
    public void EmptyHasNoBarsAndNoBudgetLine()
    {
        Assert.Empty(QuotaChartModel.Empty.Bars);
        Assert.Null(QuotaChartModel.Empty.BudgetLine);
    }
}
