using System;
using System.Collections.Generic;
using System.Linq;

namespace CwTrainer.Serial
{
    public enum SampleBand
    {
        ShortBad,
        ShortWarn,
        Good,
        LongWarn,
        LongBad,
    }

    /// <summary>
    /// Accumulates raw duration samples for one category (e.g. "dits",
    /// "dahs", "inter-character spaces") and computes summary statistics
    /// on demand. Also tracks per-band counts (ShortBad/ShortWarn/Good/
    /// LongWarn/LongBad) using TrainerSettings windows, supporting the
    /// stacked error-rate chart.
    ///
    /// For SPACE roles, the band classification uses a simple
    /// short/good/long split based on the role's ideal multiple and the
    /// overall warn tolerance, since TrainerSettings only defines explicit
    /// windows for marks (dits/dahs). Spaces use a symmetric ±30%
    /// good window and ±50% warn window around the ideal by default,
    /// which can be extended to explicit space windows later if needed.
    /// </summary>
    public sealed class StatBucket
    {
        private readonly List<double> _normalizedRatios = new List<double>();

        // Band counts - incremented alongside _normalizedRatios
        public int ShortBad { get; private set; }
        public int ShortWarn { get; private set; }
        public int Good { get; private set; }
        public int LongWarn { get; private set; }
        public int LongBad { get; private set; }

        public int Count => _normalizedRatios.Count;
        public int BadCount => ShortBad + LongBad;
        public int WarnCount => ShortWarn + LongWarn;

        /// <summary>Percentage of samples classified as Bad (0-100).</summary>
        public double BadRatePct => Count > 0 ? (BadCount * 100.0) / Count : 0;
        /// <summary>Percentage of samples classified as Warn (0-100).</summary>
        public double WarnRatePct => Count > 0 ? (WarnCount * 100.0) / Count : 0;
        /// <summary>Percentage of samples classified as Good (0-100).</summary>
        public double GoodRatePct => Count > 0 ? (Good * 100.0) / Count : 0;

        /// <summary>
        /// Add a mark sample (dit or dah), classified using TrainerSettings
        /// min/max windows. idealMs should be the role's ideal duration
        /// (ditLengthMs for dits, 3×ditLengthMs for dahs). Pass isDit to
        /// select the correct window set. rawRatio (actual/ditLengthMs) is
        /// what the windows compare against.
        /// </summary>
        public void AddMarkSample(double actualDurationMs, double idealMs, double ditLengthMs,
            bool isDit, TrainerSettings settings)
        {
            if (idealMs <= 0 || ditLengthMs <= 0) return;

            _normalizedRatios.Add(actualDurationMs / idealMs);

            // Windows are defined as multiples of ditLengthMs
            double rawRatio = actualDurationMs / ditLengthMs;

            SampleBand band;
            if (isDit)
            {
                if (rawRatio < settings.DitMinWarn) band = SampleBand.ShortBad;
                else if (rawRatio < settings.DitMinGood) band = SampleBand.ShortWarn;
                else if (rawRatio <= settings.DitMaxGood) band = SampleBand.Good;
                else if (rawRatio <= settings.DitMaxWarn) band = SampleBand.LongWarn;
                else band = SampleBand.LongBad;
            }
            else
            {
                if (rawRatio < settings.DahMinWarn) band = SampleBand.ShortBad;
                else if (rawRatio < settings.DahMinGood) band = SampleBand.ShortWarn;
                else if (rawRatio <= settings.DahMaxGood) band = SampleBand.Good;
                else if (rawRatio <= settings.DahMaxWarn) band = SampleBand.LongWarn;
                else band = SampleBand.LongBad;
            }
            IncrementBand(band);
        }

        /// <summary>
        /// Add a space sample, classified using a symmetric window around
        /// the ideal (goodFraction = ±fraction for Good, warnFraction = ±
        /// fraction for Warn). Defaults match typical space tolerance.
        /// </summary>
        public void AddSpaceSample(double actualDurationMs, double idealMs,
            double goodFraction = 0.30, double warnFraction = 0.50)
        {
            if (idealMs <= 0) return;
            double ratio = actualDurationMs / idealMs;
            _normalizedRatios.Add(ratio);

            double dev = ratio - 1.0; // positive = long, negative = short
            SampleBand band;
            if (dev < -warnFraction) band = SampleBand.ShortBad;
            else if (dev < -goodFraction) band = SampleBand.ShortWarn;
            else if (dev <= goodFraction) band = SampleBand.Good;
            else if (dev <= warnFraction) band = SampleBand.LongWarn;
            else band = SampleBand.LongBad;

            IncrementBand(band);
        }

        /// <summary>
        /// Legacy AddSample - stores ratio for stats but classifies as Good
        /// (no band window info available). Used by call sites not yet
        /// migrated to AddMarkSample/AddSpaceSample.
        /// </summary>
        public void AddSample(double actualDurationMs, double idealMs)
        {
            if (idealMs <= 0) return;
            _normalizedRatios.Add(actualDurationMs / idealMs);
            Good++; // conservative: unknown classification treated as good
        }

        private void IncrementBand(SampleBand band)
        {
            switch (band)
            {
                case SampleBand.ShortBad: ShortBad++; break;
                case SampleBand.ShortWarn: ShortWarn++; break;
                case SampleBand.Good: Good++; break;
                case SampleBand.LongWarn: LongWarn++; break;
                case SampleBand.LongBad: LongBad++; break;
            }
        }

        // --- Existing stats properties, unchanged ---

        public double MeanRatio => _normalizedRatios.Count > 0 ? _normalizedRatios.Average() : 0;

        public double MeanAbsoluteDeviation =>
            _normalizedRatios.Count > 0 ? _normalizedRatios.Average(r => Math.Abs(r - 1.0)) : 0;

        public double MeanSignedDeviation =>
            _normalizedRatios.Count > 0 ? _normalizedRatios.Average(r => r - 1.0) : 0;

        public double StdDeviation
        {
            get
            {
                if (_normalizedRatios.Count < 2) return 0;
                double mean = MeanRatio;
                double sumSquares = _normalizedRatios.Sum(r => (r - mean) * (r - mean));
                return Math.Sqrt(sumSquares / (_normalizedRatios.Count - 1));
            }
        }

        public double MinRatio => _normalizedRatios.Count > 0 ? _normalizedRatios.Min() : 0;
        public double MaxRatio => _normalizedRatios.Count > 0 ? _normalizedRatios.Max() : 0;

        public double SpreadFraction => MeanRatio > 0 ? (MaxRatio - MinRatio) / MeanRatio : 0;

        public void Clear()
        {
            _normalizedRatios.Clear();
            ShortBad = ShortWarn = Good = LongWarn = LongBad = 0;
        }
    }
}