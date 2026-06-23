using System.Collections.Generic;
using System.Text;

namespace CwTrainer.Serial
{
    /// <summary>
    /// Decodes a CharacterGroup's marks into text via the standard
    /// International Morse Code table, including prosigns (procedural
    /// signs like SK, BK, AR - sent as two letters run together with no
    /// inter-letter space, conventionally written/displayed as the two
    /// letters with an overbar, here just as the two-letter string).
    ///
    /// Uses MarkClassifier (the same logic driving the timeline's
    /// green/amber/red coloring) to judge each mark's dit-or-dah identity
    /// and quality - if ANY mark in the character would render red
    /// (MarkQuality.Bad) in the timeline, decode fails entirely and
    /// returns UndecodedText, rather than guessing through a clearly-wrong
    /// element. Warn-quality marks are tolerated (still classified as dit
    /// or dah, just imperfectly timed).
    /// </summary>
    public static class MorseDecoder
    {
        // Standard International Morse Code, dits/dahs as '.'/'-'.
        private static readonly Dictionary<string, string> Table = new Dictionary<string, string>
        {
            [".-"] = "A",
            ["-..."] = "B",
            ["-.-."] = "C",
            ["-.."] = "D",
            ["."] = "E",
            ["..-."] = "F",
            ["--."] = "G",
            ["...."] = "H",
            [".."] = "I",
            [".---"] = "J",
            ["-.-"] = "K",
            [".-.."] = "L",
            ["--"] = "M",
            ["-."] = "N",
            ["---"] = "O",
            [".--."] = "P",
            ["--.-"] = "Q",
            [".-."] = "R",
            ["..."] = "S",
            ["-"] = "T",
            ["..-"] = "U",
            ["...-"] = "V",
            [".--"] = "W",
            ["-..-"] = "X",
            ["-.--"] = "Y",
            ["--.."] = "Z",
            ["-----"] = "0",
            [".----"] = "1",
            ["..---"] = "2",
            ["...--"] = "3",
            ["....-"] = "4",
            ["....."] = "5",
            ["-...."] = "6",
            ["--..."] = "7",
            ["---.."] = "8",
            ["----."] = "9",
            // Punctuation. NOTE: ".-.-." and ".-..." are deliberately
            // omitted here and instead mapped to their prosign meanings
            // (AR and AS) below - in standard Morse, "+" and "AR" share
            // the exact same pattern by design (likewise "&" and "AS"),
            // so a decoder with no surrounding context can't distinguish
            // them; for a CW trainer, the prosign meaning is the more
            // useful one to surface.
            [".-.-.-"] = ".",
            ["--..--"] = ",",
            ["..--.."] = "?",
            [".----."] = "'",
            ["-.-.--"] = "!",
            ["-..-."] = "/",
            ["-.--."] = "(",
            ["-.--.-"] = ")",
            ["---..."] = ":",
            ["-.-.-."] = ";",
            ["-...-"] = "=",
            ["-....-"] = "-",
            ["..--.-"] = "_",
            [".-..-."] = "\"",
            ["...-..-"] = "$",
            [".--.-."] = "@",

            // Prosigns (procedural signs) - two letters sent run-together
            // with no inter-letter space, each with its own conventional
            // meaning, verified against standard references:
            //   SK  ...-.-   end of contact
            //   BK  -...-.-  invite receiving station to transmit / break
            //   AR  .-.-.    end of message (clashes with "+" - see note above, prosign meaning chosen)
            //   AS  .-...    wait / stand by (clashes with "&" - see note above, prosign meaning chosen)
            //   KA  -.-.-    beginning of message
            //   KN  -.--.    invitation to a specific station only
            //   SOS ...---...  distress call (verified 9-element pattern, NOT the 7-dit "error" signal)
            ["...-.-"] = "SK",
            ["-...-.-"] = "BK",
            [".-.-."] = "AR",
            [".-..."] = "AS",
            ["-.-.-"] = "KA",
            ["-.--."] = "KN",
            ["...---..."] = "SOS",
        };

        /// <summary>Text returned when decode is attempted but the pattern doesn't match any known entry, or any mark is too poorly timed to trust (MarkQuality.Bad).</summary>
        public const string UndecodedText = "~";

        /// <summary>
        /// Attempts to decode a completed CharacterGroup. Returns the
        /// decoded text (usually one character, occasionally a multi-letter
        /// prosign like "SK"), or UndecodedText on failure. Does NOT modify
        /// group.DecodedText itself - that's left to the caller (typically
        /// ElementHistory, right after a character completes) so this
        /// class stays a pure function with no side effects.
        /// </summary>
        public static string Decode(CharacterGroup group, double ditLengthMs,
            double goodToleranceFraction, double poorToleranceFraction)
        {
            if (group == null || group.Elements.Count == 0)
                return UndecodedText;

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
                    return UndecodedText;
                }

                pattern.Append(isDit ? '.' : '-');
            }

            if (pattern.Length == 0) return UndecodedText;

            return Table.TryGetValue(pattern.ToString(), out string decoded) ? decoded : UndecodedText;
        }
    }
}