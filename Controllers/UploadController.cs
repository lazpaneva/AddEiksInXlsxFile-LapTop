using Microsoft.AspNetCore.Mvc;
using AddEiksInXlsxFile.Services;
using AddEiksInXlsxFile.Models;

namespace AddEiksInXlsxFile.Controllers
{
    public class UploadController : Controller
    {
        private readonly XlsxService _xlsxService;
        private readonly string[] _accepted = new[] { ".xlsx" };

        public UploadController(XlsxService xlsxService)
        {
            _xlsxService = xlsxService;
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

            // If user clicked Start, show a processing message (actual matching not implemented here)
            if (!string.IsNullOrEmpty(submitButton) && submitButton.Equals("Start", StringComparison.OrdinalIgnoreCase))
            {
                result.Message = $"Processing started: File1 column={file1ColValue}, File2 column={file2ColValue}.";
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
