using Microsoft.AspNetCore.Mvc;
using AddEiksInXlsxFile.Services;
using AddEiksInXlsxFile.Models;

namespace AddEiksInXlsxFile.Controllers
{
    using Microsoft.AspNetCore.Authorization;

    [Authorize]
    public class UploadController : Controller
    {
        private readonly XlsxService _xlsxService;
        private readonly XlsxProcessingService _processingService;
        private readonly StatisticsService _statisticsService;
        private readonly SearchService _searchService;
        private readonly string[] _accepted = new[] { ".xlsx" };

        public UploadController(XlsxService xlsxService, XlsxProcessingService processingService, StatisticsService statisticsService, SearchService searchService)
        {
            _xlsxService = xlsxService;
            _processingService = processingService;
            _statisticsService = statisticsService;
            _searchService = searchService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(50 * 1024 * 1024)]
        public async Task<IActionResult> Index(IFormFile? file1, IFormFile? file2, int? file1Col = null, int? file2Col = null, string? submitButton = null, string? existingFile1 = null, string? existingFile2 = null)
        {
            // Allow processing to start when files were previously uploaded and their names are posted back
            if ((file1 == null || file1.Length == 0) && (file2 == null || file2.Length == 0)
                && string.IsNullOrEmpty(existingFile1) && string.IsNullOrEmpty(existingFile2))
            {
                ModelState.AddModelError(string.Empty, "Please select at least one XLSX file to upload.");
                return View();
            }

            var result = new UploadResult();
            var file1ColValue = file1Col ?? 1;
            var file2ColValue = file2Col ?? 1;
            result.File1CompanyColumn = file1ColValue;
            result.File2CompanyColumn = file2ColValue;

            if (file1 != null && file1.Length > 0)
            {
                if (!IsAllowed(file1.FileName))
                {
                    ModelState.AddModelError("file1", "Only .xlsx files are allowed for File 1.");
                }
                else
                {
                    result.File1Name = await _xlsxService.SaveTempFileAsync(file1);
                }
            }
            else if (!string.IsNullOrEmpty(existingFile1))
            {
                // Use previously uploaded file name if present
                result.File1Name = existingFile1;
            }
            
            if (file2 != null && file2.Length > 0)
            {
                if (!IsAllowed(file2.FileName))
                {
                    ModelState.AddModelError("file2", "Only .xlsx files are allowed for File 2.");
                }
                else
                {
                    result.File2Name = await _xlsxService.SaveTempFileAsync(file2);
                }
            }
            else if (!string.IsNullOrEmpty(existingFile2))
            {
                // Use previously uploaded file name if present
                result.File2Name = existingFile2;
            }

            if (!ModelState.IsValid)
            {
                // If this is an AJAX upload, return validation errors as JSON
                if (Request.Headers.ContainsKey("X-Requested-With") && Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    var errors = ViewData.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToArray();
                    return Json(new { success = false, errors });
                }
                return View();
            }

            // If user clicked Start, run processing
            if (!string.IsNullOrEmpty(submitButton) && submitButton.Equals("Start", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(result.File1Name) && !string.IsNullOrEmpty(result.File2Name))
                {
                    try
                    {
                        var procResult = _processingService.ProcessAndSort(result.File1Name, result.File2Name, file1ColValue, file2ColValue);
                        result.File2Name = procResult.OutputFileName;
                        result.Message = $"Processing complete. Download: {procResult.OutputFileName}";

                        // Record statistics (best-effort). Use User.Identity.Name as user id if available.
                        var userId = User?.Identity?.Name;
                        await _statisticsService.RecordAsync(procResult, userId, result.File1Name, result.File2Name);
                    }
                    catch (Exception ex)
                    {
                        result.Message = $"Processing failed: {ex.Message}";
                    }
                }
                else
                {
                    result.Message = "Both files must be uploaded before starting processing.";
                }
            }

            // If user clicked Upload (AJAX), return filenames as JSON so client can update the form
            if (!string.IsNullOrEmpty(submitButton) && submitButton.Equals("Upload", StringComparison.OrdinalIgnoreCase))
            {
                if (Request.Headers.ContainsKey("X-Requested-With") && Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, file1 = result.File1Name, file2 = result.File2Name });
                }
            }

            return View(result);
        }

        [HttpGet]
        public IActionResult Download(string file)
        {
            if (string.IsNullOrEmpty(file)) return BadRequest();
                var path = _xlsxService.GetFilePath(file);
                if (!System.IO.File.Exists(path))
                {
                    // Try to locate the file elsewhere in the project (e.g., saved in the same folder as file2)
                    var matches = Directory.GetFiles(Directory.GetCurrentDirectory(), file, SearchOption.AllDirectories);
                    path = matches.FirstOrDefault();
                }
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return NotFound();
            var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            var fs = System.IO.File.OpenRead(path);
            return File(fs, contentType, file);
        }

        [HttpGet]
        public IActionResult Search()
        {
            // locate latest result file (by name pattern) in uploads
            var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);
            var files = Directory.GetFiles(uploads, "*-result.xlsx").OrderByDescending(System.IO.File.GetLastWriteTime).ToArray();
            if (files.Length == 0) return View(new Models.SearchViewModel());
            var path = files[0];

            var wb = new ClosedXML.Excel.XLWorkbook(path);
            var ws = wb.Worksheets.First();

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 2;
            var rows = new List<Models.SearchRow>();
            for (int r = 2; r <= lastRow; r++)
            {
                var name = ws.Cell(r, 1).GetString();
                var eik = ws.Cell(r, 2).GetString();
                var norm = StringNormalizationService.NormalizeCompanyName(name) ?? string.Empty;
                rows.Add(new Models.SearchRow { RowNumber = r, CompanyName = name, Eik = eik, Normalized = norm });
            }

            // sort by second column (EIK) ascending
            rows = rows.OrderBy(x => x.Eik, StringComparer.OrdinalIgnoreCase).ToList();

            var vm = new Models.SearchViewModel { SourceFile = Path.GetFileName(path), Rows = rows };
            return View(vm);
        }

        [HttpPost]
        public IActionResult SearchSave(string sourceFile, Dictionary<string, string>? edits)
        {
            if (edits != null)
            {
                foreach (var kv in edits)
                {
                    _searchService.SetEdit(kv.Key, kv.Value);
                }
            }

            // apply edits to source file and save to Downloads
            var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            var sourcePath = Path.Combine(uploads, sourceFile ?? string.Empty);
            if (!System.IO.File.Exists(sourcePath)) return NotFound();

            var wb = new ClosedXML.Excel.XLWorkbook(sourcePath);
            var ws = wb.Worksheets.First();
            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            for (int r = 2; r <= lastRow; r++)
            {
                var name = ws.Cell(r, 1).GetString();
                var norm = StringNormalizationService.NormalizeCompanyName(name) ?? string.Empty;
                if (_searchService.TryGetEdit(norm, out var newEik))
                {
                    ws.Cell(r, 2).Value = newEik;
                }
            }

            // save to Downloads
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var downloads = Path.Combine(userProfile ?? Directory.GetCurrentDirectory(), "Downloads");
            if (!Directory.Exists(downloads)) Directory.CreateDirectory(downloads);
            var outName = Path.GetFileNameWithoutExtension(sourceFile) + "-search-saved" + Path.GetExtension(sourceFile);
            var outPath = Path.Combine(downloads, outName);
            wb.SaveAs(outPath);

            return Json(new { success = true, path = outPath, filename = outName });
        }

        private bool IsAllowed(string fileName)
        {
            var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            return _accepted.Contains(ext);
        }
    }
}
