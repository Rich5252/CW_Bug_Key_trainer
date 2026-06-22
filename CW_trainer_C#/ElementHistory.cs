using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace CwTrainer.Serial
{
    /// <summary>
    /// One character's worth of marks/spaces, grouped purely by timing.
    /// This is now the SINGLE source of truth for character grouping,
    /// consumed both by the timeline display and by calibration/decoding -
    /// previously these were two separate, independently-running
    /// implementations (TimelineRowBuilder and ElementHistory), which
    /// could disagree about where boundaries fell. Merging removes that
    /// entire class of bug.
    /// </summary>
    public sealed class CharacterGroup
    {
        public List<Element> Elements { get; } = new List<Element>();

        /// <summary>Just the mark durations (ms), in chronological order, ignoring intra-character spaces.</summary>
        public List<double> MarkDurationsMs =>
            Elements.Where(e => e.IsMark).Select(e => e.DurationMs).ToList();

        /// <summary>
        /// True if this character was closed because a long-enough real
        /// space Element arrived (the next character had already started).
        /// False if it was closed by the silence TIMEOUT instead (operator
        /// paused/stopped sending, no next mark has occurred yet, or ever
        /// will this session). Either way, the character's own Elements
        /// are equally valid to read from - this flag is informational
        /// only, useful for diagnostics, not required for normal use.
        /// </summary>
        public bool ClosedByTimeout { get; set; }
    }

    /// <summary>
    /// Maintains the full session's element history and groups it into
    /// characters as elements arrive. Characters are closed either by a
    /// real inter-character space Element (CharSpaceThresholdDits worth of
    /// silence, measured from actual key timing), OR by a silence TIMEOUT
    /// when no next mark has started within a similar window - this
    /// second path is what lets "last completed character" reflect what
    /// was just sent, without waiting for the operator to begin a new
    /// character first (previously, calibration/decoding would always lag
    /// one character behind for exactly this reason).
    ///
    /// The timeout is implemented via a System.Windows.Forms.Timer, so
    /// this class must be constructed and used on a thread with a message
    /// loop (the UI thread) - consistent with how KeyEventSerialPort
    /// already marshals its events there.
    /// </summary>
    public sealed class ElementHistory : IDisposable
    {
        /// <summary>Dit length in ms, used to decide character boundaries (a space, real or timed-out, of at least CharSpaceThresholdDits dit-widths ends the current character).</summary>
        public double DitLengthMs { get; set; } = 1200.0 / 20.0;

        public double CharSpaceThresholdDits { get; set; } = 2.5;

        /// <summary>
        /// The silence TIMEOUT backstop is this many times longer than the
        /// normal real-space threshold. It must be distinctly longer, not
        /// equal - if it fired at the same threshold as real-space
        /// detection, it would race against normal sending (the timeout
        /// and the next real mark could arrive at nearly the same instant,
        /// non-deterministically choosing which "wins"). Keeping it
        /// clearly longer means real-space closure always wins during
        /// normal sending - the timeout only ever matters when the
        /// operator genuinely stops and no next mark is coming.
        /// </summary>
        public double TimeoutMultiplier { get; set; } = 2.0; // i.e. timeout = 2x the real-space threshold

        private readonly List<CharacterGroup> _completedCharacters = new List<CharacterGroup>();
        private CharacterGroup _currentCharacter = new CharacterGroup();
        private readonly System.Windows.Forms.Timer _timeoutTimer;

        /// <summary>All characters completed so far this session, oldest first.</summary>
        public IReadOnlyList<CharacterGroup> CompletedCharacters => _completedCharacters;

        /// <summary>The character currently being sent (not yet closed) - may be empty.</summary>
        public CharacterGroup CurrentCharacter => _currentCharacter;

        /// <summary>Raised when a character is closed, by either a real inter-character space or the silence timeout. Check CharacterGroup.ClosedByTimeout if the distinction matters to your consumer.</summary>
        public event EventHandler<CharacterGroup> CharacterCompleted;

        /// <summary>Raised whenever the in-progress character changes (new element added, or it was just reset after closing) - subscribe to this to repaint a live/current-row display.</summary>
        public event EventHandler<CharacterGroup> LiveCharacterChanged;

        public ElementHistory()
        {
            // One-shot timer, re-armed after every element to fire at the
            // (longer) TIMEOUT window, not the normal real-space threshold.
            // During normal sending, the next real mark always arrives well
            // before this fires, so it's continuously cancelled and
            // re-armed without ever actually triggering - it only fires
            // when the operator pauses for longer than even the timeout
            // window, which by construction can't happen during normal
            // inter-character gaps.
            _timeoutTimer = new System.Windows.Forms.Timer();
            _timeoutTimer.Tick += (s, e) =>
            {
                _timeoutTimer.Stop();
                if (_currentCharacter.Elements.Count > 0)
                {
                    CloseCurrentCharacter(closedByTimeout: true);
                }
            };
        }

        public void AddElement(Element element)
        {
            double thresholdMs = CharSpaceThresholdDits * DitLengthMs;
            double timeoutMs = thresholdMs * TimeoutMultiplier;

            // A space arriving while _currentCharacter is EMPTY means the
            // timeout already closed the previous character based on this
            // same silence. This space describes silence that belongs to
            // the character that already closed, not to the new one about
            // to start - discard it.
            if (!element.IsMark && _currentCharacter.Elements.Count == 0)
            {
                RearmTimeout(timeoutMs);
                return;
            }

            // PRIMARY closure path: a real space Element at/above the
            // normal threshold closes the character immediately - this is
            // unchanged from the original behavior and is what fires
            // during ALL normal sending, well before the timeout backstop
            // ever gets a chance to.
            if (!element.IsMark && element.DurationMs >= thresholdMs && _currentCharacter.Elements.Count > 0)
            {
                _currentCharacter.Elements.Add(element);
                CloseCurrentCharacter(closedByTimeout: false);
                return;
            }

            _currentCharacter.Elements.Add(element);
            LiveCharacterChanged?.Invoke(this, _currentCharacter);

            // Re-arm the BACKSTOP timeout - only relevant if no next
            // element (mark or qualifying space) arrives before this
            // longer window elapses.
            RearmTimeout(timeoutMs);
        }

        private void RearmTimeout(double timeoutMs)
        {
            _timeoutTimer.Stop();
            int intervalMs = Math.Max(1, (int)Math.Ceiling(timeoutMs));
            _timeoutTimer.Interval = intervalMs;
            _timeoutTimer.Start();
        }

        private void CloseCurrentCharacter(bool closedByTimeout)
        {
            _currentCharacter.ClosedByTimeout = closedByTimeout;
            _completedCharacters.Add(_currentCharacter);
            CharacterCompleted?.Invoke(this, _currentCharacter);

            _currentCharacter = new CharacterGroup();
            LiveCharacterChanged?.Invoke(this, _currentCharacter);
        }

        /// <summary>The most recently COMPLETED character, or null if none yet.</summary>
        public CharacterGroup LastCompletedCharacter =>
            _completedCharacters.Count > 0 ? _completedCharacters[_completedCharacters.Count - 1] : null;

        public void Reset()
        {
            _timeoutTimer.Stop();
            _completedCharacters.Clear();
            _currentCharacter = new CharacterGroup();
            LiveCharacterChanged?.Invoke(this, _currentCharacter);
        }

        public void Dispose()
        {
            _timeoutTimer.Stop();
            _timeoutTimer.Dispose();
        }
    }
}