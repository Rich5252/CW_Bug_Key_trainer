using System;
using System.Collections.Generic;

namespace CwTrainer.Display
{
    /// <summary>
    /// One row of the timeline view: the sequence of mark/space Elements
    /// that make up a single character, plus the trailing space that ended
    /// it (if the row is complete) or nothing yet (if still live).
    /// </summary>
    public sealed class TimelineRow
    {
        public List<Element> Elements { get; } = new List<Element>();

        /// <summary>True once a character-ending space has been observed and this row is finalized.</summary>
        public bool IsComplete { get; set; }

        /// <summary>Total elapsed duration of this row in ms - convenience for layout/scaling.</summary>
        public double TotalDurationMs
        {
            get
            {
                double sum = 0;
                foreach (var e in Elements) sum += e.DurationMs;
                return sum;
            }
        }
    }

    /// <summary>
    /// Accumulates the raw Element stream into per-character TimelineRows,
    /// using a configurable dit-length to decide where character boundaries
    /// fall (a space >= CharSpaceThresholdDits * ditMs ends the current row).
    ///
    /// This is deliberately separate from any decode/character-recognition
    /// logic - it only groups elements by timing, it doesn't attempt to
    /// identify which letter was sent. That keeps this class simple and
    /// reusable even before/without a decoder.
    /// </summary>
    public sealed class TimelineRowBuilder
    {
        /// <summary>Dit length in ms, used purely to decide character boundaries and to scale the ideal grid. Set from the operator's entered WPM: ditMs = 1200 / wpm.</summary>
        public double DitLengthMs { get; set; } = 1200.0 / 20.0; // default 20 WPM

        /// <summary>Spaces at least this many dit-widths long end the current character (classic standard is 3 dits for inter-character space).</summary>
        public double CharSpaceThresholdDits { get; set; } = 2.5; // slightly under 3 to tolerate real-world variance

        private TimelineRow _currentRow = new TimelineRow();

        /// <summary>Raised when a row is finalized (character boundary reached) - subscribe to know when to start a new line in the UI.</summary>
        public event EventHandler<TimelineRow> RowCompleted;

        /// <summary>Raised whenever the live (incomplete) row changes - subscribe to repaint the current bottom row as the operator keys.</summary>
        public event EventHandler<TimelineRow> LiveRowChanged;

        /// <summary>
        /// Feed one completed Element (mark or space) into the builder.
        /// Call this from the same place you currently construct Elements
        /// from paired KeyEvents.
        /// </summary>
        public void AddElement(Element element)
        {
            double thresholdMs = CharSpaceThresholdDits * DitLengthMs;

            if (!element.IsMark && element.DurationMs >= thresholdMs && _currentRow.Elements.Count > 0)
            {
                // This space is long enough to end the character. Include
                // it in the row (so the row's total duration/last gap is
                // visible) then finalize.
                _currentRow.Elements.Add(element);
                _currentRow.IsComplete = true;
                RowCompleted?.Invoke(this, _currentRow);

                _currentRow = new TimelineRow();
                LiveRowChanged?.Invoke(this, _currentRow);
                return;
            }

            _currentRow.Elements.Add(element);
            LiveRowChanged?.Invoke(this, _currentRow);
        }

        /// <summary>Discard any in-progress row - call when starting a new session.</summary>
        public void Reset()
        {
            _currentRow = new TimelineRow();
            LiveRowChanged?.Invoke(this, _currentRow);
        }
    }
}