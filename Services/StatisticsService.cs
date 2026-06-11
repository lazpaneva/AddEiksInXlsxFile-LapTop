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
                if (!StatisticsCalculationService.IsOperatorResult(result.OutputFileName) &&
                    !StatisticsCalculationService.IsOperatorResult(result.OutputFilePath))
                {
                    return;
                }

                var outputFileName = StatisticsCalculationService.GetOperatorResultFileName(result.OutputFileName)
                    ?? StatisticsCalculationService.GetOperatorResultFileName(result.OutputFilePath);
                if (string.IsNullOrEmpty(outputFileName))
                {
                    return;
                }

                var stat = await _db.ProcessingStatistics
                    .Where(s => s.UserId == userId)
                    .Where(s =>
                        s.InputFile2 == outputFileName ||
                        (s.OutputFilePath != null && s.OutputFilePath.EndsWith(outputFileName)))
                    .OrderByDescending(s => s.TimestampUtc)
                    .ThenByDescending(s => s.Id)
                    .FirstOrDefaultAsync();

                if (stat == null)
                {
                    stat = new ProcessingStatistics
                    {
                        UserId = userId,
                        InputFile1 = inputFile1,
                        InputFile2 = outputFileName
                    };
                    _db.ProcessingStatistics.Add(stat);
                }

                stat.TimestampUtc = System.DateTime.UtcNow;
                stat.InputFile1 = inputFile1 ?? stat.InputFile1;
                stat.InputFile2 = outputFileName;
                stat.PageNumber = pageNumber;
                stat.OutputFilePath = result.OutputFilePath;
                stat.TotalRows = result.TotalRows;
                stat.UniqueEiksCount = result.UniqueEiksCount;
                stat.MatchedCount = result.MatchedCount;
                stat.SuccessRate = StatisticsCalculationService.CalculateSuccessRate(result.TotalRows, result.MatchedCount);
                stat.ErrorMessage = result.ErrorMessage;

                await _db.SaveChangesAsync();
            }
            catch
            {
                // swallow exceptions: statistics are best-effort
            }
        }
    }
}
