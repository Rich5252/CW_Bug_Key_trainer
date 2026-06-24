using System.Collections.Generic;
using System.Linq;

namespace CwTrainer.Serial
{
    /// <summary>One row of Pareto-ready data: a category label, its badness value (as a percentage), and how many samples it's based on.</summary>
    public readonly struct ParetoEntry
    {
        public string Label { get; }
        public double ValuePercent { get; }
        public int SampleCount { get; }

        public ParetoEntry(string label, double valuePercent, int sampleCount)
        {
            Label = label;
            ValuePercent = valuePercent;
            SampleCount = sampleCount;
        }
    }

    public enum ParetoMetric
    {
        MeanAbsoluteDeviation,
        SpreadFraction,
    }

    /// <summary>
    /// Builds sorted, Pareto-ready data from SessionStats - one set of
    /// helpers for the "by character" view, one for the "by element role"
    /// view. Pure data shaping, no UI - ParetoChartControl consumes the
    /// resulting List&lt;ParetoEntry&gt; directly.
    /// </summary>
    public static class ParetoDataBuilder
    {
        /// <summary>
        /// Builds one entry per character that has at least one sample,
        /// sorted descending by the chosen metric (pure badness ranking,
        /// NOT weighted by sample count - a rarely-sent character that's
        /// always bad ranks just as high as a frequently-sent one, by
        /// design, per your call on this).
        /// </summary>
        public static List<ParetoEntry> BuildByCharacter(SessionStats stats, ParetoMetric metric)
        {
            var entries = new List<ParetoEntry>();

            foreach (var kvp in stats.PerCharacter)
            {
                string charLabel = kvp.Key;
                RoleBreakdown breakdown = kvp.Value;

                // Combine all roles' samples for this character into one
                // "how bad is this character overall" figure - simplest
                // approach: average the metric across whichever roles
                // actually have samples for this character (not every
                // character has dahs, e.g. "E" is dit-only).
                var applicableBuckets = new[]
                {
                    breakdown.Dits, breakdown.Dahs,
                    breakdown.IntraCharacterSpaces, breakdown.InterCharacterSpaces, breakdown.WordSpaces,
                }.Where(b => b.Count > 0).ToList();

                if (applicableBuckets.Count == 0) continue;

                double value = metric == ParetoMetric.MeanAbsoluteDeviation
                    ? applicableBuckets.Average(b => b.MeanAbsoluteDeviation)
                    : applicableBuckets.Average(b => b.SpreadFraction);

                int totalSamples = applicableBuckets.Sum(b => b.Count);

                entries.Add(new ParetoEntry(charLabel, value * 100.0, totalSamples));
            }

            return entries.OrderByDescending(e => e.ValuePercent).ToList();
        }

        /// <summary>
        /// Builds one entry per element role (Dit, Dah, IntraCharacterSpace,
        /// InterCharacterSpace, WordSpace) from the GLOBAL breakdown,
        /// sorted descending by the chosen metric. Roles with zero samples
        /// are omitted (e.g. WordSpace before any word-spaces have been sent).
        /// </summary>
        public static List<ParetoEntry> BuildByRole(SessionStats stats, ParetoMetric metric)
        {
            var roleLabels = new (ElementRole Role, string Label)[]
            {
                (ElementRole.Dit, "Dit"),
                (ElementRole.Dah, "Dah"),
                (ElementRole.IntraCharacterSpace, "Intra-char space"),
                (ElementRole.InterCharacterSpace, "Inter-char space"),
                (ElementRole.WordSpace, "Word space"),
            };

            var entries = new List<ParetoEntry>();

            foreach (var (role, label) in roleLabels)
            {
                StatBucket bucket = stats.Global.For(role);
                if (bucket.Count == 0) continue;

                double value = metric == ParetoMetric.MeanAbsoluteDeviation
                    ? bucket.MeanAbsoluteDeviation
                    : bucket.SpreadFraction;

                entries.Add(new ParetoEntry(label, value * 100.0, bucket.Count));
            }

            return entries.OrderByDescending(e => e.ValuePercent).ToList();
        }
    }
}