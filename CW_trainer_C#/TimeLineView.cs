using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CwTrainer.Serial;

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
        [Category("Trainer")]
        [DefaultValue(500)]
        public int MaxRetainedRows { get; set; } = 500;

        /// <summary>Pixels per millisecond - controls how "wide" each character row is drawn. Increase to zoom in on timing detail, decrease to fit more on screen.</summary>
        [Category("Trainer")]
        [DefaultValue(1.2f)]
        public float PixelsPerMs { get; set; } = 1.2f;

        private double _ditLengthMs = 1200.0 / 20.0; // default 20 WPM

        [Category("Trainer")]
        public double DitLengthMs
        {
            get => _ditLengthMs;
            set { _ditLengthMs = value; _rowBuilder.DitLengthMs = value; Invalidate(); }
        }

        private bool ShouldSerializeDitLengthMs() => Math.Abs(_ditLengthMs - (1200.0 / 20.0)) > 0.0001;
        private void ResetDitLengthMs() => DitLengthMs = 1200.0 / 20.0;

        /// <summary>Acceptable deviation (as a fraction of ideal width) before a block is colored amber instead of green. E.g. 0.15 = +/-15% is "good".</summary>
        [Category("Trainer")]
        [DefaultValue(0.15)]
        public double GoodToleranceFraction { get; set; } = 0.15;

        /// <summary>Deviation beyond this fraction is colored red instead of amber.</summary>
        [Category("Trainer")]
        [DefaultValue(0.35)]
        public double PoorToleranceFraction { get; set; } = 0.35;

        private const int RowHeight = 28;
        private const int RowSpacing = 6;
        private const int LeftMargin = 8;
        private const int TopMargin = 8;

        private static readonly Color BackgroundColor = Color.FromArgb(24, 24, 28);
        private static readonly Color GridLineColor = Color.FromArgb(120, 120, 132);
        private static readonly Color GridLineColorCharBoundary = Color.FromArgb(190, 190, 210);
        private static readonly Color GoodColor = Color.FromArgb(80, 200, 120);
        private static readonly Color WarnColor = Color.FromArgb(230, 180, 60);
        private static readonly Color BadColor = Color.FromArgb(220, 80, 70);
        private static readonly Color SpaceGapColor = Color.FromArgb(40, 40, 46);
        private static readonly Color LiveRowOutline = Color.FromArgb(120, 160, 220);

        public TimelineView()
        {
            DoubleBuffered = true;
            BackColor = BackgroundColor;

            // NOTE: no AutoScroll, no manual scroll-offset tracking either.
            // The live row is pinned to a fixed Y position (see OnPaint);
            // completed rows are positioned by counting backward from it,
            // so there's nothing to "scroll" in the traditional sense -
            // older rows simply stop being drawn once they'd fall above
            // the visible area (y > -RowHeight check in OnPaint).
            SetStyle(ControlStyles.ResizeRedraw, true);

            _rowBuilder.RowCompleted += (s, row) =>
            {
                _completedRows.Add(row);
                if (_completedRows.Count > MaxRetainedRows)
                    _completedRows.RemoveAt(0);
                Invalidate(ClientRectangle);
            };

            _rowBuilder.LiveRowChanged += (s, row) =>
            {
                _liveRow = row;
                Invalidate(ClientRectangle);
            };
        }

        /// <summary>Feed one completed Element (mark or space) - call for every element your serial pairing logic produces.</summary>
        public void AddElement(Element element) => _rowBuilder.AddElement(element);

        /// <summary>Clears all rows and starts fresh - call when starting a new session.</summary>
        public void ClearSession()
        {
            _completedRows.Clear();
            _rowBuilder.Reset();
            Invalidate(ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Live row is pinned to a fixed Y position near the bottom of
            // the visible client area - it never moves, regardless of how
            // many rows have accumulated. This makes it much easier to
            // keep your eye on "what's happening right now" without
            // hunting for a moving target each time a row completes.
            int liveRowY = ClientSize.Height - RowHeight - TopMargin;

            DrawRow(g, _liveRow, liveRowY, isLive: true);

            // Completed rows are drawn working UPWARD from the live row,
            // most recent immediately above it, older ones further up and
            // eventually off the top of the visible area entirely (simply
            // not drawn - no scrolling transform needed at all now, since
            // position is computed directly from "rows back from live").
            int y = liveRowY - RowSpacing - RowHeight;
            for (int i = _completedRows.Count - 1; i >= 0 && y > -RowHeight; i--)
            {
                DrawRow(g, _completedRows[i], y, isLive: false);
                y -= RowHeight + RowSpacing;
            }
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
            //
            // IMPORTANT: grid lines are drawn with antialiasing OFF and
            // coordinates snapped to whole pixels. Thin lines drawn with
            // antialiasing at fractional sub-pixel X positions render
            // inconsistently (sometimes crisp, sometimes blurry/split
            // across two columns) as the grid's absolute position shifts
            // during scrolling - snapping + no antialiasing makes every
            // line a single, consistent, sharp pixel column regardless of
            // scroll offset.
            var previousSmoothingMode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;

            int ditIndex = 0;
            for (float gx = LeftMargin; gx <= LeftMargin + rowWidthPx; gx += ditPx, ditIndex++)
            {
                bool isCharBoundaryTick = ditIndex % 3 == 0;
                int snappedX = (int)Math.Round(gx);

                Color lineColor = isCharBoundaryTick ? GridLineColorCharBoundary : GridLineColor;
                float lineWidth = isCharBoundaryTick ? 2f : 1f;

                using var gridPen = new Pen(lineColor, lineWidth);
                // NOTE: no explicit PenAlignment set (left at default Center).
                // Inset alignment was found to clip the top/bottom-most
                // pixel of vertical lines when a row's y-range coincided
                // with the AutoScroll clip boundary during scrolling -
                // default Center alignment with integer-snapped coordinates
                // is both simpler and avoids that edge case.
                //
                // Lines are also drawn 1px short of the row's exact top/
                // bottom edge (y+1 to y+RowHeight-1) rather than flush
                // (y to y+RowHeight) - this keeps both endpoints strictly
                // inside the row's own bounds rather than exactly on them,
                // so a line's endpoint never coincides with the clip
                // rectangle's edge at the moment the view is scrolled to
                // place that row's top/bottom right at the visible boundary.
                g.DrawLine(gridPen, snappedX, y + 1, snappedX, y + RowHeight - 1);
            }

            g.SmoothingMode = previousSmoothingMode;

            if (isLive)
            {
                using var outlinePen = new Pen(LiveRowOutline, 1f) { DashStyle = DashStyle.Dot };
                g.DrawRectangle(outlinePen, rowRect.X, rowRect.Y, rowRect.Width, rowRect.Height);
            }

            // Draw each element to scale, left to right, chronologically.
            // Blocks keep antialiasing on (previousSmoothingMode, typically
            // AntiAlias) since filled rectangles benefit from smooth edges
            // and aren't subject to the same hairline-jitter problem thin
            // strokes have.
            float x = LeftMargin;
            foreach (var element in row.Elements)
            {
                float widthPx = (float)(element.DurationMs * PixelsPerMs);
                var elementRect = new RectangleF(x, y + 6, Math.Max(widthPx, 1), RowHeight - 12);

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
            Invalidate(ClientRectangle);
        }
    }
}