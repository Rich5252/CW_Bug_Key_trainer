using System;
using System.Collections.Generic;
using System.Linq;

namespace CwTrainer.Serial
{
    /// <summary>
    /// Accumulates raw duration samples for one category (e.g. "dits",
    /// "dahs", "inter-character spaces") and computes summary statistics
    /// on demand. Durations are stored normalized as a ratio against the
    /// category's ideal target (e.g. a dit's ideal is 1.0x DitLengthMs, a
    /// dah's is 3.0x) - this makes stats comparable across categories with
    /// different absolute ideal lengths (you can directly compare "dits
    /// are 4% off" against "dahs are 12% off" on the same scale).
    /// </summary>
    public sealed class StatBucket
    {
        private readonly List<double> _normalizedRatios = new List<double>();

        public int Count => _normalizedRatios.Count;

        /// <summary>Add one sample: the actual duration and the ideal target it should be compared against (e.g. DitLengthMs for a dit, 3*DitLengthMs for a dah).</summary>
        public void AddSample(double actualDurationMs, double idealMs)
        {
            if (idealMs <= 0) return; // guard against div-by-zero if DitLengthMs is ever 0/uncalibrated
            _normalizedRatios.Add(actualDurationMs / idealMs);
        }

        /// <summary>Mean of (actual/ideal) - 1.0 = perfectly on target, on average.</summary>
        public double MeanRatio => _normalizedRatios.Count > 0 ? _normalizedRatios.Average() : 0;

        /// <summary>Mean deviation from ideal, as a fraction (e.g. 0.08 = sending averages 8% off target, regardless of direction - see MeanSignedDeviation for direction).</summary>
        public double MeanAbsoluteDeviation =>
            _normalizedRatios.Count > 0 ? _normalizedRatios.Average(r => Math.Abs(r - 1.0)) : 0;

        /// <summary>Mean signed deviation from ideal (e.g. +0.08 = consistently 8% LONG; -0.08 = consistently 8% SHORT). Useful for spotting systematic bias vs. random jitter.</summary>
        public double MeanSignedDeviation =>
            _normalizedRatios.Count > 0 ? _normalizedRatios.Average(r => r - 1.0) : 0;

        /// <summary>Standard deviation of the (actual/ideal) ratio - lower = more consistent.</summary>
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

        /// <summary>(max - min) / mean - the simple spread metric, consistent with calibration's existing VarianceFraction approach.</summary>
        public double SpreadFraction => MeanRatio > 0 ? (MaxRatio - MinRatio) / MeanRatio : 0;

        public void Clear() => _normalizedRatios.Clear();
    }
}