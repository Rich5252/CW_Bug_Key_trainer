using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using CwTrainer.Serial;

namespace CwTrainer.Display
{
    /// <summary>
    /// Stacked 100% bar chart showing the distribution of element timing
    /// quality across Short-Warn / Good / Long-Warn bands, with a separate
    /// thin bar below each main bar showing the Bad sample rate.
    ///
    /// For each category (element role or character), the main bar always
    /// sums to 100% of NON-bad samples, keeping the warn/good/warn
    /// distribution readable regardless of how many bad samples exist.
    /// The bad rate bar answers "what proportion of all samples were
    /// outside the accept window entirely?" as a separate, distinct visual.
    ///
    /// Call SetData() with the list of categories to display.
    /// </summary>
    public sealed class ErrorRateChartControl : UserControl
    {
        private readonly Chart _chart;
        private readonly ChartArea _chartArea;
        private readonly Series _shortWarnSeries;
        private readonly Series _goodSeries;
        private readonly Series _longWarnSeries;
        private readonly Series _badSeries;

        private static readonly Color ShortWarnColor = Color.FromArgb(230, 180, 60);   // amber-left
        private static readonly Color GoodColor = Color.FromArgb(80, 200, 120);  // green
        private static readonly Color LongWarnColor = Color.FromArgb(230, 130, 40);   // deeper amber-right
        private static readonly Color BadColor = Color.FromArgb(220, 80, 70);   // red

        public ErrorRateChartControl()
        {
            _chart = new Chart { Dock = DockStyle.Fill };
            _chart.Click += (s, e) => OnClick(e);  // forward the child's click as this control's own Click

            // Single chart area - legend auto-sizes above it correctly,
            // same as ParetoChartControl. No explicit Position needed.
            _chartArea = new ChartArea("Main");
            _chartArea.AxisX.Interval = 1;
            _chartArea.AxisX.LabelStyle.Angle = -45;

            // Primary Y: 0-100%, for the stacked warn/good/warn bars
            _chartArea.AxisY.Minimum = 0;
            _chartArea.AxisY.Maximum = 100;
            _chartArea.AxisY.Title = "Distribution (%)";

            // Secondary Y: 0-100%, for the bad rate series
            _chartArea.AxisY2.Minimum = 0;
            _chartArea.AxisY2.Maximum = 100;
            _chartArea.AxisY2.Title = "Bad rate (%)";
            _chartArea.AxisY2.MajorGrid.Enabled = false;

            _chart.ChartAreas.Add(_chartArea);

            var legend = new Legend("L") { Docking = Docking.Top };
            _chart.Legends.Add(legend);

            // Stacked 100% bars on primary Y - short-warn / good / long-warn
            _shortWarnSeries = MakeStackedSeries("Short", ShortWarnColor, AxisType.Primary);
            _goodSeries = MakeStackedSeries("Good", GoodColor, AxisType.Primary);
            _longWarnSeries = MakeStackedSeries("Long", LongWarnColor, AxisType.Primary);

            // Bad rate as a line + diamond markers on secondary Y - same
            // pattern as ParetoChartControl's cumulative line, which is
            // proven to render cleanly over StackedColumn100 series.
            _badSeries = new Series("Bad %")
            {
                ChartType = SeriesChartType.Line,
                ChartArea = "Main",
                Legend = "L",
                YAxisType = AxisType.Secondary,
                Color = BadColor,
                BorderWidth = 2,
                MarkerStyle = MarkerStyle.Diamond,
                MarkerSize = 8,
                MarkerColor = BadColor,
                IsXValueIndexed = true,
            };

            _chart.Series.Add(_shortWarnSeries);
            _chart.Series.Add(_goodSeries);
            _chart.Series.Add(_longWarnSeries);
            _chart.Series.Add(_badSeries);

            Controls.Add(_chart);
        }

        private Series MakeStackedSeries(string name, Color color, AxisType yAxis)
        {
            return new Series(name)
            {
                ChartType = SeriesChartType.StackedColumn100,
                ChartArea = "Main",
                Legend = "L",
                YAxisType = yAxis,
                Color = color,
                IsXValueIndexed = true,
                IsValueShownAsLabel = false,
            };
        }

