using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using CodeCompanionDesktop.ElevenLabs;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace CodeCompanionDesktop;

/// <summary>
/// Draws the daily usage chart. Geometry and colours come from QuotaChartModel,
/// which is tested without WPF; this file only turns that into a PlotModel.
/// </summary>
public partial class MainWindow
{
    private static readonly OxyColor NormalBarColor = OxyColor.FromRgb(60, 179, 113);
    private static readonly OxyColor WarningBarColor = OxyColor.FromRgb(218, 165, 32);
    private static readonly OxyColor CriticalBarColor = OxyColor.FromRgb(205, 92, 92);

    /// <summary>Half a column's width, in category units.</summary>
    private const double ColumnHalfWidth = 0.35;

    private void RenderQuotaGraph(QuotaChartModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.Bars.Count == 0)
        {
            QuotaUsagePlot.Visibility = Visibility.Collapsed;
            QuotaUsagePlot.Model = null;
            return;
        }

        var plot = new PlotModel
        {
            PlotAreaBorderThickness = new OxyThickness(0, 0, 0, 1),
            PlotAreaBorderColor = OxyColors.LightGray,
        };

        plot.Axes.Add(BuildDayAxis(model));
        plot.Axes.Add(BuildValueAxis());
        plot.Series.Add(BuildBarSeries(model));
        AddBudgetLine(plot, model.BudgetLine);

        QuotaUsagePlot.Model = plot;
        QuotaUsagePlot.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// A LinearAxis, deliberately NOT a CategoryAxis. RectangleBarSeries is a plain
    /// XY series: it does not consume CategoryAxis items the way BarSeries does, so
    /// pairing the two leaves the axis auto-ranging independently of the bars. On
    /// real data that rendered the columns bunched into a fraction of the plot with
    /// every date label overprinted. Indices map back to dates through the
    /// formatter instead.
    /// </summary>
    private static LinearAxis BuildDayAxis(QuotaChartModel model)
    {
        var labels = model.Bars
            .Select(bar => bar.Date.ToString("d MMM", CultureInfo.CurrentCulture))
            .ToList();

        // At most six labels: a full billing period is ~31 columns, and every
        // date printed is an unreadable smear.
        var step = Math.Max(1, (int)Math.Ceiling(labels.Count / 6d));

        return new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Minimum = -0.5,
            Maximum = Math.Max(0.5, labels.Count - 0.5),
            MajorStep = step,
            MinorStep = step,
            MajorGridlineStyle = LineStyle.None,
            MinorTickSize = 0,
            LabelFormatter = value =>
            {
                var index = (int)Math.Round(value);
                return index >= 0 && index < labels.Count ? labels[index] : string.Empty;
            },
        };
    }

    private static LinearAxis BuildValueAxis()
    {
        return new LinearAxis
        {
            Position = AxisPosition.Left,
            Minimum = 0,

            // AbsoluteMinimum as well as Minimum: without it the axis auto-ranged
            // symmetrically to -20,000, wasting half the plot on impossible values.
            AbsoluteMinimum = 0,
            MinimumPadding = 0,
            StringFormat = "#,0",
            MajorGridlineStyle = LineStyle.Dot,
        };
    }

    /// <summary>
    /// RectangleBarSeries. OxyPlot 2.2.0 ships no ColumnSeries (it was a 1.x type),
    /// and its BarSeries is horizontal-only: BarSeriesBase.GetCategoryAxis() throws
    /// "BarSeries requires a CategoryAxis on the Y Axis", which PlotModel.Update
    /// swallows into GetLastPlotException and the view paints as
    /// "OxyPlot exception: ..." in place of the chart - no build error, a blank
    /// chart at runtime. RectangleBarSeries draws vertical columns against a bottom
    /// CategoryAxis and its items carry a per-item Color, which the threshold
    /// palette needs.
    /// </summary>
    private static RectangleBarSeries BuildBarSeries(QuotaChartModel model)
    {
        var series = new RectangleBarSeries
        {
            FillColor = NormalBarColor,
            StrokeThickness = 0,
            TrackerFormatString = "{Title}: {Y1:#,0} characters",

            // RectangleBarSeries draws each item's Title onto the bar itself. With
            // one bar per day that overprints the columns into an unreadable smear,
            // and the axis already carries the dates. Transparent hides the drawn
            // label while leaving Title available to the hover tracker.
            TextColor = OxyColors.Transparent,
        };

        for (var index = 0; index < model.Bars.Count; index++)
        {
            var bar = model.Bars[index];
            series.Items.Add(new RectangleBarItem(
                index - ColumnHalfWidth,
                0,
                index + ColumnHalfWidth,
                bar.Characters)
            {
                Color = ColorFor(bar.Level),
                Title = bar.Date.ToString("d MMM", CultureInfo.CurrentCulture),
            });
        }

        return series;
    }

    private static void AddBudgetLine(PlotModel plot, double? budgetLine)
    {
        // No limit means no denominator, so there is no budget to draw. The bars
        // are still true, and are left alone.
        if (budgetLine is not double budget)
        {
            return;
        }

        plot.Annotations.Add(new LineAnnotation
        {
            Type = LineAnnotationType.Horizontal,
            Y = budget,
            Color = OxyColors.SteelBlue,
            LineStyle = LineStyle.Dash,
            Text = $"Budget {budget:#,0}/day",
            TextColor = OxyColors.SteelBlue,
        });
    }

    private static OxyColor ColorFor(QuotaChartBarLevel level)
    {
        return level switch
        {
            QuotaChartBarLevel.Critical => CriticalBarColor,
            QuotaChartBarLevel.Warning => WarningBarColor,
            _ => NormalBarColor,
        };
    }
}
