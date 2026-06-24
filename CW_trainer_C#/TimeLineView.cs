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
    /// behind them for visual comparison. The live row is pinned to a
    /// fixed position at the bottom of the control; completed rows are
    /// drawn working upward from it.
    ///
    /// Usage:
    ///   - Call AttachHistory(history) once, passing the SAME ElementHistory
    ///     instance your form uses for calibration/decoding - this control
    ///     no longer maintains its own separate character-grouping logic,
    ///     it purely visualizes whatever ElementHistory has already
    ///     grouped. This guarantees the timeline view and calibration/
    ///     decoding always agree about character boundaries, since
    ///     there's only one piece of code deciding them.
    ///   - Set DitLengthMs from the operator's entered WPM (ditMs = 1200/wpm)
    ///     before or during a session - grid rescales live. This should be
    ///     the SAME value you set on the attached ElementHistory.
    /// </summary>
    public sealed class TimelineView : UserControl
    {
        private ElementHistory _history;
        private readonly List<CharacterGroup> _completedRows = new List<CharacterGroup>();
        private CharacterGroup _liveRow = new CharacterGroup();

        /// <summary>Maximum completed rows retained in memory/displayed - older rows are discarded once exceeded (prevents unbounded growth in long sessions).</summary>
        [Category("Trainer")]
        [DefaultValue(500)]
        public int MaxRetainedRows { get; set; } = 500;

        /// <summary>Pixels per millisecond - controls how "wide" each character row is drawn. Increase to zoom in on timing detail, decrease to fit more on screen.</summary>
        [Category("Trainer")]
        [DefaultValue(1.2f)]
        public float PixelsPerMs { get; set; } = 1.2f;

        private double _ditLengthMs = 1200.0 / 20.0; // default 20 WPM

        /// <summary>
        /// Dit length in ms, used to scale the ideal grid. This is purely
        /// for DRAWING - it does not affect character boundary detection
        /// (that's entirely owned by the attached ElementHistory now).
        /// Keep this in sync with the ElementHistory's own DitLengthMs by
        /// setting both from the same place (e.g. your WPM textbox handler).
        /// </summary>
        [Category("Trainer")]
        public double DitLengthMs
        {
            get => _ditLengthMs;
            set { _ditLengthMs = value; Invalidate(); }
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

        /// <summary>Width in pixels reserved for the decoded-character column, drawn before the timing grid starts.</summary>
        private const int CharColumnWidth = 24;

        private static readonly Color BackgroundColor = Color.FromArgb(24, 24, 28);
        private static readonly Color GridLineColor = Color.FromArgb(120, 120, 132);
        private static readonly Color GridLineColorCharBoundary = Color.FromArgb(190, 190, 210);
        private static readonly Color GoodColor = Color.FromArgb(80, 200, 120);
        private static readonly Color WarnColor = Color.FromArgb(230, 180, 60);
        private static readonly Color BadColor = Color.FromArgb(220, 80, 70);
        private static readonly Color SpaceGapColor = Color.FromArgb(40, 40, 46);
        private static readonly Color LiveRowOutline = Color.FromArgb(120, 160, 220);
        private static readonly Color DecodedCharColor = Color.FromArgb(220, 220, 230);
        private static readonly Color UndecodedCharColor = Color.FromArgb(140, 100, 100);

        private static readonly Font CharFont = new Font("Consolas", 11f, FontStyle.Bold);

        public TimelineView()
        {
            DoubleBuffered = true;
            BackColor = BackgroundColor;
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        /// <summary>
        /// Attach the ElementHistory this view should visualize. Call once,
        /// passing the same instance your form uses for calibration so both
        /// always agree about character boundaries. Safe to call again
        /// later to switch to a different history instance (e.g. loading a
        /// past session) - the previous subscription is cleanly removed.
        /// </summary>
        public void AttachHistory(ElementHistory history)
        {
            if (_history != null)
            {
                _history.CharacterCompleted -= OnCharacterCompleted;
                _history.LiveCharacterChanged -= OnLiveCharacterChanged;
            }

            _history = history;
            _completedRows.Clear();
            _completedRows.AddRange(history.CompletedCharacters);
            _liveRow = history.CurrentCharacter;

            _history.CharacterCompleted += OnCharacterCompleted;
            _history.LiveCharacterChanged += OnLiveCharacterChanged;

            Invalidate(ClientRectangle);
        }

        private void OnCharacterCompleted(object sender, CharacterGroup row)
        {
            _completedRows.Add(row);
            if (_completedRows.Count > MaxRetainedRows)
                _completedRows.RemoveAt(0);
            _scrollOffsetRows = 0; // new data always snaps view back to the bottom
            Refresh(); // Invalidate immediately followed by a forced synchronous paint
        }

        private void OnLiveCharacterChanged(object sender, CharacterGroup row)
        {
            _liveRow = row;

            // Don't force a repaint for an EMPTY live row - this happens
            // right after a character closes (real space or timeout),
            // before the operator has started the next one. Showing a
            // visibly empty row at that moment is confusing (looks like
            // "two rows for one character"); instead, just hold the
            // previous frame until the new row actually has content. The
            // empty row still exists correctly in the data
            // (ElementHistory.CurrentCharacter), it's purely a display
            // timing choice to not repaint for it yet.
            if (row.Elements.Count == 0) return;

            _scrollOffsetRows = 0; // new data always snaps view back to the bottom
            Invalidate(ClientRectangle);
        }

        /// <summary>Clears the displayed rows - call when starting a new session (after also calling Reset() on the attached ElementHistory).</summary>
        public void ClearSession()
        {
            _completedRows.Clear();
            _liveRow = _history?.CurrentCharacter ?? new CharacterGroup();
            Invalidate(ClientRectangle);
        }


        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // TEMP DIAGNOSTIC
/*
             * System.Diagnostics.Debug.WriteLine(
                $"[OnPaint] liveRow.Elements={_liveRow.Elements.Count}, " +
                $"completedRows={_completedRows.Count}, " +
                $"lastCompletedDecodedChar={(_completedRows.Count > 0 ? _completedRows[_completedRows.Count - 1].DecodedText?.ToString() ?? "null" : "n/a")}");
*/
            // Live row is pinned to a fixed Y position near the bottom of
            // the visible client area when NOT scrolled back (_scrollOffsetRows
            // == 0). Scrolling shifts the whole stack up by N row-heights via
            // _scrollOffsetRows, measured in whole rows rather than pixels -
            // simpler to reason about and to clamp than a continuous pixel
            // offset, and mouse-wheel naturally steps in row-sized chunks.
            int liveRowY = ClientSize.Height - RowHeight - TopMargin
                           + _scrollOffsetRows * (RowHeight + RowSpacing);

            // An EMPTY live row (right after a character closes - by a
            // real space or the silence timeout - before the operator has
            // started the next one) is deliberately NOT drawn. Drawing an
            // empty row at that moment looks like a confusing "extra blank
            // line" sitting below the character that was just sent; better
            // to keep the just-completed character visually anchored at
            // the bottom position until a new mark actually arrives to
            // justify showing a fresh live row.
            bool liveRowHasContent = _liveRow.Elements.Count > 0;
            if (liveRowHasContent)
            {
                DrawRow(g, _liveRow, liveRowY, isLive: _scrollOffsetRows == 0);
            }

            // Completed rows are drawn working UPWARD from the live row's
            // position - if the live row is empty (not drawn), the most
            // recently completed row takes that bottom slot instead, so
            // there's no visual gap left behind.
            int y = liveRowHasContent
                ? liveRowY - RowSpacing - RowHeight
                : liveRowY;
            for (int i = _completedRows.Count - 1; i >= 0 && y > -RowHeight; i--)
            {
                bool isNewestCompleted = liveRowHasContent == false && i == _completedRows.Count - 1
                                          && _scrollOffsetRows == 0;
                DrawRow(g, _completedRows[i], y, isLive: isNewestCompleted);
                y -= RowHeight + RowSpacing;
            }
        }

        /// <summary>
        /// How many rows the view is scrolled back from "live" (0 = showing
        /// the current/most recent row at the bottom, as before). Increases
        /// as the operator scrolls UP to review history. Always reset to 0
        /// when new data arrives, per the simple "always snap to bottom on
        /// new data" behavior - there's no auto-follow override to fight
        /// here, scrolling back is purely a momentary review action.
        /// </summary>
        private int _scrollOffsetRows = 0;

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            // SystemInformation.MouseWheelScrollLines gives the user's
            // configured wheel sensitivity; e.Delta is +/-120 per notch by
            // convention. One notch = one row feels natural here rather
            // than scaling by the system's line setting (which is tuned
            // for text line-scrolling, not this control's row size).
            int notches = e.Delta / 120;
            _scrollOffsetRows += notches; // wheel up (positive delta) scrolls further back into history

            int maxScrollBack = _completedRows.Count; // can't scroll back further than we have rows for
            _scrollOffsetRows = Math.Max(0, Math.Min(_scrollOffsetRows, maxScrollBack));

            Invalidate(ClientRectangle);
        }

        private void DrawRow(Graphics g, CharacterGroup row, int y, bool isLive)
        {
            float ditPx = (float)(DitLengthMs * PixelsPerMs);
            if (ditPx <= 0) ditPx = 1;

            // Grid/blocks start after the reserved character column, not at
            // LeftMargin directly - LeftMargin is now just the char
            // column's own left padding.
            float gridStartX = LeftMargin + CharColumnWidth;

            // Determine how many dit-widths this row should span for grid
            // drawing - use the larger of (actual content) or a sensible
            // minimum so short/empty live rows still show a starter grid.
            double rowDurationMs = row.TotalDurationMs();
            float rowWidthPx = Math.Max((float)(rowDurationMs * PixelsPerMs), ditPx * 12);

            var rowRect = new RectangleF(gridStartX, y, rowWidthPx, RowHeight);

            // Decoded text column - shown whenever DecodedText has a
            // value, regardless of isLive. isLive here means "draw with
            // the dotted outline because this row occupies the bottom
            // slot" - it does NOT mean "this is the genuinely in-progress
            // row with no decode yet". A completed, decoded row can
            // legitimately be drawn with isLive=true (when the live row
            // itself is empty and the newest completed row takes the
            // bottom slot) - in that case it still has real DecodedText
            // and must show it. Only the TRUE live/in-progress row (passed
            // in from the _liveRow branch in OnPaint) will ever have
            // DecodedText == null, since decode only ever runs inside
            // CloseCurrentCharacter - so checking for null alone is
            // sufficient and correct, no isLive check needed.
            //
            // NOTE: DecodedText may be a multi-character prosign (e.g.
            // "SK", "BK") rather than a single letter - the fixed-width
            // CharColumnWidth was sized for single characters, so longer
            // prosign text may visually overflow/get clipped at the
            // default font size. Acceptable for now; revisit column width
            // or font scaling if prosigns become a frequent display need.
            if (!string.IsNullOrEmpty(row.DecodedText))
            {
                Color textColor = row.DecodedText == MorseDecoder.UndecodedText ? UndecodedCharColor : DecodedCharColor;
                var charRect = new RectangleF(0, y, LeftMargin + CharColumnWidth, RowHeight);
                using var textBrush = new SolidBrush(textColor);
                using var stringFormat = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                };
                g.DrawString(row.DecodedText, CharFont, textBrush, charRect, stringFormat);
            }

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
            for (float gx = gridStartX; gx <= gridStartX + rowWidthPx; gx += ditPx, ditIndex++)
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
            float x = gridStartX;
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
        /// "ideal" multiple of a dit (1x = dit, 3x = dah) - delegates to
        /// MarkClassifier, the single shared definition of mark quality
        /// also used by MorseDecoder, so the visual coloring and the
        /// decoder's strict "any red mark fails decode" rule always agree.
        /// </summary>
        private Color ColorForMark(double durationMs, double ditLengthMs)
        {
            var quality = MarkClassifier.Classify(durationMs, ditLengthMs,
                GoodToleranceFraction, PoorToleranceFraction, out _);

            return quality switch
            {
                MarkQuality.Good => GoodColor,
                MarkQuality.Warn => WarnColor,
                _ => BadColor,
            };
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate(ClientRectangle);
        }
    }
}