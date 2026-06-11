using Microsoft.AspNetCore.Mvc;
using AddEiksInXlsxFile.Models;
using AddEiksInXlsxFile.Services;

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
                var readPath = SearchOperatorService.GetOperatorResultPath(path, downloads);
                if (!System.IO.File.Exists(readPath)) readPath = path;

                using var wb = new ClosedXML.Excel.XLWorkbook(readPath);
                var ws = wb.Worksheets.First();
                var rows = SearchOperatorService.ReadMissingEikRows(ws, companyCol);

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

            var (applied, changedRows, applyErrors) = SearchOperatorService.ApplyOperatorEdits(ws, companyCol, eikCol, edits);
            errors.AddRange(applyErrors);

            SearchOperatorService.NormalizeCompanyNames(ws, companyCol, lastRow);
            var progress = SearchOperatorService.CountOperatorProgress(sourcePath, ws, eikCol, lastRow);

            // save new file to Downloads; overwrite the same operator result on every save
            wb.SaveAs(outPath);

            // record statistics for operator edits (best-effort)
            try
            {
                var proc = new AddEiksInXlsxFile.Services.ProcessResult
                {
                    OutputFileName = outName,
                    OutputFilePath = outPath,
                    TotalRows = progress.OriginalMissingRows,
                    MatchedCount = progress.ProcessedRows,
                    UniqueEiksCount = progress.UniqueEiksCount,
                    ErrorMessage = errors.Count > 0 ? string.Join("; ", errors) : null
                };
                var userId = User?.Identity?.Name;

                await _statisticsService.RecordAsync(proc, userId, null, outName);
            }
            catch
            {
                // swallow logging errors
            }

            return Json(new
            {
                success = true,
                filename = outName,
                path = outPath,
                errors,
                changedRows,
                totalMissingRows = progress.OriginalMissingRows,
                processedRows = progress.ProcessedRows,
                uniqueEiksCount = progress.UniqueEiksCount
            });
        }

    }

    public class SaveOperatorEditsRequest
    {
        public string SourceFile { get; set; } = string.Empty;
        public int? File2Col { get; set; }
        public Dictionary<string, string>? Edits { get; set; }
    }
}
