using AddEiksInXlsxFile.Models;

namespace AddEiksInXlsxFile.Services
{
    public class OperatorStatSnapshot
    {
        public int Id { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string FileNameOrPath { get; set; } = string.Empty;
        public int MatchedCount { get; set; }
        public int UniqueEiksCount { get; set; }
        public int TotalRows { get; set; }
    }

    public static class StatisticsCalculationService
    {
        public static bool IsOperatorResult(string? fileNameOrPath)
        {
            if (string.IsNullOrWhiteSpace(fileNameOrPath))
            {
                return false;
            }

            var fileName = Path.GetFileName(fileNameOrPath);
            return fileName.EndsWith("-operator-result.xlsx", StringComparison.OrdinalIgnoreCase);
        }

        public static string? GetOperatorResultFileName(string? fileNameOrPath)
        {
            if (!IsOperatorResult(fileNameOrPath))
            {
                return null;
            }

            return Path.GetFileName(fileNameOrPath);
        }

        public static decimal CalculateSuccessRate(int totalRows, int matchedCount)
        {
            return totalRows == 0 ? 0 : (decimal)matchedCount / totalRows;
        }

        public static AggregatedOperatorStatistics AggregateLatestByFile(IEnumerable<OperatorStatSnapshot> periodStats)
        {
            var latestStatsByFile = periodStats
                .GroupBy(s => Path.GetFileName(s.FileNameOrPath), StringComparer.OrdinalIgnoreCase)
                .Select(g => g
                    .OrderByDescending(s => s.TimestampUtc)
                    .ThenByDescending(s => s.Id)
                    .First())
                .ToList();

            return new AggregatedOperatorStatistics
            {
                ProcessedExclamations = latestStatsByFile.Sum(s => s.MatchedCount),
                UniqueEiksFromProcessedExclamations = latestStatsByFile.Sum(s => s.UniqueEiksCount),
                TotalExclamationsAtPeriodStart = latestStatsByFile.Sum(s => s.TotalRows),
                Files = latestStatsByFile
                    .OrderBy(f => Path.GetFileName(f.FileNameOrPath))
                    .Select(f => new FileStatisticsViewModel
                    {
                        FileName = Path.GetFileName(f.FileNameOrPath),
                        ProcessedExclamations = f.MatchedCount,
                        UniqueEiksFromProcessedExclamations = f.UniqueEiksCount,
                        TotalExclamationsAtPeriodStart = f.TotalRows
                    })
                    .ToList()
            };
        }
    }

    public class AggregatedOperatorStatistics
    {
        public int ProcessedExclamations { get; set; }
        public int UniqueEiksFromProcessedExclamations { get; set; }
        public int TotalExclamationsAtPeriodStart { get; set; }
        public List<FileStatisticsViewModel> Files { get; set; } = new();
    }
}
