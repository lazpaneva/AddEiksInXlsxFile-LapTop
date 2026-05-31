using Microsoft.AspNetCore.Mvc;
using AddEiksInXlsxFile.Services;
using AddEiksInXlsxFile.Models;

namespace AddEiksInXlsxFile.Controllers
{
    public class UploadController : Controller
    {
        private readonly XlsxService _xlsxService;
        private readonly XlsxProcessingService _processingService;
        private readonly string[] _accepted = new[] { ".xlsx" };

        public UploadController(XlsxService xlsxService, XlsxProcessingService processingService)
        {
            _xlsxService = xlsxService;
            _processingService = processingService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [RequestSizeLimit(50 * 1024 * 1024)]
        public async Task<IActionResult> Index(IFormFile? file1, IFormFile? file2, int? file1Col = null, int? file2Col = null, string? submitButton = null)
        {
            if ((file1 == null || file1.Length == 0) && (file2 == null || file2.Length == 0))
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

            if (!ModelState.IsValid)
            {
                return View();
            }

            // If user clicked Start, run processing
            if (!string.IsNullOrEmpty(submitButton) && submitButton.Equals("Start", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(result.File1Name) && !string.IsNullOrEmpty(result.File2Name))
                {
                    try
                    {
                        var output = _processingService.ProcessAndSort(result.File1Name, result.File2Name, file1ColValue, file2ColValue);
                        result.File2Name = output;
                        result.Message = $"Processing complete. Download: {output}";
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

            return View(result);
        }

        [HttpGet]
        public IActionResult Download(string file)
        {
            if (string.IsNullOrEmpty(file)) return BadRequest();
            var path = _xlsxService.GetFilePath(file);
            if (!System.IO.File.Exists(path)) return NotFound();
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
