using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AddEiksInXlsxFile.Services
{
    public class XlsxService
    {
        private readonly string _uploadsPath;

        public XlsxService()
        {
            _uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(_uploadsPath)) Directory.CreateDirectory(_uploadsPath);
            // Clean up existing .xlsx files on startup
            try
            {
                var files = Directory.GetFiles(_uploadsPath, "*.xlsx");
                foreach (var f in files)
                {
                    try { System.IO.File.Delete(f); } catch { /* ignore individual file delete errors */ }
                }
            }
            catch
            {
                // ignore any errors when enumerating/deleting at startup
            }
        }

        public async Task<string> SaveTempFileAsync(IFormFile file)
        {
            var originalName = Path.GetFileName(file.FileName) ?? "upload.xlsx";
            var safeName = MakeSafeFileName(originalName);
            var filePath = Path.Combine(_uploadsPath, safeName);

            // If a file with same name exists, append a numeric suffix to avoid overwrite
            if (System.IO.File.Exists(filePath))
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(safeName);
                var ext = Path.GetExtension(safeName);
                int counter = 1;
                string newName;
                do
                {
                    newName = $"{nameWithoutExt} ({counter}){ext}";
                    filePath = Path.Combine(_uploadsPath, newName);
                    counter++;
                } while (System.IO.File.Exists(filePath));
            }

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);
            return Path.GetFileName(filePath) ?? safeName; // return stored file name for download link
        }

        private static string MakeSafeFileName(string name)
        {
            var invalids = Path.GetInvalidFileNameChars();
            var cleaned = string.Join("_", name.Split(invalids, System.StringSplitOptions.RemoveEmptyEntries)).Trim();
            // additionally trim spaces
            if (string.IsNullOrWhiteSpace(cleaned)) cleaned = "upload.xlsx";
            return cleaned;
        }

        public string GetFilePath(string fileName)
        {
            return Path.Combine(_uploadsPath, fileName);
        }
    }
}
