using CwTrainer.Serial;

namespace CwTrainer.Display
{
    /// <summary>
    /// Small extension to support the timeline view's layout calculations
    /// directly on CharacterGroup, now that TimelineView consumes
    /// ElementHistory/CharacterGroup as its single source of truth rather
    /// than maintaining its own separate TimelineRow/TimelineRowBuilder.
    /// </summary>
    public static class CharacterGroupExtensions
    {
        /// <summary>Total elapsed duration of this character's elements in ms - convenience for layout/scaling.</summary>
        public static double TotalDurationMs(this CharacterGroup group)
        {
            double sum = 0;
            foreach (var e in group.Elements) sum += e.DurationMs;
            return sum;
        }
    }
}