using System;
using System.Collections.Generic;

namespace CwTrainer.Serial
{
    public enum ElementRole
    {
        Dit,
        Dah,
        IntraCharacterSpace,
        InterCharacterSpace,
        WordSpace,
    }

    /// <summary>
    /// One full set of per-role buckets - used both for the GLOBAL
    /// breakdown (all characters) and for each PER-CHARACTER breakdown
    /// (e.g. just "K", just "T"), so the same shape of data is available
    /// at both levels.
    /// </summary>
    public sealed class RoleBreakdown
    {
        public StatBucket Dits { get; } = new StatBucket();
        public StatBucket Dahs { get; } = new StatBucket();
        public StatBucket IntraCharacterSpaces { get; } = new StatBucket();
        public StatBucket InterCharacterSpaces { get; } = new StatBucket();
        public StatBucket WordSpaces { get; } = new StatBucket();

        /// <summary>
        /// How many times THIS character was completed/sent (e.g. how
        /// many times "E" itself was sent) - distinct from any single
        /// StatBucket's Count, which is an ELEMENT-level sample count
        /// (e.g. summed dit/space samples across all those E's). Only
        /// meaningful/incremented on a PerCharacter breakdown; the Global
        /// breakdown doesn't use this (TotalCharactersCompleted on
        /// SessionStats covers that already).
        /// </summary>
        public int CharacterCount { get; set; }

        public StatBucket For(ElementRole role) => role switch
        {
            ElementRole.Dit => Dits,
            ElementRole.Dah => Dahs,
            ElementRole.IntraCharacterSpace => IntraCharacterSpaces,
            ElementRole.InterCharacterSpace => InterCharacterSpaces,
            ElementRole.WordSpace => WordSpaces,
            _ => throw new ArgumentOutOfRangeException(nameof(role)),
        };

        public void Clear()
        {
            Dits.Clear();
            Dahs.Clear();
            IntraCharacterSpaces.Clear();
            InterCharacterSpaces.Clear();
            WordSpaces.Clear();
            CharacterCount = 0;
        }
    }

    /// <summary>
    /// Consumes completed CharacterGroups (via ElementHistory.CharacterCompleted)
    /// and builds up timing statistics broken down by element ROLE
    /// (dit/dah/intra-char space/inter-char space/word space) - both
    /// GLOBALLY (across the whole session) and PER DECODED CHARACTER (so
    /// you can see "my K's dahs are 18% off" separately from "my dahs
    /// overall are 9% off", surfacing which specific characters or
    /// sequences are giving you trouble vs. general technique issues).
    ///
    /// This is a pure data-accumulation class - no UI. Subscribe a display
    /// of your choosing to read Global / PerCharacter on demand, or after
    /// each Update() call if you want live-updating stats.
    /// </summary>
    public sealed class SessionStats
    {
        /// <summary>Stats across all characters sent this session.</summary>
        public RoleBreakdown Global { get; } = new RoleBreakdown();

        /// <summary>Stats broken down by which character was decoded - e.g. PerCharacter['K'] for just K's. Undecoded ('~') characters are still included, keyed under '~', since their timing data is still real and useful even though the letter itself is unknown.</summary>
        public Dictionary<string, RoleBreakdown> PerCharacter { get; } = new Dictionary<string, RoleBreakdown>();

        /// <summary>Total number of characters sent this session (every RecordCompletedCharacter call counts once, regardless of which letter or whether decode succeeded). Distinct from PerCharacter.Count, which is how many UNIQUE characters/letters have appeared.</summary>
        public int TotalCharactersCompleted { get; private set; }

        /// <summary>
        /// Feed one completed CharacterGroup into the stats. Call this from
        /// ElementHistory.CharacterCompleted, alongside your other
        /// subscribers (TimelineView's AttachHistory, MainForm's decoded
        /// text accumulation) - SessionStats doesn't subscribe itself, you
        /// wire it in explicitly so it's easy to have multiple independent
        /// stats instances (e.g. one for the whole session, one reset per
        /// drill) if useful later.
        /// </summary>
        public void RecordCompletedCharacter(CharacterGroup group, double ditLengthMs)
        {
            if (group == null || ditLengthMs <= 0) return;

            TotalCharactersCompleted++;

            string key = string.IsNullOrEmpty(group.DecodedText) ? "~" : group.DecodedText;
            if (!PerCharacter.TryGetValue(key, out RoleBreakdown perChar))
            {
                perChar = new RoleBreakdown();
                PerCharacter[key] = perChar;
            }

            perChar.CharacterCount++;

            var elements = group.Elements;
            for (int i = 0; i < elements.Count; i++)
            {
                Element element = elements[i];
                bool isLastElement = (i == elements.Count - 1);

                ElementRole role = ClassifyRole(element, isLastElement, group, ditLengthMs);
                double idealMs = IdealMsFor(role, ditLengthMs);

                Global.For(role).AddSample(element.DurationMs, idealMs);
                perChar.For(role).AddSample(element.DurationMs, idealMs);
            }
        }

        private static ElementRole ClassifyRole(Element element, bool isLastElement, CharacterGroup group, double ditLengthMs)
        {
            if (element.IsMark)
            {
                // Snap to nearest ideal (dit=1x, dah=3x), same convention
                // as MarkClassifier - reuse it so role assignment and the
                // timeline's green/amber/red coloring always agree about
                // which target a given mark is being judged against.
                MarkClassifier.Classify(element.DurationMs, ditLengthMs,
                    goodToleranceFraction: 1.0, poorToleranceFraction: 1.0, // tolerance irrelevant here, only isDit matters
                    out bool isDit);
                return isDit ? ElementRole.Dit : ElementRole.Dah;
            }

            // The LAST element in a completed CharacterGroup is the
            // closing space (real inter-character space, OR the
            // synthesized timeout space) - classify it as word-space or
            // inter-character-space based on WasWordSpace. Every other
            // space is intra-character (between elements of the same
            // letter).
            if (isLastElement)
            {
                return group.WasWordSpace ? ElementRole.WordSpace : ElementRole.InterCharacterSpace;
            }

            return ElementRole.IntraCharacterSpace;
        }

        private static double IdealMsFor(ElementRole role, double ditLengthMs) => role switch
        {
            ElementRole.Dit => ditLengthMs * 1.0,
            ElementRole.Dah => ditLengthMs * 3.0,
            ElementRole.IntraCharacterSpace => ditLengthMs * 1.0,
            ElementRole.InterCharacterSpace => ditLengthMs * 3.0,
            ElementRole.WordSpace => ditLengthMs * 7.0,
            _ => ditLengthMs,
        };

        public void Reset()
        {
            Global.Clear();
            PerCharacter.Clear();
            TotalCharactersCompleted = 0;
        }
    }
}