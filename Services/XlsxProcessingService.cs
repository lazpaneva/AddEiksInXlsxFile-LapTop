using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace AddEiksInXlsxFile.Services
{
    public class XlsxProcessingService
    {
        private readonly XlsxService _xlsxService;

        public XlsxProcessingService(XlsxService xlsxService)
        {
            _xlsxService = xlsxService;
        }

        public ProcessResult ProcessAndSort(string file1Name, string file2Name, int file1CompanyCol, int file2CompanyCol)
        {
            var file1Path = _xlsxService.GetFilePath(file1Name);
            var file2Path = _xlsxService.GetFilePath(file2Name);

            using var wb1 = new XLWorkbook(file1Path);
            using var wb2 = new XLWorkbook(file2Path);

            var ws1 = wb1.Worksheets.First();
            var ws2 = wb2.Worksheets.First();

            int file1EikCol = DetectEikColumn(ws1, file1CompanyCol);

            var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            var lastRow1 = ws1.LastRowUsed()?.RowNumber() ?? 0;
            for (int r = 2; r <= lastRow1; r++)
            {
                var name = ws1.Cell(r, file1CompanyCol).GetString();
                var norm = StringNormalizationService.NormalizeCompanyName(name);
                if (string.IsNullOrEmpty(norm)) continue;
                var eikRaw = ws1.Cell(r, file1EikCol).GetString().Trim();
                if (string.IsNullOrEmpty(eikRaw)) continue;
                var digits = Regex.Replace(eikRaw, "\\D", "");
                if (digits.Length != 9 && digits.Length != 10 && digits.Length != 12) continue;
                if (!map.TryGetValue(norm, out var set))
                {
                    set = new HashSet<string>();
                    map[norm] = set;
                }
                set.Add(digits);
            }

            // Prepare new workbook for sorted output
            var newWb = new XLWorkbook();
            var newWs = newWb.Worksheets.Add(ws2.Name ?? "Sheet1");

            // Copy header row (assume row 1 is header)
            ws2.Row(1).CopyTo(newWs.Row(1));

            int lastRow2 = ws2.LastRowUsed()?.RowNumber() ?? 0;
            var dataRows = new List<(string Norm, int OrigRow)>();
            for (int r = 2; r <= lastRow2; r++)
            {
                var name = ws2.Cell(r, file2CompanyCol).GetString();
                var norm = StringNormalizationService.NormalizeCompanyName(name);
                dataRows.Add((norm, r));
            }

            // Sort by normalized name ascending using ordinal ignore-case for determinism.
            // Place empty/null normalized names after non-empty ones.
            dataRows.Sort((a, b) =>
            {
                var an = a.Norm;
                var bn = b.Norm;
                var aEmpty = string.IsNullOrEmpty(an);
                var bEmpty = string.IsNullOrEmpty(bn);
                if (aEmpty && bEmpty) return 0;
                if (aEmpty) return 1; // a after b
                if (bEmpty) return -1; // a before b
                return StringComparer.OrdinalIgnoreCase.Compare(an, bn);
            });

            // Determine target column for EIK in output (file2CompanyCol + 1)
            int targetCol = file2CompanyCol + 1;

            int outRow = 2;
            int matchedCount = 0;
            foreach (var (norm, origRow) in dataRows)
            {
                // Copy original row into new sheet
                ws2.Row(origRow).CopyTo(newWs.Row(outRow));

                // Determine EIK value
                string eikValue = "!!!!";
                if (!string.IsNullOrEmpty(norm) && map.TryGetValue(norm, out var set))
                {
                    if (set.Count == 1)
                    {
                        eikValue = set.First();
                    }
                    else
                    {
                        eikValue = "!!!!";
                    }
                }

                // If there was no mapping, leave as "!!!!"
                newWs.Cell(outRow, targetCol).Value = eikValue;
                if (!string.IsNullOrEmpty(eikValue) && eikValue != "!!!!") matchedCount++;
                outRow++;
            }

            // Ensure header for EIK column
            if (newWs.Cell(1, targetCol).IsEmpty()) newWs.Cell(1, targetCol).Value = "EIK";
            // Remove any background fills from the generated sheet (do not preserve source fills)
            var usedRange = newWs.RangeUsed();
            if (usedRange != null)
            {
                foreach (var cell in usedRange.Cells())
                {
                    cell.Style.Fill.PatternType = ClosedXML.Excel.XLFillPatternValues.None;
                    cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.NoColor;
                }
            }

            // Save result with a new filename. Strategy:
            // 1) If caller provided a directory inside `file2Name`, try to use it.
            // 2) If that is not usable, try the current user's Downloads folder.
            // 3) Finally, fall back to the directory where file2 was read from (usually wwwroot/uploads).
            var resultName = MakeResultFileName(file2Name);
            string? chosenDir = null;

            // Helper: test whether a directory can be created/written to
            static bool CanUseDirectory(string dir)
            {
                try
                {
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    // try to create a small temp file
                    var testFile = Path.Combine(dir, Path.GetRandomFileName());
                    using (var fs = System.IO.File.Create(testFile)) { }
                    System.IO.File.Delete(testFile);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            try
            {
                var dirFromFile2Name = Path.GetDirectoryName(file2Name);
                if (!string.IsNullOrEmpty(dirFromFile2Name))
                {
                    if (!Path.IsPathRooted(dirFromFile2Name))
                    {
                        dirFromFile2Name = Path.Combine(Directory.GetCurrentDirectory(), dirFromFile2Name);
                    }
                    if (CanUseDirectory(dirFromFile2Name))
                    {
                        chosenDir = dirFromFile2Name;
                    }
                }
            }
            catch
            {
                // ignore
            }

            // If we couldn't use directory from File2Name, try user's Downloads folder
            if (string.IsNullOrEmpty(chosenDir))
            {
                try
                {
                    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    if (!string.IsNullOrEmpty(userProfile))
                    {
                        var downloads = Path.Combine(userProfile, "Downloads");
                        if (CanUseDirectory(downloads)) chosenDir = downloads;
                    }
                }
                catch
                {
                    // ignore
                }
            }

            // Final fallback: directory where file2 was read from (uploads)
            if (string.IsNullOrEmpty(chosenDir))
            {
                var fallback = Path.GetDirectoryName(file2Path);
                if (string.IsNullOrEmpty(fallback)) fallback = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(fallback)) Directory.CreateDirectory(fallback);
                chosenDir = fallback;
            }

            var resultPath = Path.Combine(chosenDir, resultName);
            newWb.SaveAs(resultPath);

            // Append diagnostic log so tests/users can inspect which directories were tried/chosen
            try
            {
                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);
                var logFile = Path.Combine(uploadsDir, "save-log.txt");
                var log = $"{DateTime.UtcNow:O} | file2Name='{file2Name}' | file2Path='{file2Path}' | chosenDir='{chosenDir}' | resultPath='{resultPath}'\r\n";
                System.IO.File.AppendAllText(logFile, log);
            }
            catch
            {
                // ignore logging failures
            }

            return new ProcessResult
            {
                OutputFileName = resultName,
                OutputFilePath = resultPath,
                TotalRows = dataRows.Count,
                MatchedCount = matchedCount,
                ErrorMessage = null
            };
        }

        private int DetectEikColumn(IXLWorksheet ws, int companyCol)
        {
            // Try to detect EIK column by header keywords in row 1
            var headerRow = ws.Row(1);
            var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? (companyCol + 1);
            for (int c = 1; c <= lastCol; c++)
            {
                var h = headerRow.Cell(c).GetString().Trim();
                if (string.IsNullOrEmpty(h)) continue;
                var lowered = h.ToLowerInvariant();
                if (lowered.Contains("eik") || lowered.Contains("uic") || lowered.Contains("bulstat") || lowered.Contains("uid"))
                {
                    return c;
                }
            }

            // Fallback: column to the right of company column
            return companyCol + 1;
        }

        private static string MakeResultFileName(string original)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(original);
            var ext = Path.GetExtension(original);
            var safe = string.IsNullOrWhiteSpace(nameWithoutExt) ? "result" : nameWithoutExt + "-result";
            return safe + (string.IsNullOrEmpty(ext) ? ".xlsx" : ext);
        }
    }
}
