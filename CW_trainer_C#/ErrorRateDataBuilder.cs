using System.Collections.Generic;
using System.Linq;
using static CwTrainer.Display.ErrorRateChartControl;

namespace CwTrainer.Serial
{
    public static class ErrorRateDataBuilder
    {
        /// <summary>Builds one entry per element role, sorted by bad rate descending.</summary>
        public static List<ErrorRateEntry> BuildByRole(SessionStats stats)
        {
            var roleLabels = new (ElementRole Role, string Label)[]
            {
                (ElementRole.Dit,                "Dit"),
                (ElementRole.Dah,                "Dah"),
                (ElementRole.IntraCharacterSpace, "Intra-char"),
                (ElementRole.InterCharacterSpace, "Inter-char"),
                (ElementRole.WordSpace,           "Word space"),
            };

            return roleLabels
                .Select(r => new ErrorRateEntry(r.Label, stats.Global.For(r.Role)))
                .Where(e => e.Count > 0)
                .OrderByDescending(e => e.ShortBad + e.LongBad) // worst bad rate first
                .ToList();
        }

        /// <summary>Builds one entry per decoded character, sorted by bad rate descending.</summary>
        public static List<ErrorRateEntry> BuildByCharacter(SessionStats stats)
        {
            var entries = new List<ErrorRateEntry>();

            foreach (var kvp in stats.PerCharacter)
            {
                var breakdown = kvp.Value;

                // Aggregate all role buckets for this character into one
                // combined band-count entry.
                int shortBad = 0, shortWarn = 0, good = 0, longWarn = 0, longBad = 0, count = 0;
                foreach (ElementRole role in System.Enum.GetValues(typeof(ElementRole)))
                {
                    StatBucket b = breakdown.For(role);
                    shortBad += b.ShortBad;
                    shortWarn += b.ShortWarn;
                    good += b.Good;
                    longWarn += b.LongWarn;
                    longBad += b.LongBad;
                    count += b.Count;
                }

                if (count > 0)
                    entries.Add(new ErrorRateEntry(kvp.Key, shortBad, shortWarn, good, longWarn, longBad, count));
            }

            return entries
                .OrderByDescending(e => e.ShortBad + e.LongBad)
                .ToList();
        }
    }
}