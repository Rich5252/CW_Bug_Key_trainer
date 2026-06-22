// Compile-time switch between two calibration strategies:
//   - (undefined) GAP-SPLIT: groups marks by finding the largest gap in
//     sorted durations (original approach) - works on a single character's
//     marks alone, doesn't need to see the spaces between them.
//   - USE_PARIS_BURST_CALIBRATION: requires a clean run of exactly 5 marks
//     + 4 intervening spaces (the shape of sending "5" - five dits with
//     four inter-element spaces between them), checks that all 9 durations
//     fall within a tolerance of each other (consistent dit-rate sending),
//     then derives WPM directly from (total span)/9 rather than averaging
//     marks alone. This validates spacing consistency too, not just mark
//     consistency - arguably a stronger sanity check for a clean
//     calibration burst, at the cost of requiring the space data
//     (Elements, not just MarkDurationsMs) and being strict about needing
//     exactly that 5-mark/4-space shape.
//
// To switch strategies, comment/uncomment the line below (or define it in
// your project's Build Properties > Conditional compilation symbols for a
// no-code-edit toggle).
#define USE_PARIS_BURST_CALIBRATION

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

        /// <summary>
        /// (max - min) / average of the durations used in this
        /// calibration - a measure of how consistent the burst was.
        /// 0 = perfectly uniform; e.g. 0.08 = 8% spread. Useful to log
        /// alongside the WPM result so you can see how "clean" each
        /// calibration attempt was over time, even on success.
        /// </summary>
        public double VarianceFraction { get; }

        private CalibrationResult(bool success, string failureReason, double ditLengthMs, double wpm, int ditsUsed, double varianceFraction)
        {
            Success = success;
            FailureReason = failureReason;
            DitLengthMs = ditLengthMs;
            Wpm = wpm;
            DitsUsed = ditsUsed;
            VarianceFraction = varianceFraction;
        }

        public static CalibrationResult Fail(string reason, double varianceFraction = 0) =>
            new CalibrationResult(false, reason, 0, 0, 0, varianceFraction);

        public static CalibrationResult Ok(double ditLengthMs, int ditsUsed, double varianceFraction)
        {
            // Standard WPM formula (PARIS standard): one dit = 1200/WPM ms.
            double wpm = 1200.0 / ditLengthMs;
            return new CalibrationResult(true, null, ditLengthMs, wpm, ditsUsed, varianceFraction);
        }
    }

    public static class WpmCalibrator
    {
        public const int MinDitsRequired = 5;

        /// <summary>
        /// Tolerance for the PARIS-burst strategy: the spread between the
        /// shortest and longest of the 9 durations (5 marks + 4 spaces)
        /// must be within this fraction of their average. Tweak this
        /// const to loosen/tighten how strict the consistency check is.
        /// 15% accommodates the natural mechanical variability of a bug
        /// key's spring-driven dits, which won't be as perfectly uniform
        /// as an electronic keyer.
        /// </summary>
        public const double ParisBurstToleranceFraction = 0.15;

#if USE_PARIS_BURST_CALIBRATION

        /// <summary>
        /// Calibrates from a clean burst of exactly 5 marks + 4 spaces
        /// (the element shape of sending "5"). Requires ALL 9 durations to
        /// fall within ParisBurstToleranceFraction of their average -
        /// validates that both the dits AND the spaces between them were
        /// sent consistently, not just the marks. WPM is derived from the
        /// total span (sum of all 9 durations) / 9, rather than averaging
        /// marks alone - total elapsed time over a known number of
        /// dit-widths is a more direct measurement than averaging
        /// individual (noisier) element durations.
        /// </summary>
        public static CalibrationResult Calibrate(List<Element> elements)
        {
            if (elements == null)
                return CalibrationResult.Fail("No elements in the selected character.");

            // Expect the burst to start with a mark (spaces only exist
            // BETWEEN marks within a character - there's never a leading
            // space inside a character group, by construction of
            // ElementHistory). Take exactly the first 9 elements if
            // present; a clean "5" produces exactly mark,space,mark,space,
            // mark,space,mark,space,mark = 9 elements, no more, no less.
            if (elements.Count != 9)
            {
                return CalibrationResult.Fail(
                    $"Expected exactly 9 elements (5 marks + 4 spaces, e.g. sending \"5\"), " +
                    $"got {elements.Count}. Send a clean five-dit character and try again.");
            }

            for (int i = 0; i < 9; i++)
            {
                bool expectedIsMark = (i % 2 == 0); // positions 0,2,4,6,8 = marks; 1,3,5,7 = spaces
                if (elements[i].IsMark != expectedIsMark)
                {
                    return CalibrationResult.Fail(
                        "Element sequence doesn't match the expected mark/space/mark/... pattern for a clean 5-dit burst.");
                }
            }

            var durations = elements.Select(e => e.DurationMs).ToList();
            double average = durations.Average();
            double min = durations.Min();
            double max = durations.Max();
            double spreadFraction = (max - min) / average;

            if (spreadFraction > ParisBurstToleranceFraction)
            {
                return CalibrationResult.Fail(
                    $"Timing too inconsistent for calibration: durations ranged {min:F1}-{max:F1}ms " +
                    $"({spreadFraction:P0} spread, average {average:F1}ms) - tolerance is {ParisBurstToleranceFraction:P0}. " +
                    "Try sending a steadier, more evenly-paced \"5\".",
                    spreadFraction);
            }

            double totalSpanMs = durations.Sum();
            double ditLengthMs = totalSpanMs / 9.0;

            return CalibrationResult.Ok(ditLengthMs, ditsUsed: 5, spreadFraction);
        }

#else

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

            int splitIndex = FindLargestGapIndex(sorted);

            List<double> shortGroup = sorted.Take(splitIndex + 1).ToList();

            if (shortGroup.Count < MinDitsRequired)
            {
                return CalibrationResult.Fail(
                    $"Only {shortGroup.Count} mark(s) identified as dits (rest appear to be dahs); " +
                    $"need at least {MinDitsRequired}. Try sending an all-dit character (e.g. \"5\").");
            }

            double averageDitMs = shortGroup.Average();
            double minDit = shortGroup.Min();
            double maxDit = shortGroup.Max();
            double varianceFraction = (maxDit - minDit) / averageDitMs;

            return CalibrationResult.Ok(averageDitMs, shortGroup.Count, varianceFraction);
        }

        private static int FindLargestGapIndex(List<double> sorted)
        {
            if (sorted.Count == 1) return 0;

            int largestGapIndex = sorted.Count - 1;
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

            double shortGroupSpread = sorted[largestGapIndex] - sorted[0];
            double minimumMeaningfulGap = Math.Max(shortGroupSpread, 5.0);

            if (largestGap < minimumMeaningfulGap)
            {
                return sorted.Count - 1;
            }

            return largestGapIndex;
        }

#endif
    }
}