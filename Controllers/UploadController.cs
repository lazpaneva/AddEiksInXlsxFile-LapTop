using Microsoft.AspNetCore.Mvc;
using AddEiksInXlsxFile.Services;
using AddEiksInXlsxFile.Models;

namespace AddEiksInXlsxFile.Controllers
{
    using Microsoft.AspNetCore.Authorization;

    /// <summary>
    /// Контролер за качване и управление на XLSX файлове. Отговаря за приемане
    /// на входни файлове, валидация и задействане на обработка/експорт.
    /// </summary>
    [Authorize]
    public class UploadController : Controller
    {
        private readonly XlsxService _xlsxService;
        private readonly XlsxProcessingService _processingService;
        private readonly StatisticsService _statisticsService;
        private readonly SearchService _searchService;
        private readonly string[] _accepted = new[] { ".xlsx" };

        /// <summary>
        /// Инициализира нов екземпляр на <see cref="UploadController"/> с необходимите услуги.
        /// </summary>
        public UploadController(XlsxService xlsxService, XlsxProcessingService processingService, StatisticsService statisticsService, SearchService searchService)
        {
            _xlsxService = xlsxService;
            _processingService = processingService;
            _statisticsService = statisticsService;
            _searchService = searchService;
        }

        [HttpGet]
        /// <summary>
        /// Първичен изглед за качване на файлове.
        /// </summary>
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        /// <summary>
        /// Пренасочва към контролера за търсене (Search).
        /// </summary>
        public IActionResult Search()
        {
            return RedirectToAction("Index", "Search");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(50 * 1024 * 1024)]
        /// <summary>
        /// Обработва POST заявката за качване/стартиране на обработка.
        /// Приема две опционални файлови полета и допълнителни параметри за колони.
        /// </summary>
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
                // (non-obvious: return JSON for client-side upload flow)
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
                    // Complex operation: run processing and record statistics. Wrap in try/catch to preserve UX.
                    try
                    {
                        var procResult = _processingService.ProcessAndSort(result.File1Name, result.File2Name, file1ColValue, file2ColValue);
                        result.File2Name = procResult.OutputFileName;
                        result.Message = $"Processing complete. Download: {procResult.OutputFileName}";

                        // Record statistics (best-effort). Use User.Identity.Name as user id if available.
                        var userId = User?.Identity?.Name;
                        await _statisticsService.RecordAsync(procResult, userId, result.File1Name, result.File2Name);

                        // After successful processing, redirect to Search page and pass sourceFile and file2Col so SearchController can use them
                        return RedirectToAction("Index", "Search", new { sourceFile = procResult.OutputFileName, file2Col = file2ColValue });
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

        

        private bool IsAllowed(string fileName)
        {
            var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            return _accepted.Contains(ext);
        }
    }
}
