using Microsoft.AspNetCore.Mvc;
using AddEiksInXlsxFile.Models;
using AddEiksInXlsxFile.Services;
using System.Text.RegularExpressions;

namespace AddEiksInXlsxFile.Controllers
{
    using Microsoft.AspNetCore.Authorization;

    [Authorize]
    public class SearchController : Controller
    {
        private readonly XlsxService _xlsxService;
        private readonly StatisticsService _statisticsService;

        public SearchController(XlsxService xlsxService, StatisticsService statisticsService)
        {
            _xlsxService = xlsxService;
            _statisticsService = statisticsService;
        }

        [HttpGet]
        public IActionResult Index(string? sourceFile = null, int? file2Col = null)
        {
            // try Downloads first, then uploads
            string? path = null;
            if (!string.IsNullOrEmpty(sourceFile))
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var downloads = Path.Combine(userProfile ?? Directory.GetCurrentDirectory(), "Downloads");
                var p = Path.Combine(downloads, sourceFile);
                if (System.IO.File.Exists(p)) path = p;
                else
                {
                    var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    var pu = Path.Combine(uploads, sourceFile);
                    if (System.IO.File.Exists(pu)) path = pu;
                }
            }

            if (string.IsNullOrEmpty(path))
            {
                // find latest in Downloads or uploads
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var downloads = Path.Combine(userProfile ?? Directory.GetCurrentDirectory(), "Downloads");
                if (Directory.Exists(downloads))
                {
                    var files = Directory.GetFiles(downloads, "*-result.xlsx").OrderByDescending(System.IO.File.GetLastWriteTime).ToArray();
                    if (files.Length > 0) path = files[0];
                }
                if (string.IsNullOrEmpty(path))
                {
                    var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    if (Directory.Exists(uploads))
                    {
                        var files = Directory.GetFiles(uploads, "*-result.xlsx").OrderByDescending(System.IO.File.GetLastWriteTime).ToArray();
                        if (files.Length > 0) path = files[0];
                    }
                }
            }

            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                return View(new SearchViewModel());
            }

            var wb = new ClosedXML.Excel.XLWorkbook(path);
            var ws = wb.Worksheets.First();

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 2;
            var rows = new List<SearchRow>();

            int companyCol = (file2Col ?? 1);
            int eikCol = companyCol + 1;

            for (int r = 2; r <= lastRow; r++)
            {
                var name = ws.Cell(r, companyCol).GetString();
                var eik = ws.Cell(r, eikCol).GetString();
                if (!string.Equals(eik, "!!!!", StringComparison.Ordinal)) continue;

                // build full row text
                var parts = new List<string>();
                for (int c = 1; c <= lastCol; c++)
                {
                    parts.Add(ws.Cell(r, c).GetString());
                }
                var full = string.Join(" | ", parts).Trim();
                var truncated = full.Length > 35 ? full.Substring(0, 35) : full;

                var norm = StringNormalizationService.NormalizeCompanyName(name) ?? string.Empty;
                rows.Add(new SearchRow
                {
                    RowNumber = r,
                    CompanyName = name,
                    Eik = eik,
                    Normalized = norm,
                    FullRowText = full,
                    TruncatedText = truncated,
                    InputEik = string.Empty
                });
            }

            var vm = new SearchViewModel { SourceFile = Path.GetFileName(path), Rows = rows };
            ViewData["File2Col"] = companyCol;
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveOperatorEdits(string sourceFile, int? file2Col, Dictionary<string, string>? edits)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(sourceFile)) return BadRequest();

            // locate source
            string? sourcePath = null;
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var downloads = Path.Combine(userProfile ?? Directory.GetCurrentDirectory(), "Downloads");
            var p = Path.Combine(downloads, sourceFile);
            if (System.IO.File.Exists(p)) sourcePath = p;
            else
            {
                var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                var pu = Path.Combine(uploads, sourceFile);
                if (System.IO.File.Exists(pu)) sourcePath = pu;
            }
            if (string.IsNullOrEmpty(sourcePath)) return NotFound();

            var wb = new ClosedXML.Excel.XLWorkbook(sourcePath);
            var ws = wb.Worksheets.First();
            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            int companyCol = (file2Col ?? 1);
            int eikCol = companyCol + 1;

            var eikRegex = new Regex("^(\\d{9}|\\d{12})$");
            int applied = 0;
            if (edits != null)
            {
                foreach (var kv in edits)
                {
                    if (!int.TryParse(kv.Key, out var row))
                    {
                        errors.Add($"Invalid row key: {kv.Key}");
                        continue;
                    }
                    var newEik = (kv.Value ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(newEik)) continue; // skip empty
                    if (!eikRegex.IsMatch(newEik))
                    {
                        errors.Add($"Row {row}: invalid EIK '{newEik}'");
                        continue;
                    }

                    // ensure the cell was previously !!!!
                    var current = ws.Cell(row, eikCol).GetString();
                    if (!string.Equals(current, "!!!!", StringComparison.Ordinal))
                    {
                        errors.Add($"Row {row}: cell not marked with !!!!, skipped.");
                        continue;
                    }

                    ws.Cell(row, eikCol).Value = newEik;
                    applied++;
                }
            }

            // save new file to Downloads
            var outName = Path.GetFileNameWithoutExtension(sourceFile) + "-operator-result" + Path.GetExtension(sourceFile);
            var outPath = Path.Combine(downloads, outName);
            wb.SaveAs(outPath);

            // record statistics for operator edits (best-effort)
            try
            {
                var proc = new AddEiksInXlsxFile.Services.ProcessResult
                {
                    OutputFileName = outName,
                    OutputFilePath = outPath,
                    TotalRows = lastRow - 1,
                    MatchedCount = applied,
                    ErrorMessage = errors.Count > 0 ? string.Join("; ", errors) : null
                };
                var userId = User?.Identity?.Name;
                await _statisticsService.RecordAsync(proc, userId, null, sourceFile);
            }
            catch
            {
                // swallow logging errors
            }

            return Json(new { success = true, filename = outName, path = outPath, errors });
        }
    }
}
