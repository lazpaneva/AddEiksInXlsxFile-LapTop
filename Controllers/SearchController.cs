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
        public IActionResult Index(string? sourceFile = null, int? file2Col = null, int page = 1)
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
                    var files = Directory.GetFiles(downloads, "*-result.xlsx")
                        .Where(f => !Path.GetFileName(f).EndsWith("-operator-result.xlsx", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(System.IO.File.GetLastWriteTime)
                        .ToArray();
                    if (files.Length > 0) path = files[0];
                }
                if (string.IsNullOrEmpty(path))
                {
                    var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    if (Directory.Exists(uploads))
                    {
                        var files = Directory.GetFiles(uploads, "*-result.xlsx")
                            .Where(f => !Path.GetFileName(f).EndsWith("-operator-result.xlsx", StringComparison.OrdinalIgnoreCase))
                            .OrderByDescending(System.IO.File.GetLastWriteTime)
                            .ToArray();
                        if (files.Length > 0) path = files[0];
                    }
                }
            }

            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                var emptyCompanyCol = file2Col ?? 1;
                ViewData["File2Col"] = emptyCompanyCol;
                return View(new SearchViewModel { File2Col = emptyCompanyCol });
            }

            int companyCol = file2Col ?? 1;
            ViewData["File2Col"] = companyCol;

            try
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var downloads = Path.Combine(userProfile ?? Directory.GetCurrentDirectory(), "Downloads");
                var readPath = GetOperatorResultPath(path, downloads);
                if (!System.IO.File.Exists(readPath)) readPath = path;

                using var wb = new ClosedXML.Excel.XLWorkbook(readPath);
                var ws = wb.Worksheets.First();

                int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
                int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 2;
                var rows = new List<SearchRow>();
                int eikCol = companyCol + 1;

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

                const int pageSize = 50;
                var totalRows = rows.Count;
                var totalPages = Math.Max(1, (int)Math.Ceiling(totalRows / (double)pageSize));
                var currentPage = Math.Clamp(page, 1, totalPages);
                var pageRows = rows
                    .Skip((currentPage - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return View(new SearchViewModel
                {
                    SourceFile = Path.GetFileName(path),
                    Rows = pageRows,
                    CurrentPage = currentPage,
                    PageSize = pageSize,
                    TotalRows = totalRows,
                    TotalPages = totalPages,
                    File2Col = companyCol
                });
            }
            catch (Exception ex)
            {
                ViewData["Error"] = $"Неуспешно четене на файла „{Path.GetFileName(path)}“: {ex.Message}";
                return View(new SearchViewModel());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveOperatorEdits([FromBody] SaveOperatorEditsRequest request)
        {
            var errors = new List<string>();
            var sourceFile = request.SourceFile;
            var file2Col = request.File2Col;
            var edits = request.Edits;

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

            var outName = Path.GetFileNameWithoutExtension(sourceFile) + "-operator-result" + Path.GetExtension(sourceFile);
            var outPath = Path.Combine(downloads, outName);
            var editPath = System.IO.File.Exists(outPath) ? outPath : sourcePath;

            var wb = new ClosedXML.Excel.XLWorkbook(editPath);
            var ws = wb.Worksheets.First();
            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            int companyCol = (file2Col ?? 1);
            int eikCol = companyCol + 1;

            var eikRegex = new Regex("^(\\d{9}|\\d{10}|\\d{12})$");
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

                    // ensure the cell was previously !!!!
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
                        ws.Cell(row, eikCol).Clear(ClosedXML.Excel.XLClearOptions.Contents);
                        continue;
                    }

                    if (!eikRegex.IsMatch(newEik))
                    {
                        errors.Add($"Row {row}: invalid EIK '{newEik}'");
                        continue;
                    }

                    ws.Cell(row, eikCol).Value = newEik;
                    applied++;
                }
            }

            // save new file to Downloads; overwrite the same operator result on every save
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

        private static string GetOperatorResultPath(string sourcePath, string? preferredDirectory = null)
        {
            var fileName = Path.GetFileName(sourcePath);
            var outName = Path.GetFileNameWithoutExtension(fileName) + "-operator-result" + Path.GetExtension(fileName);
            if (!string.IsNullOrEmpty(preferredDirectory))
            {
                var preferredPath = Path.Combine(preferredDirectory, outName);
                if (System.IO.File.Exists(preferredPath)) return preferredPath;
            }

            var directory = Path.GetDirectoryName(sourcePath) ?? Directory.GetCurrentDirectory();
            return Path.Combine(directory, outName);
        }
    }

    public class SaveOperatorEditsRequest
    {
        public string SourceFile { get; set; } = string.Empty;
        public int? File2Col { get; set; }
        public Dictionary<string, string>? Edits { get; set; }
    }
}
