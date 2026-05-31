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

        public string ProcessAndSort(string file1Name, string file2Name, int file1CompanyCol, int file2CompanyCol)
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
                if (digits.Length != 9 && digits.Length != 12) continue;
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

            // Sort by normalized name ascending using current culture
            dataRows.Sort((a, b) => StringComparer.Create(CultureInfo.CurrentCulture, true).Compare(a.Norm, b.Norm));

            // Determine target column for EIK in output (file2CompanyCol + 1)
            int targetCol = file2CompanyCol + 1;

            int outRow = 2;
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
                outRow++;
            }

            // Ensure header for EIK column
            if (newWs.Cell(1, targetCol).IsEmpty()) newWs.Cell(1, targetCol).Value = "EIK";

            // Save result with a new filename
            var resultName = MakeResultFileName(file2Name);
            var resultPath = _xlsxService.GetFilePath(resultName);
            newWb.SaveAs(resultPath);

            return resultName;
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
