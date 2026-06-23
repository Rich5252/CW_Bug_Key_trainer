using System.Collections.Generic;
using System.Text;

namespace CwTrainer.Serial
{
    /// <summary>
    /// Decodes a CharacterGroup's marks into a character via the standard
    /// International Morse Code table. Uses MarkClassifier (the same logic
    /// driving the timeline's green/amber/red coloring) to judge each
    /// mark's dit-or-dah identity and quality - if ANY mark in the
    /// character would render red (MarkQuality.Bad) in the timeline,
    /// decode fails entirely and returns '~', rather than guessing through
    /// a clearly-wrong element. Warn-quality marks are tolerated (still
    /// classified as dit or dah, just imperfectly timed).
    /// </summary>
    public static class MorseDecoder
    {
        // Standard International Morse Code, dits/dahs as '.'/'-'.
        private static readonly Dictionary<string, char> Table = new Dictionary<string, char>
        {
            [".-"] = 'A',
            ["-..."] = 'B',
            ["-.-."] = 'C',
            ["-.."] = 'D',
            ["."] = 'E',
            ["..-."] = 'F',
            ["--."] = 'G',
            ["...."] = 'H',
            [".."] = 'I',
            [".---"] = 'J',
            ["-.-"] = 'K',
            [".-.."] = 'L',
            ["--"] = 'M',
            ["-."] = 'N',
            ["---"] = 'O',
            [".--."] = 'P',
            ["--.-"] = 'Q',
            [".-."] = 'R',
            ["..."] = 'S',
            ["-"] = 'T',
            ["..-"] = 'U',
            ["...-"] = 'V',
            [".--"] = 'W',
            ["-..-"] = 'X',
            ["-.--"] = 'Y',
            ["--.."] = 'Z',
            ["-----"] = '0',
            [".----"] = '1',
            ["..---"] = '2',
            ["...--"] = '3',
            ["....-"] = '4',
            ["....."] = '5',
            ["-...."] = '6',
            ["--..."] = '7',
            ["---.."] = '8',
            ["----."] = '9',
            // Common prosigns/punctuation - extend as needed.
            [".-.-.-"] = '.',
            ["--..--"] = ',',
            ["..--.."] = '?',
            [".----."] = '\'',
            ["-.-.--"] = '!',
            ["-..-."] = '/',
            ["-.--."] = '(',
            ["-.--.-"] = ')',
            [".-..."] = '&',
            ["---..."] = ':',
            ["-.-.-."] = ';',
            ["-...-"] = '=',
            [".-.-."] = '+',
            ["-....-"] = '-',
            ["..--.-"] = '_',
            [".-..-."] = '"',
            ["...-..-"] = '$',
            [".--.-."] = '@',
        };

        /// <summary>Character returned when decode is attempted but the pattern doesn't match any known entry, or any mark is too poorly timed to trust (MarkQuality.Bad).</summary>
        public const char UndecodedChar = '~';

        /// <summary>
        /// Attempts to decode a completed CharacterGroup. Returns the
        /// decoded character, or UndecodedChar on failure. Does NOT modify
        /// group.DecodedChar itself - that's left to the caller (typically
        /// ElementHistory, right after a character completes) so this
        /// class stays a pure function with no side effects.
        /// </summary>
        public static char Decode(CharacterGroup group, double ditLengthMs,
            double goodToleranceFraction, double poorToleranceFraction)
        {
            if (group == null || group.Elements.Count == 0)
                return UndecodedChar;

            var pattern = new StringBuilder();

            foreach (var element in group.Elements)
            {
                if (!element.IsMark) continue; // spaces between elements aren't part of the pattern string

                var quality = MarkClassifier.Classify(element.DurationMs, ditLengthMs,
                    goodToleranceFraction, poorToleranceFraction, out bool isDit);

                if (quality == MarkQuality.Bad)
                {
                    // Strict mode: any single red-quality mark fails the
                    // whole character rather than guessing through it.
                    return UndecodedChar;
                }

                pattern.Append(isDit ? '.' : '-');
            }

            if (pattern.Length == 0) return UndecodedChar;

            return Table.TryGetValue(pattern.ToString(), out char decoded) ? decoded : UndecodedChar;
        }
    }
}