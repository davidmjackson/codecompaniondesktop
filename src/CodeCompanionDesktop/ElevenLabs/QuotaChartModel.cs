using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeCompanionDesktop.ElevenLabs;

public enum QuotaChartBarLevel
{
    Normal,
    Warning,
    Critical,
}

public sealed record QuotaChartBar(DateOnly Date, long Characters, QuotaChartBarLevel Level);

/// <summary>
/// The daily usage chart, computed without WPF so the geometry and the colour
/// thresholds are testable. MainWindow.QuotaGraph.cs turns this into a PlotModel.
///
/// Where the plan limit is unreadable there is no budget line, but the bars are
/// still true - usage needs only the text-to-speech key. Show what is known, omit
/// what is not.
/// </summary>
public sealed record QuotaChartModel(IReadOnlyList<QuotaChartBar> Bars, double? BudgetLine)
{
    public static readonly QuotaChartModel Empty =
        new(Array.Empty<QuotaChartBar>(), null);

    public static QuotaChartModel Create(
        IReadOnlyList<UsageDay> usageDays,
        double? survivalBudgetPerDay)
    {
        ArgumentNullException.ThrowIfNull(usageDays);

        var budget = survivalBudgetPerDay is > 0 ? survivalBudgetPerDay : null;

        var bars = usageDays
            .OrderBy(day => day.Date)
            .Select(day => new QuotaChartBar(day.Date, day.Characters, Classify(day.Characters, budget)))
            .ToList();

        return new QuotaChartModel(bars, budget);
    }

    /// <summary>
    /// At or over the survival budget is amber; double it is red. Same language as
    /// the quota bar's existing 70/90 thresholds: over budget means this day is
    /// spending the reset's headroom.
    /// </summary>
    private static QuotaChartBarLevel Classify(long characters, double? budget)
    {
        if (budget is not double line)
        {
            return QuotaChartBarLevel.Normal;
        }

        if (characters >= line * 2)
        {
            return QuotaChartBarLevel.Critical;
        }

        return characters >= line ? QuotaChartBarLevel.Warning : QuotaChartBarLevel.Normal;
    }
}
