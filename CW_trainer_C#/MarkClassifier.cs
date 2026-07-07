namespace CwTrainer.Serial
{
    public enum MarkQuality
    {
        /// <summary>Within the Good window (DitMinGood-DitMaxGood or DahMinGood-DahMaxGood).</summary>
        Good,
        /// <summary>Outside Good but within the Warn window - acceptable but notably off.</summary>
        Warn,
        /// <summary>Outside the Warn window entirely - too far from either ideal to trust for decoding.</summary>
        Bad,
    }

    /// <summary>
    /// Classifies a single mark's duration against the calibrated dit
    /// length using explicit min/max ratio windows from TrainerSettings,
    /// rather than symmetric ±fraction bands. This allows independent
    /// tuning of the dit and dah acceptance ranges, matching the approach
    /// used in the on-air decoder (MIN/MAX_DOT_WINDOW, MIN/MAX_DASH_WINDOW).
    ///
    /// This remains the SINGLE shared definition of mark quality - both
    /// TimelineView's color-coding and MorseDecoder's strict-reject-on-Bad
    /// rule use this, so they can never disagree.
    /// </summary>
    public static class MarkClassifier
    {
        /// <summary>
        /// Classifies a mark using TrainerSettings min/max windows.
        /// isDit is set based on whether the ratio is closer to 1.0 (dit)
        /// or 3.0 (dah), using settings.DitDahBoundary as the split point.
        /// </summary>
        public static MarkQuality Classify(double durationMs, double ditLengthMs,
            TrainerSettings settings, out bool isDit)
        {
            if (ditLengthMs <= 0)
            {
                isDit = true;
                return MarkQuality.Good;
            }

            double ratio = durationMs / ditLengthMs;
            isDit = ratio < settings.DitDahBoundary;

            if (isDit)
            {
                if (ratio >= settings.DitMinGood && ratio <= settings.DitMaxGood) return MarkQuality.Good;
                if (ratio >= settings.DitMinWarn && ratio <= settings.DitMaxWarn) return MarkQuality.Warn;
                return MarkQuality.Bad;
            }
            else
            {
                if (ratio >= settings.DahMinGood && ratio <= settings.DahMaxGood) return MarkQuality.Good;
                if (ratio >= settings.DahMinWarn && ratio <= settings.DahMaxWarn) return MarkQuality.Warn;
                return MarkQuality.Bad;
            }
        }

        /// <summary>
        /// Legacy overload using symmetric tolerance fractions, kept for
        /// any call sites that pass dummy 1.0/1.0 values purely to get
        /// the isDit classification (e.g. SessionStats.ClassifyRole).
        /// For those callers, quality is irrelevant - only isDit matters.
        /// </summary>
        public static MarkQuality Classify(double durationMs, double ditLengthMs,
            double goodToleranceFraction, double poorToleranceFraction,
            out bool isDit)
        {
            if (ditLengthMs <= 0) { isDit = true; return MarkQuality.Good; }

            double ratio = durationMs / ditLengthMs;
            isDit = ratio < 2.0;
            double nearestIdeal = isDit ? 1.0 : 3.0;
            double dev = System.Math.Abs(ratio - nearestIdeal) / nearestIdeal;

            if (dev <= goodToleranceFraction) return MarkQuality.Good;
            if (dev <= poorToleranceFraction) return MarkQuality.Warn;
            return MarkQuality.Bad;
        }
    }
}