        public readonly struct ErrorRateEntry
        {
            public string Label { get; }
            public int ShortWarn { get; }
            public int Good { get; }
            public int LongWarn { get; }
            public int ShortBad { get; }
            public int LongBad { get; }
            public int Count { get; }

            /// <summary>Construct directly from a single StatBucket (for role-level entries).</summary>
            public ErrorRateEntry(string label, StatBucket bucket)
            {
                Label = label;
                ShortWarn = bucket.ShortWarn;
                Good = bucket.Good;
                LongWarn = bucket.LongWarn;
                ShortBad = bucket.ShortBad;
                LongBad = bucket.LongBad;
                Count = bucket.Count;
            }

            /// <summary>Construct from pre-summed band counts (for character-level entries that aggregate across multiple role buckets).</summary>
            public ErrorRateEntry(string label, int shortBad, int shortWarn, int good, int longWarn, int longBad, int count)
            {
                Label = label;
                ShortBad = shortBad;
                ShortWarn = shortWarn;
                Good = good;
                LongWarn = longWarn;
                LongBad = longBad;
                Count = count;
            }
        }

        /// <summary>
        /// Populates the chart from a list of entries. Entries are shown
        /// in the order provided - caller is responsible for sorting.
        /// </summary>
        public void SetData(List<ErrorRateEntry> entries)
        {
            _shortWarnSeries.Points.Clear();
            _goodSeries.Points.Clear();
            _longWarnSeries.Points.Clear();
            _badSeries.Points.Clear();

            if (entries == null || entries.Count == 0) return;

            // Accumulators for the totals column
            int totalShortWarn = 0, totalGood = 0, totalLongWarn = 0;
            int totalShortBad = 0, totalLongBad = 0, totalCount = 0;

            foreach (var entry in entries)
            {
                string label = $"{entry.Label} (n={entry.Count})";

                int acceptedCount = entry.ShortWarn + entry.Good + entry.LongWarn;
                int badCount = entry.ShortBad + entry.LongBad;

                double shortWarnPct = acceptedCount > 0 ? entry.ShortWarn * 100.0 / acceptedCount : 0;
                double goodPct = acceptedCount > 0 ? entry.Good * 100.0 / acceptedCount : 0;
                double longWarnPct = acceptedCount > 0 ? entry.LongWarn * 100.0 / acceptedCount : 0;
                double badPct = entry.Count > 0 ? badCount * 100.0 / entry.Count : 0;

                AddPoint(_shortWarnSeries, label, shortWarnPct);
                AddPoint(_goodSeries, label, goodPct);
                AddPoint(_longWarnSeries, label, longWarnPct);
                AddPoint(_badSeries, label, badPct);

                totalShortWarn += entry.ShortWarn;
                totalGood += entry.Good;
                totalLongWarn += entry.LongWarn;
                totalShortBad += entry.ShortBad;
                totalLongBad += entry.LongBad;
                totalCount += entry.Count;
            }

            // Totals column - same calculation as individual entries
            int totalAccepted = totalShortWarn + totalGood + totalLongWarn;
            int totalBad = totalShortBad + totalLongBad;
            string totalLabel = $"TOTAL (n={totalCount})";

            AddPoint(_shortWarnSeries, totalLabel, totalAccepted > 0 ? totalShortWarn * 100.0 / totalAccepted : 0);
            AddPoint(_goodSeries, totalLabel, totalAccepted > 0 ? totalGood * 100.0 / totalAccepted : 0);
            AddPoint(_longWarnSeries, totalLabel, totalAccepted > 0 ? totalLongWarn * 100.0 / totalAccepted : 0);
            AddPoint(_badSeries, totalLabel, totalCount > 0 ? totalBad * 100.0 / totalCount : 0);
        }

        private static void AddPoint(Series series, string label, double value)
        {
            int idx = series.Points.AddXY(label, Math.Round(value, 1));
            series.Points[idx].AxisLabel = label;
            series.Points[idx].ToolTip = $"{series.Name}: {value:F1}%";
        }
    }
}