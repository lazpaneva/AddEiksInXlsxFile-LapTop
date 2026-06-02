using System.Threading.Tasks;
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

        public async Task RecordAsync(ProcessResult result, string? userId, string? inputFile1, string? inputFile2)
        {
            try
            {
                var stat = new ProcessingStatistics
                {
                    UserId = userId,
                    TimestampUtc = System.DateTime.UtcNow,
                    InputFile1 = inputFile1,
                    InputFile2 = inputFile2,
                    OutputFilePath = result.OutputFilePath,
                    TotalRows = result.TotalRows,
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
    }
}
