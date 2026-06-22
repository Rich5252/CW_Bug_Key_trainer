using System;
using System.Collections.Generic;
using System.Linq;

namespace CwTrainer.Serial
{
    /// <summary>
    /// One character's worth of marks/spaces, grouped purely by timing
    /// (same boundary logic as TimelineRowBuilder, but kept independent so
    /// calibration/decoding don't depend on the timeline UI control
    /// existing or being visible).
    /// </summary>
    public sealed class CharacterGroup
    {
        public List<Element> Elements { get; } = new List<Element>();

        /// <summary>Just the mark durations (ms), in chronological order, ignoring intra-character spaces.</summary>
        public List<double> MarkDurationsMs =>
            Elements.Where(e => e.IsMark).Select(e => e.DurationMs).ToList();
    }

    /// <summary>
    /// Maintains the full session's element history and groups it into
    /// characters as elements arrive, independent of any UI control. This
    /// is the data source for calibration now, and decoding later - both
    /// consume the same character-grouped history rather than duplicating
    /// boundary-detection logic.
    /// </summary>
    public sealed class ElementHistory
    {
        /// <summary>Dit length in ms, used only to decide character boundaries (a space >= this many dit-widths ends the current character).</summary>
        public double DitLengthMs { get; set; } = 1200.0 / 20.0;

        public double CharSpaceThresholdDits { get; set; } = 2.5;

        private readonly List<CharacterGroup> _completedCharacters = new List<CharacterGroup>();
        private CharacterGroup _currentCharacter = new CharacterGroup();

        /// <summary>All characters completed so far this session, oldest first.</summary>
        public IReadOnlyList<CharacterGroup> CompletedCharacters => _completedCharacters;

        /// <summary>The character currently being sent (not yet ended by a long-enough space) - may be empty.</summary>
        public CharacterGroup CurrentCharacter => _currentCharacter;

        public event EventHandler<CharacterGroup> CharacterCompleted;

        public void AddElement(Element element)
        {
            double thresholdMs = CharSpaceThresholdDits * DitLengthMs;

            if (!element.IsMark && element.DurationMs >= thresholdMs && _currentCharacter.Elements.Count > 0)
            {
                _currentCharacter.Elements.Add(element);
                _completedCharacters.Add(_currentCharacter);
                CharacterCompleted?.Invoke(this, _currentCharacter);

                _currentCharacter = new CharacterGroup();
                return;
            }

            _currentCharacter.Elements.Add(element);
        }

        /// <summary>The most recently COMPLETED character, or null if none yet.</summary>
        public CharacterGroup LastCompletedCharacter =>
            _completedCharacters.Count > 0 ? _completedCharacters[_completedCharacters.Count - 1] : null;

        public void Reset()
        {
            _completedCharacters.Clear();
            _currentCharacter = new CharacterGroup();
        }
    }
}