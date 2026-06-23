namespace CwTrainer.Serial
{
    public enum MarkQuality
    {
        /// <summary>Within GoodToleranceFraction of its nearest ideal (dit or dah).</summary>
        Good,
        /// <summary>Outside Good but within PoorToleranceFraction - acceptable but notably off.</summary>
        Warn,
        /// <summary>Outside PoorToleranceFraction entirely - too far from either ideal to trust.</summary>
        Bad,
    }

    /// <summary>
    /// Classifies a single mark's duration against the calibrated dit
    /// length, snapping to whichever ideal (dit=1x, dah=3x) is closer.
    /// This is the SINGLE shared definition of "how close to ideal timing
    /// is this mark" - both TimelineView's color-coding and MorseDecoder's
    /// strict-decode-on-red-mark rule consume this same classification, so
    /// they can never disagree about what counts as acceptable.
    /// </summary>
    public static class MarkClassifier
    {
        public static MarkQuality Classify(double durationMs, double ditLengthMs,
            double goodToleranceFraction, double poorToleranceFraction,
            out bool isDit)
        {
            if (ditLengthMs <= 0)
            {
                isDit = true;
                return MarkQuality.Good;
            }

            double ratio = durationMs / ditLengthMs;
            // Snap to whichever ideal target (dit=1 or dah=3) is closer,
            // so a slightly-long dit isn't compared against a dah's target.
            double nearestIdeal = ratio < 2.0 ? 1.0 : 3.0;
            isDit = nearestIdeal == 1.0;

            double deviationFraction = System.Math.Abs(ratio - nearestIdeal) / nearestIdeal;

            if (deviationFraction <= goodToleranceFraction) return MarkQuality.Good;
            if (deviationFraction <= poorToleranceFraction) return MarkQuality.Warn;
            return MarkQuality.Bad;
        }
    }
}