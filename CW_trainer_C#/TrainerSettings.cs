namespace CwTrainer.Serial
{
    /// <summary>
    /// Single source of truth for all configurable timing limits and
    /// thresholds used throughout the trainer. One instance is created in
    /// MainForm and passed/set on every class that needs it, so there is
    /// exactly one place to change any default.
    ///
    /// Mark classification uses explicit min/max ratio WINDOWS (as
    /// multiples of DitLengthMs) rather than symmetric ±fraction bands,
    /// matching the approach used in the on-air decoder and allowing
    /// separate tuning of the dit and dah acceptance windows independently.
    ///
    /// All ratio values are multipliers of the calibrated DitLengthMs:
    ///   ratio = actualDurationMs / DitLengthMs
    /// so a perfect dit = 1.0, a perfect dah = 3.0.
    /// </summary>
    public sealed class TrainerSettings
    {
        // ----------------------------------------------------------------
        // Mark classification windows
        // Good  = green in timeline, accepted by decoder
        // Warn  = amber in timeline, still accepted by decoder
        // Bad   = red in timeline, rejected by decoder (decode fails)
        //
        // A mark is Good if its ratio falls inside [DitMinGood, DitMaxGood]
        // (for dits) or [DahMinGood, DahMaxGood] (for dahs).
        // A mark is Warn if it falls inside the wider [DitMinWarn, DitMaxWarn]
        // / [DahMinWarn, DahMaxWarn] but outside the Good window.
        // A mark is Bad if it falls outside the Warn window entirely.
        // ----------------------------------------------------------------

        // Dit windows (ideal = 1.0 × ditLengthMs)
        public double DitMinGood { get; set; } = 0.7;
        public double DitMaxGood { get; set; } = 1.3;
        public double DitMinWarn { get; set; } = 0.4;   // MIN_DOT_WINDOW from on-air decoder
        public double DitMaxWarn { get; set; } = 1.5;   // MAX_DOT_WINDOW from on-air decoder

        // Dah windows (ideal = 3.0 × ditLengthMs)
        public double DahMinGood { get; set; } = 2.5;
        public double DahMaxGood { get; set; } = 3.5;
        public double DahMinWarn { get; set; } = 2.0;   // MIN_DASH_WINDOW from on-air decoder
        public double DahMaxWarn { get; set; } = 5.0;   // MAX_DASH_WINDOW from on-air decoder

        // ----------------------------------------------------------------
        // Character boundary detection
        // ----------------------------------------------------------------

        /// <summary>A space of at least this many dit-widths closes the current character (real-space path).</summary>
        public double CharSpaceThresholdDits { get; set; } = 2.5;

        /// <summary>A closing space of at least this many dit-widths is also classified as a word-space (WasWordSpace = true).</summary>
        public double WordSpaceThresholdDits { get; set; } = 5.0;

        /// <summary>Silence backstop timeout fires at CharSpaceThresholdDits × TimeoutMultiplier dit-widths of silence. Must comfortably exceed WordSpaceThresholdDits to avoid closing a word-space before it's been fully measured.</summary>
        public double TimeoutMultiplier { get; set; } = 3.5;

        // ----------------------------------------------------------------
        // Calibration
        // ----------------------------------------------------------------

        /// <summary>PARIS-burst calibration tolerance: the spread (max-min)/average across all 9 burst elements must be within this fraction to accept the calibration.</summary>
        public double ParisBurstToleranceFraction { get; set; } = 0.15;

        // ----------------------------------------------------------------
        // Ideal space multiples for statistics (IdealMsFor in SessionStats)
        // ----------------------------------------------------------------

        public double IntraCharSpaceIdealDits { get; set; } = 1.0;
        public double InterCharSpaceIdealDits { get; set; } = 3.0;
        public double WordSpaceIdealDits { get; set; } = 7.0;

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        /// <summary>
        /// The midpoint between DahMinWarn and DitMaxWarn - used by
        /// MarkClassifier to decide whether a mark is "closer to a dit"
        /// or "closer to a dah" when it falls in an ambiguous zone.
        /// Defaults to 2.0 (the standard dit/dah boundary), matching the
        /// original ratio < 2.0 snap logic.
        /// </summary>
        public double DitDahBoundary { get; set; } = 2.0;
    }
}