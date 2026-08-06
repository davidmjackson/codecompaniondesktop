using System;
using System.Globalization;
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

        plot.Axes.Add(BuildCategoryAxis(model));
        plot.Axes.Add(BuildValueAxis());
        plot.Series.Add(BuildBarSeries(model));
        AddBudgetLine(plot, model.BudgetLine);

        QuotaUsagePlot.Model = plot;
        QuotaUsagePlot.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Labels are added explicitly rather than via ItemsSource/LabelField:
    /// DateOnly does not format reliably through the axis StringFormat path.
    /// </summary>
    private static CategoryAxis BuildCategoryAxis(QuotaChartModel model)
    {
        var axis = new CategoryAxis
        {
            Position = AxisPosition.Bottom,
            IsTickCentered = true,
            GapWidth = 0.3,
        };

        foreach (var bar in model.Bars)
        {
            axis.Labels.Add(bar.Date.ToString("d MMM", CultureInfo.CurrentCulture));
        }

        return axis;
    }

    private static LinearAxis BuildValueAxis()
    {
        return new LinearAxis
        {
            Position = AxisPosition.Left,
            Minimum = 0,
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
