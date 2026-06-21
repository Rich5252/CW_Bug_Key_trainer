using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CwTrainer.Display
{
    /// <summary>
    /// Scrolling timeline display: one row per character, each row drawing
    /// its actual mark/space elements to scale, with a faint ideal-dit grid
    /// behind them for visual comparison. New rows appear at the bottom;
    /// older rows scroll up and off the top once they exceed the visible
    /// area (standard ListBox-style behavior, implemented via a
    /// fixed-capacity row list + AutoScroll).
    ///
    /// Usage:
    ///   - Set DitLengthMs from the operator's entered WPM (ditMs = 1200/wpm)
    ///     before or during a session - grid rescales live.
    ///   - Call AddElement(element) for every Element as it completes (same
    ///     data your existing pairing logic already produces).
    ///   - The control internally uses a TimelineRowBuilder to group
    ///     elements into rows; you don't need to manage rows yourself.
    /// </summary>
    public sealed class TimelineView : UserControl
    {
        private readonly TimelineRowBuilder _rowBuilder = new TimelineRowBuilder();
        private readonly List<TimelineRow> _completedRows = new List<TimelineRow>();
        private TimelineRow _liveRow = new TimelineRow();

        /// <summary>Maximum completed rows retained in memory/displayed - older rows are discarded once exceeded (prevents unbounded growth in long sessions). UI shows the most recent rows that fit, scrolled to bottom.</summary>
        public int MaxRetainedRows { get; set; } = 500;

        /// <summary>Pixels per millisecond - controls how "wide" each character row is drawn. Increase to zoom in on timing detail, decrease to fit more on screen.</summary>
        public float PixelsPerMs { get; set; } = 1.2f;

        public double DitLengthMs
        {
            get => _rowBuilder.DitLengthMs;
            set { _rowBuilder.DitLengthMs = value; Invalidate(); }
        }

        /// <summary>Acceptable deviation (as a fraction of ideal width) before a block is colored amber instead of green. E.g. 0.15 = +/-15% is "good".</summary>
        public double GoodToleranceFraction { get; set; } = 0.15;

        /// <summary>Deviation beyond this fraction is colored red instead of amber.</summary>
        public double PoorToleranceFraction { get; set; } = 0.35;

        private const int RowHeight = 28;
        private const int RowSpacing = 6;
        private const int LeftMargin = 8;
        private const int TopMargin = 8;

        private static readonly Color BackgroundColor = Color.FromArgb(24, 24, 28);
        private static readonly Color GridLineColor = Color.FromArgb(60, 60, 66);
        private static readonly Color GridLineColorCharBoundary = Color.FromArgb(90, 90, 100);
        private static readonly Color GoodColor = Color.FromArgb(80, 200, 120);
        private static readonly Color WarnColor = Color.FromArgb(230, 180, 60);
        private static readonly Color BadColor = Color.FromArgb(220, 80, 70);
        private static readonly Color SpaceGapColor = Color.FromArgb(40, 40, 46);
        private static readonly Color LiveRowOutline = Color.FromArgb(120, 160, 220);

        public TimelineView()
        {
            DoubleBuffered = true;
            BackColor = BackgroundColor;
            AutoScroll = true;

            _rowBuilder.RowCompleted += (s, row) =>
            {
                _completedRows.Add(row);
                if (_completedRows.Count > MaxRetainedRows)
                    _completedRows.RemoveAt(0);
                UpdateScrollExtentAndInvalidate();
            };

            _rowBuilder.LiveRowChanged += (s, row) =>
            {
                _liveRow = row;
                UpdateScrollExtentAndInvalidate();
            };
        }

        /// <summary>Feed one completed Element (mark or space) - call for every element your serial pairing logic produces.</summary>
        public void AddElement(Element element) => _rowBuilder.AddElement(element);

        /// <summary>Clears all rows and starts fresh - call when starting a new session.</summary>
        public void ClearSession()
        {
            _completedRows.Clear();
            _rowBuilder.Reset();
            UpdateScrollExtentAndInvalidate();
        }

        private void UpdateScrollExtentAndInvalidate()
        {
            int totalRows = _completedRows.Count + 1; // +1 for live row
            int contentHeight = TopMargin + totalRows * (RowHeight + RowSpacing);
            AutoScrollMinSize = new Size(0, contentHeight);

            // Auto-scroll to bottom so the newest/live row is always visible,
            // matching the requested "log scrolls up" behavior.
            AutoScrollPosition = new Point(0, Math.Max(0, contentHeight - ClientSize.Height));

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);

            int y = TopMargin;

            foreach (var row in _completedRows)
            {
                DrawRow(g, row, y, isLive: false);
                y += RowHeight + RowSpacing;
            }

            // Always draw the live row last, even if empty - gives a
            // consistent "current line" position for the operator to watch.
            DrawRow(g, _liveRow, y, isLive: true);
        }

        private void DrawRow(Graphics g, TimelineRow row, int y, bool isLive)
        {
            float ditPx = (float)(DitLengthMs * PixelsPerMs);
            if (ditPx <= 0) ditPx = 1;

            // Determine how many dit-widths this row should span for grid
            // drawing - use the larger of (actual content) or a sensible
            // minimum so short/empty live rows still show a starter grid.
            double rowDurationMs = row.TotalDurationMs;
            float rowWidthPx = Math.Max((float)(rowDurationMs * PixelsPerMs), ditPx * 12);

            var rowRect = new RectangleF(LeftMargin, y, rowWidthPx, RowHeight);

            // Faint ideal grid: ticks every 1 dit, slightly stronger every
            // 3 dits (the classic inter-character space width) so the
            // operator has a built-in ruler.
            int ditIndex = 0;
            for (float gx = LeftMargin; gx <= LeftMargin + rowWidthPx; gx += ditPx, ditIndex++)
            {
                bool isCharBoundaryTick = ditIndex % 3 == 0;
                using var gridPen = new Pen(isCharBoundaryTick ? GridLineColorCharBoundary : GridLineColor,
                    isCharBoundaryTick ? 1.2f : 0.6f);
                g.DrawLine(gridPen, gx, y, gx, y + RowHeight);
            }

            if (isLive)
            {
                using var outlinePen = new Pen(LiveRowOutline, 1f) { DashStyle = DashStyle.Dot };
                g.DrawRectangle(outlinePen, rowRect.X, rowRect.Y, rowRect.Width, rowRect.Height);
            }

            // Draw each element to scale, left to right, chronologically.
            float x = LeftMargin;
            foreach (var element in row.Elements)
            {
                float widthPx = (float)(element.DurationMs * PixelsPerMs);
                var elementRect = new RectangleF(x, y + 4, Math.Max(widthPx, 1), RowHeight - 8);

                if (element.IsMark)
                {
                    Color fillColor = ColorForMark(element.DurationMs, ditPx > 0 ? DitLengthMs : 0);
                    using var brush = new SolidBrush(fillColor);
                    g.FillRectangle(brush, elementRect);
                }
                else
                {
                    // Spaces drawn as a subtle filled gap rather than fully
                    // blank, so short vs long spaces remain visually
                    // distinguishable even without comparing to the grid.
                    using var brush = new SolidBrush(SpaceGapColor);
                    g.FillRectangle(brush, elementRect);
                }

                x += widthPx;
            }
        }

        /// <summary>
        /// Color-codes a mark by how close its duration is to the nearest
        /// "ideal" multiple of a dit (1x = dit, 3x = dah) - this lets both
        /// dits and dahs be judged against their own correct target, rather
        /// than assuming every mark should be exactly one dit.
        /// </summary>
        private Color ColorForMark(double durationMs, double ditLengthMs)
        {
            if (ditLengthMs <= 0) return GoodColor;

            double ratio = durationMs / ditLengthMs;
            // Snap to whichever ideal target (dit=1 or dah=3) is closer,
            // so a slightly-long dit isn't compared against a dah's target.
            double nearestIdeal = ratio < 2.0 ? 1.0 : 3.0;
            double deviationFraction = Math.Abs(ratio - nearestIdeal) / nearestIdeal;

            if (deviationFraction <= GoodToleranceFraction) return GoodColor;
            if (deviationFraction <= PoorToleranceFraction) return WarnColor;
            return BadColor;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollExtentAndInvalidate();
        }
    }
}
