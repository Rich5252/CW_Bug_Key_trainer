using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using CwTrainer.Serial;

namespace CwTrainer.Display
{
    /// <summary>
    /// Bar + cumulative-line Pareto chart, wrapping the WinForms.DataVisualization
    /// Chart control. Call SetData() with a sorted List&lt;ParetoEntry&gt;
    /// (from ParetoDataBuilder.BuildByCharacter/BuildByRole) to populate it.
    ///
    /// Bars use the primary Y axis (the metric, as a percentage); the
    /// cumulative-percentage line uses the secondary Y axis (always 0-100).
    /// Sample count is shown in each X-axis label (e.g. "K (n=14)") so a
    /// low-count outlier is visible at a glance without affecting the sort.
    /// </summary>
    public sealed class ParetoChartControl : UserControl
    {
        private readonly Chart _chart;
        private readonly ChartArea _chartArea;
        private readonly Series _barSeries;
        private readonly Series _lineSeries;

        public ParetoChartControl()
        {
            _chart = new Chart { Dock = DockStyle.Fill };
            _chart.Click += (s, e) => OnClick(e);  // forward the child's click as this control's own Click

            _chartArea = new ChartArea("Main");
            _chartArea.AxisX.Interval = 1; // show every label, don't auto-skip
            _chartArea.AxisX.LabelStyle.Angle = -45;
            _chartArea.AxisY.Title = "Metric (%)";
            _chartArea.AxisY.Minimum = 0;
            _chartArea.AxisY2.Title = "Cumulative %";
            _chartArea.AxisY2.Minimum = 0;
            _chartArea.AxisY2.Maximum = 100;
            _chart.ChartAreas.Add(_chartArea);

            var legend = new Legend("MainLegend") { Docking = Docking.Top };
            _chart.Legends.Add(legend);

            _barSeries = new Series("Metric")
            {
                ChartType = SeriesChartType.Column,
                ChartArea = "Main",
                Legend = "MainLegend",
                Color = Color.FromArgb(216, 90, 48), // matches the c-coral 600 used in the mockup
                IsValueShownAsLabel = false,
                IsXValueIndexed = true, // REQUIRED for string/category X-values to render as separate
                                        // sequential points rather than collapsing onto one position -
                                        // without this, multiple AddXY(string, ...) calls were merging
                                        // into a single bar.
            };

            _lineSeries = new Series("Cumulative %")
            {
                ChartType = SeriesChartType.Line,
                ChartArea = "Main",
                Legend = "MainLegend",
                YAxisType = AxisType.Secondary,
                Color = Color.FromArgb(55, 138, 221), // matches the c-blue 600 used in the mockup
                BorderWidth = 2,
                MarkerStyle = MarkerStyle.Diamond,
                MarkerSize = 6,
                IsXValueIndexed = true, // same fix - both series share the same category axis positions
            };

            _chart.Series.Add(_barSeries);
            _chart.Series.Add(_lineSeries);

            Controls.Add(_chart);
        }

        /// <summary>
        /// Populates the chart from a sorted list of entries - call with
        /// the output of ParetoDataBuilder.BuildByCharacter or BuildByRole.
        /// Y-axis title updates to reflect which metric is being shown.
        /// </summary>
        public void SetData(List<ParetoEntry> entries, string metricAxisTitle)
        {
            _barSeries.Points.Clear();
            _lineSeries.Points.Clear();
            _chartArea.AxisY.Title = metricAxisTitle;

            if (entries == null || entries.Count == 0) return;

            double total = 0;
            foreach (var entry in entries) total += entry.ValuePercent;
            if (total <= 0) return;

            double running = 0;
            foreach (var entry in entries)
            {
                string label = $"{entry.Label} (n={entry.SampleCount})";

                int barIndex = _barSeries.Points.AddXY(label, Math.Round(entry.ValuePercent, 1));
                _barSeries.Points[barIndex].AxisLabel = label;

                running += entry.ValuePercent;
                double cumulativePercent = Math.Round((running / total) * 100.0, 1);
                int lineIndex = _lineSeries.Points.AddXY(label, cumulativePercent);
                _lineSeries.Points[lineIndex].AxisLabel = label;
            }
        }
    }
}