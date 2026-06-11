using System.Text.RegularExpressions;
using AddEiksInXlsxFile.Models;
using ClosedXML.Excel;

namespace AddEiksInXlsxFile.Services
{
    public static class SearchOperatorService
    {
        private static readonly Regex EikRegex = new("^(\\d{9}|\\d{10}|\\d{13})$");

        public static bool IsValidOperatorEik(string eik) => EikRegex.IsMatch(eik);

        public static string TruncateText(string text, int maxLength = 35)
        {
            var trimmed = text.Trim();
            return trimmed.Length > maxLength ? trimmed.Substring(0, maxLength) : trimmed;
        }

        public static string GetOperatorResultPath(string sourcePath, string? preferredDirectory = null)
        {
            var fileName = Path.GetFileName(sourcePath);
            var outName = Path.GetFileNameWithoutExtension(fileName) + "-operator-result" + Path.GetExtension(fileName);
            if (!string.IsNullOrEmpty(preferredDirectory))
            {
                var preferredPath = Path.Combine(preferredDirectory, outName);
                if (File.Exists(preferredPath)) return preferredPath;
            }

            var directory = Path.GetDirectoryName(sourcePath) ?? Directory.GetCurrentDirectory();
            return Path.Combine(directory, outName);
        }

        public static List<SearchRow> ReadMissingEikRows(IXLWorksheet ws, int companyCol)
        {
            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 2;
            int eikCol = companyCol + 1;
            var rows = new List<SearchRow>();

            for (int r = 2; r <= lastRow; r++)
            {
                var name = ws.Cell(r, companyCol).GetString();
                var eik = ws.Cell(r, eikCol).GetString();
                if (!string.Equals(eik, "!!!!", StringComparison.Ordinal)) continue;

                var parts = new List<string>();
                for (int c = 1; c <= lastCol; c++)
                {
                    parts.Add(ws.Cell(r, c).GetString());
                }

                var full = string.Join(" | ", parts).Trim();
                var norm = StringNormalizationService.NormalizeCompanyName(name) ?? string.Empty;
                rows.Add(new SearchRow
                {
                    RowNumber = r,
                    CompanyName = name,
                    Eik = eik,
                    Normalized = norm,
                    FullRowText = full,
                    TruncatedText = TruncateText(full),
                    InputEik = string.Empty
                });
            }

            return rows;
        }

        public static void NormalizeCompanyNames(IXLWorksheet ws, int companyCol, int lastRow)
        {
            for (int row = 2; row <= lastRow; row++)
            {
                var normalized = StringNormalizationService.NormalizeCompanyName(ws.Cell(row, companyCol).GetString());
                ws.Cell(row, companyCol).Value = normalized;
            }
        }

        public static (int Applied, int ChangedRows, List<string> Errors) ApplyOperatorEdits(
            IXLWorksheet ws,
            int companyCol,
            int eikCol,
            IReadOnlyDictionary<string, string>? edits)
        {
            var errors = new List<string>();
            int applied = 0;
            int changedRows = 0;

            if (edits == null) return (applied, changedRows, errors);

            foreach (var kv in edits)
            {
                if (!int.TryParse(kv.Key, out var row))
                {
                    errors.Add($"Invalid row key: {kv.Key}");
                    continue;
                }

                var newEik = (kv.Value ?? string.Empty).Trim();
                var current = ws.Cell(row, eikCol).GetString();
                if (!string.Equals(current, "!!!!", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(current) &&
                    !string.Equals(current, newEik, StringComparison.Ordinal))
                {
                    errors.Add($"Row {row}: cell not marked with !!!!, skipped.");
                    continue;
                }

                if (string.IsNullOrEmpty(newEik))
                {
                    ws.Cell(row, eikCol).Clear(XLClearOptions.Contents);
                    changedRows++;
                    continue;
                }

                if (!IsValidOperatorEik(newEik))
                {
                    errors.Add($"Row {row}: invalid EIK '{newEik}'");
                    continue;
                }

                ws.Cell(row, eikCol).Value = newEik;
                applied++;
                changedRows++;
            }

            return (applied, changedRows, errors);
        }

        public static (int OriginalMissingRows, int ProcessedRows, int UniqueEiksCount) CountOperatorProgress(
            string sourcePath,
            IXLWorksheet editedWs,
            int eikCol,
            int editedLastRow)
        {
            using var sourceWb = new XLWorkbook(sourcePath);
            var sourceWs = sourceWb.Worksheets.First();
            var sourceLastRow = sourceWs.LastRowUsed()?.RowNumber() ?? 0;
            var lastRow = Math.Min(sourceLastRow, editedLastRow);
            var uniqueEiks = new HashSet<string>(StringComparer.Ordinal);
            var originalMissingRows = 0;
            var processedRows = 0;

            for (int row = 2; row <= lastRow; row++)
            {
                var original = sourceWs.Cell(row, eikCol).GetString().Trim();
                if (!string.Equals(original, "!!!!", StringComparison.Ordinal)) continue;

                originalMissingRows++;
                var edited = editedWs.Cell(row, eikCol).GetString().Trim();
                if (string.Equals(edited, "!!!!", StringComparison.Ordinal)) continue;

                processedRows++;
                if (!string.IsNullOrWhiteSpace(edited))
                {
                    uniqueEiks.Add(edited);
                }
            }

            return (originalMissingRows, processedRows, uniqueEiks.Count);
        }
    }
}
