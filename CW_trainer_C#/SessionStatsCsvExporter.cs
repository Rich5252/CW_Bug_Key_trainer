using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CwTrainer.Serial
{
    /// <summary>
    /// Builds a CSV-formatted string from SessionStats, for archiving to
    /// Excel via clipboard copy-paste. Two blocks in one CSV: characters
    /// first (alphabetically sorted by decoded character), then element
    /// roles (alphabetically sorted by role name), each with deviation
    /// columns and sample count.
    /// </summary>
    public static class SessionStatsCsvExporter
    {
        public static string BuildCsv(SessionStats stats)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Characters");
            sb.AppendLine("Character,Count,MeanAbsDeviation%,MeanSignedDeviation%,StdDeviation%,Spread%");

            foreach (var charKey in stats.PerCharacter.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                RoleBreakdown breakdown = stats.PerCharacter[charKey];

                var applicableBuckets = new[]
                {
                    breakdown.Dits, breakdown.Dahs,
                    breakdown.IntraCharacterSpaces, breakdown.InterCharacterSpaces, breakdown.WordSpaces,
                }.Where(b => b.Count > 0).ToList();

                if (applicableBuckets.Count == 0) continue;

                int totalCount = applicableBuckets.Sum(b => b.Count);
                double meanAbsDev = applicableBuckets.Average(b => b.MeanAbsoluteDeviation) * 100.0;
                double meanSignedDev = applicableBuckets.Average(b => b.MeanSignedDeviation) * 100.0;
                double stdDev = applicableBuckets.Average(b => b.StdDeviation) * 100.0;
                double spread = applicableBuckets.Average(b => b.SpreadFraction) * 100.0;

                sb.AppendLine(string.Join(",",
                    CsvField(charKey),
                    totalCount.ToString(CultureInfo.InvariantCulture),
                    Round(meanAbsDev), Round(meanSignedDev), Round(stdDev), Round(spread)));
            }

            sb.AppendLine();
            sb.AppendLine("Elements");
            sb.AppendLine("Role,Count,MeanAbsDeviation%,MeanSignedDeviation%,StdDeviation%,Spread%");

            var roleLabels = new (ElementRole Role, string Label)[]
            {
                (ElementRole.Dit, "Dit"),
                (ElementRole.Dah, "Dah"),
                (ElementRole.IntraCharacterSpace, "Intra-char space"),
                (ElementRole.InterCharacterSpace, "Inter-char space"),
                (ElementRole.WordSpace, "Word space"),
            };

            foreach (var (role, label) in roleLabels.OrderBy(r => r.Label, StringComparer.Ordinal))
            {
                StatBucket bucket = stats.Global.For(role);
                if (bucket.Count == 0) continue;

                sb.AppendLine(string.Join(",",
                    CsvField(label),
                    bucket.Count.ToString(CultureInfo.InvariantCulture),
                    Round(bucket.MeanAbsoluteDeviation * 100.0),
                    Round(bucket.MeanSignedDeviation * 100.0),
                    Round(bucket.StdDeviation * 100.0),
                    Round(bucket.SpreadFraction * 100.0)));
            }

            return sb.ToString();
        }

        private static string Round(double value) =>
            Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);

        /// <summary>Quotes a field if it contains a comma, quote, or newline - standard CSV escaping. Most labels here (single letters, role names) won't need it, but this keeps it correct if a prosign like "SK" or punctuation character ever appears as a key.</summary>
        private static string CsvField(string value)
        {
            if (value == null) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }
    }
}