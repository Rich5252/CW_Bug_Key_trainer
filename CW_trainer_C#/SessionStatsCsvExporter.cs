using System;
using System.Collections.Generic;
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

            sb.AppendLine($"Exported,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Total characters sent,{stats.TotalCharactersCompleted}");
            sb.AppendLine($"Distinct characters,{stats.PerCharacter.Count}");
            sb.AppendLine();

            sb.AppendLine("Characters");
            sb.AppendLine("Character,NChars,NElements,MeanAbsDeviation%,MeanSignedDeviation%,StdDeviation%,Spread%");

            var charRows = new List<(string Key, int NChars, int Count, double MeanAbsDev, double MeanSignedDev, double StdDev, double Spread)>();

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

                charRows.Add((charKey, breakdown.CharacterCount, totalCount, meanAbsDev, meanSignedDev, stdDev, spread));

                sb.AppendLine(string.Join(",",
                    CsvField(charKey),
                    breakdown.CharacterCount.ToString(CultureInfo.InvariantCulture),
                    totalCount.ToString(CultureInfo.InvariantCulture),
                    Round(meanAbsDev), Round(meanSignedDev), Round(stdDev), Round(spread)));
            }

            AppendWeightedSummaryRow(sb, charRows.Select(r => (r.Count, r.MeanAbsDev, r.MeanSignedDev, r.StdDev, r.Spread)).ToList(),
                extraLeadingColumn: charRows.Sum(r => r.NChars).ToString(CultureInfo.InvariantCulture));

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

            var roleRows = new List<(int Count, double MeanAbsDev, double MeanSignedDev, double StdDev, double Spread)>();

            foreach (var (role, label) in roleLabels.OrderBy(r => r.Label, StringComparer.Ordinal))
            {
                StatBucket bucket = stats.Global.For(role);
                if (bucket.Count == 0) continue;

                double meanAbsDev = bucket.MeanAbsoluteDeviation * 100.0;
                double meanSignedDev = bucket.MeanSignedDeviation * 100.0;
                double stdDev = bucket.StdDeviation * 100.0;
                double spread = bucket.SpreadFraction * 100.0;

                roleRows.Add((bucket.Count, meanAbsDev, meanSignedDev, stdDev, spread));

                sb.AppendLine(string.Join(",",
                    CsvField(label),
                    bucket.Count.ToString(CultureInfo.InvariantCulture),
                    Round(meanAbsDev), Round(meanSignedDev), Round(stdDev), Round(spread)));
            }

            AppendWeightedSummaryRow(sb, roleRows);

            return sb.ToString();
        }

        /// <summary>
        /// Appends one summary row: Count is a plain SUM (total samples in
        /// this block); the deviation/spread columns are sample-count-WEIGHTED
        /// averages, not plain averages of the per-row averages - this
        /// matters because each row already averages across however many
        /// samples it has, so a character sent 40 times and one sent twice
        /// would otherwise count equally toward an overall figure, which
        /// would misrepresent how the session actually went. Weighting by
        /// Count keeps frequently-sent items appropriately dominant in the
        /// OVERALL figure, even though the per-row Pareto sort deliberately
        /// ignores frequency (a different, intentional choice for triage
        /// vs. this row's purpose of representing the whole block).
        /// </summary>
        private static void AppendWeightedSummaryRow(StringBuilder sb,
            List<(int Count, double MeanAbsDev, double MeanSignedDev, double StdDev, double Spread)> rows,
            string extraLeadingColumn = null)
        {
            string countsPrefix = extraLeadingColumn != null ? extraLeadingColumn + "," : "";

            if (rows.Count == 0)
            {
                sb.AppendLine($"TOTAL,{countsPrefix}0,,,,");
                return;
            }

            int totalCount = rows.Sum(r => r.Count);
            if (totalCount == 0)
            {
                sb.AppendLine($"TOTAL,{countsPrefix}0,,,,");
                return;
            }

            double weightedMeanAbsDev = rows.Sum(r => r.MeanAbsDev * r.Count) / totalCount;
            double weightedMeanSignedDev = rows.Sum(r => r.MeanSignedDev * r.Count) / totalCount;
            double weightedStdDev = rows.Sum(r => r.StdDev * r.Count) / totalCount;
            double weightedSpread = rows.Sum(r => r.Spread * r.Count) / totalCount;

            var fields = new List<string> { "TOTAL (count-weighted avg)" };
            if (extraLeadingColumn != null) fields.Add(extraLeadingColumn);
            fields.Add(totalCount.ToString(CultureInfo.InvariantCulture));
            fields.Add(Round(weightedMeanAbsDev));
            fields.Add(Round(weightedMeanSignedDev));
            fields.Add(Round(weightedStdDev));
            fields.Add(Round(weightedSpread));

            sb.AppendLine(string.Join(",", fields));
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