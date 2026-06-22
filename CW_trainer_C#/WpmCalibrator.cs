using System;
using System.Collections.Generic;
using System.Linq;

namespace CwTrainer.Serial
{
    /// <summary>Result of a calibration attempt - always check Success before using DitLengthMs/Wpm.</summary>
    public readonly struct CalibrationResult
    {
        public bool Success { get; }
        public string FailureReason { get; }
        public double DitLengthMs { get; }
        public double Wpm { get; }
        public int DitsUsed { get; }

        private CalibrationResult(bool success, string failureReason, double ditLengthMs, double wpm, int ditsUsed)
        {
            Success = success;
            FailureReason = failureReason;
            DitLengthMs = ditLengthMs;
            Wpm = wpm;
            DitsUsed = ditsUsed;
        }

        public static CalibrationResult Fail(string reason) => new CalibrationResult(false, reason, 0, 0, 0);

        public static CalibrationResult Ok(double ditLengthMs, int ditsUsed)
        {
            // Standard WPM formula (PARIS standard): one dit = 1200/WPM ms.
            double wpm = 1200.0 / ditLengthMs;
            return new CalibrationResult(true, null, ditLengthMs, wpm, ditsUsed);
        }
    }

    /// <summary>
    /// Calibrates WPM from a single character's marks: splits marks into
    /// "short" (candidate dits) and "long" (candidate dahs) groups by
    /// finding the largest gap in sorted durations, then requires at least
    /// MinDitsRequired short marks to produce a result. Designed to be
    /// triggered by an explicit "Calibrate" button press after the
    /// operator sends a known all-dit (or mostly-dit) character.
    /// </summary>
    public static class WpmCalibrator
    {
        public const int MinDitsRequired = 5;

        /// <summary>
        /// Attempts calibration from a single character's mark durations.
        /// Pass CharacterGroup.MarkDurationsMs from the character you want
        /// to calibrate against (typically ElementHistory.LastCompletedCharacter).
        /// </summary>
        public static CalibrationResult Calibrate(List<double> markDurationsMs)
        {
            if (markDurationsMs == null || markDurationsMs.Count == 0)
                return CalibrationResult.Fail("No marks in the selected character.");

            if (markDurationsMs.Count < MinDitsRequired)
                return CalibrationResult.Fail(
                    $"Character has only {markDurationsMs.Count} mark(s); need at least {MinDitsRequired} dits. " +
                    "Try sending a character with more dits (e.g. \"5\").");

            var sorted = markDurationsMs.OrderBy(d => d).ToList();

            // Find the largest gap between consecutive sorted durations -
            // this is the natural dit/dah split point if the character
            // contains both. If the character is ALL dits (or all dahs),
            // there won't be a meaningfully large gap anywhere, and the
            // "short" group below will end up being all of them, which is
            // exactly what we want for an all-dits calibration character.
            int splitIndex = FindLargestGapIndex(sorted);

            List<double> shortGroup = sorted.Take(splitIndex + 1).ToList();

            if (shortGroup.Count < MinDitsRequired)
            {
                return CalibrationResult.Fail(
                    $"Only {shortGroup.Count} mark(s) identified as dits (rest appear to be dahs); " +
                    $"need at least {MinDitsRequired}. Try sending an all-dit character (e.g. \"5\").");
            }

            double averageDitMs = shortGroup.Average();
            return CalibrationResult.Ok(averageDitMs, shortGroup.Count);
        }

        /// <summary>
        /// Returns the index (within the sorted list) of the element just
        /// BEFORE the largest gap to the next element. Everything up to
        /// and including this index is the "short" group.
        ///
        /// If all values are nearly identical (e.g. a clean all-dit
        /// character), the largest gap will be small and could occur
        /// anywhere numerically - but since all values are close together
        /// regardless of where the "split" lands, the resulting short
        /// group will still correctly include all of them only if the gap
        /// search considers the WHOLE list as one group when no gap
        /// stands out. We handle that by comparing the largest gap found
        /// against the overall spread - if no gap is clearly dominant,
        /// treat the entire list as one group (all candidate dits).
        /// </summary>
        private static int FindLargestGapIndex(List<double> sorted)
        {
            if (sorted.Count == 1) return 0;

            int largestGapIndex = sorted.Count - 1; // default: no split, whole list is one group
            double largestGap = 0;

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                double gap = sorted[i + 1] - sorted[i];
                if (gap > largestGap)
                {
                    largestGap = gap;
                    largestGapIndex = i;
                }
            }

            // Require the largest gap to be meaningfully bigger than the
            // "noise" within the short group itself - otherwise small
            // natural jitter between dits could create a spurious split
            // partway through what should be one uniform group. Heuristic:
            // the gap must be at least as large as the spread (max-min)
            // of whatever would become the short group, with a minimum
            // floor so a tiny absolute spread doesn't make this
            // hypersensitive.
            double shortGroupSpread = sorted[largestGapIndex] - sorted[0];
            double minimumMeaningfulGap = Math.Max(shortGroupSpread, 5.0); // 5ms floor

            if (largestGap < minimumMeaningfulGap)
            {
                // No convincing split found - treat everything as one
                // group (the all-dits-or-all-dahs case).
                return sorted.Count - 1;
            }

            return largestGapIndex;
        }
    }
}