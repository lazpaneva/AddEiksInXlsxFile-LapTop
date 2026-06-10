using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AddEiksInXlsxFile.Data;
using AddEiksInXlsxFile.Models;

namespace AddEiksInXlsxFile.Services
{
    public class StatisticsService
    {
        private readonly ApplicationDbContext _db;

        public StatisticsService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task RecordAsync(ProcessResult result, string? userId, string? inputFile1, string? inputFile2, int? pageNumber = null)
        {
            try
            {
                if (!IsOperatorResult(result.OutputFileName) && !IsOperatorResult(result.OutputFilePath))
                {
                    return;
                }

                // If this is an operator edit for a specific page, ensure we only count that page once per user+file
                if (pageNumber.HasValue && !string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(inputFile2))
                {
                    var already = await _db.ProcessingStatistics.AnyAsync(s => s.UserId == userId && s.InputFile2 == inputFile2 && s.PageNumber == pageNumber.Value);
                    if (already)
                    {
                        // already recorded for this page; do not double-count
                        return;
                    }
                }

                var stat = new ProcessingStatistics
                {
                    UserId = userId,
                    TimestampUtc = System.DateTime.UtcNow,
                    InputFile1 = inputFile1,
                    InputFile2 = inputFile2,
                    PageNumber = pageNumber,
                    OutputFilePath = result.OutputFilePath,
                    TotalRows = result.TotalRows,
                    UniqueEiksCount = result.UniqueEiksCount,
                    MatchedCount = result.MatchedCount,
                    SuccessRate = result.TotalRows == 0 ? 0 : (decimal)result.MatchedCount / result.TotalRows,
                    ErrorMessage = result.ErrorMessage
                };
                _db.ProcessingStatistics.Add(stat);
                await _db.SaveChangesAsync();
            }
            catch
            {
                // swallow exceptions: statistics are best-effort
            }
        }

        private static bool IsOperatorResult(string? fileNameOrPath)
        {
            if (string.IsNullOrWhiteSpace(fileNameOrPath))
            {
                return false;
            }

            var fileName = System.IO.Path.GetFileName(fileNameOrPath);
            return fileName.EndsWith("-operator-result.xlsx", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
