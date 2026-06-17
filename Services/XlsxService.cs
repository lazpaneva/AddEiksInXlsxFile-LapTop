using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AddEiksInXlsxFile.Services
{
    /// <summary>
    /// Помощна услуга за записване и локализиране на временни XLSX файлове
    /// в `wwwroot/uploads` и управление на безопасни имена на файлове.
    /// </summary>
    public class XlsxService
    {
        private readonly string _uploadsPath;

        /// <summary>
        /// Инициализира услугата и почиства временните .xlsx файлове при стартиране.
        /// </summary>
        public XlsxService()
        {
            _uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(_uploadsPath)) Directory.CreateDirectory(_uploadsPath);
            // Clean up existing .xlsx files on startup
            try
            {
                // Non-obvious: remove stale uploads to avoid accumulating files
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

        /// <summary>
        /// Записва входен `IFormFile` като временен файл и връща името за сваляне.
        /// </summary>
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

        /// <summary>
        /// Прави подаденото име безопасно за файловата система, замествайки
        /// невалидни символи и премахвайки празни места.
        /// </summary>
        private static string MakeSafeFileName(string name)
        {
            var invalids = Path.GetInvalidFileNameChars();
            var cleaned = string.Join("_", name.Split(invalids, System.StringSplitOptions.RemoveEmptyEntries)).Trim();
            // additionally trim spaces
            if (string.IsNullOrWhiteSpace(cleaned)) cleaned = "upload.xlsx";
            return cleaned;
        }

        /// <summary>
        /// Връща пълния път към временния файл за дадено име.
        /// </summary>
        public string GetFilePath(string fileName)
        {
            return Path.Combine(_uploadsPath, fileName);
        }
    }
}
