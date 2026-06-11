using AddEiksInXlsxFile.Services;
using Xunit;

namespace AddEiksInXlsxFile.Tests.Services;

public class StatisticsCalculationServiceTests
{
    [Theory]
    [InlineData("report-operator-result.xlsx", true)]
    [InlineData(@"C:\Downloads\report-operator-result.xlsx", true)]
    [InlineData("REPORT-OPERATOR-RESULT.XLSX", true)]
    [InlineData("report-result.xlsx", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsOperatorResult_detects_operator_result_files(string? fileNameOrPath, bool expected)
    {
        Assert.Equal(expected, StatisticsCalculationService.IsOperatorResult(fileNameOrPath));
    }

    [Fact]
    public void GetOperatorResultFileName_returns_file_name_for_valid_path()
    {
        var result = StatisticsCalculationService.GetOperatorResultFileName(
            @"C:\Users\test\Downloads\data-operator-result.xlsx");

        Assert.Equal("data-operator-result.xlsx", result);
    }

    [Fact]
    public void GetOperatorResultFileName_returns_null_for_non_operator_file()
    {
        Assert.Null(StatisticsCalculationService.GetOperatorResultFileName("data-result.xlsx"));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(10, 0, 0)]
    [InlineData(10, 5, 0.5)]
    [InlineData(4, 1, 0.25)]
    public void CalculateSuccessRate_divides_matched_by_total(int totalRows, int matchedCount, decimal expected)
    {
        Assert.Equal(expected, StatisticsCalculationService.CalculateSuccessRate(totalRows, matchedCount));
    }

    [Fact]
    public void AggregateLatestByFile_uses_latest_snapshot_per_file()
    {
        var stats = new[]
        {
            new OperatorStatSnapshot
            {
                Id = 1,
                TimestampUtc = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc),
                FileNameOrPath = @"C:\Downloads\a-operator-result.xlsx",
                MatchedCount = 1,
                UniqueEiksCount = 1,
                TotalRows = 5
            },
            new OperatorStatSnapshot
            {
                Id = 2,
                TimestampUtc = new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc),
                FileNameOrPath = "a-operator-result.xlsx",
                MatchedCount = 3,
                UniqueEiksCount = 2,
                TotalRows = 5
            },
            new OperatorStatSnapshot
            {
                Id = 3,
                TimestampUtc = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc),
                FileNameOrPath = "b-operator-result.xlsx",
                MatchedCount = 2,
                UniqueEiksCount = 2,
                TotalRows = 4
            }
        };

        var aggregated = StatisticsCalculationService.AggregateLatestByFile(stats);

        Assert.Equal(5, aggregated.ProcessedExclamations);
        Assert.Equal(4, aggregated.UniqueEiksFromProcessedExclamations);
        Assert.Equal(9, aggregated.TotalExclamationsAtPeriodStart);
        Assert.Equal(2, aggregated.Files.Count);
        Assert.Equal("a-operator-result.xlsx", aggregated.Files[0].FileName);
        Assert.Equal(3, aggregated.Files[0].ProcessedExclamations);
        Assert.Equal(2, aggregated.Files[0].UniqueEiksFromProcessedExclamations);
        Assert.Equal(5, aggregated.Files[0].TotalExclamationsAtPeriodStart);
        Assert.Equal("b-operator-result.xlsx", aggregated.Files[1].FileName);
    }

    [Fact]
    public void AggregateLatestByFile_breaks_timestamp_ties_by_id()
    {
        var timestamp = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var stats = new[]
        {
            new OperatorStatSnapshot
            {
                Id = 1,
                TimestampUtc = timestamp,
                FileNameOrPath = "file-operator-result.xlsx",
                MatchedCount = 1,
                UniqueEiksCount = 1,
                TotalRows = 10
            },
            new OperatorStatSnapshot
            {
                Id = 5,
                TimestampUtc = timestamp,
                FileNameOrPath = "file-operator-result.xlsx",
                MatchedCount = 4,
                UniqueEiksCount = 3,
                TotalRows = 10
            }
        };

        var aggregated = StatisticsCalculationService.AggregateLatestByFile(stats);

        Assert.Single(aggregated.Files);
        Assert.Equal(4, aggregated.ProcessedExclamations);
        Assert.Equal(3, aggregated.Files[0].UniqueEiksFromProcessedExclamations);
    }

    [Fact]
    public void AggregateLatestByFile_returns_empty_summary_for_no_data()
    {
        var aggregated = StatisticsCalculationService.AggregateLatestByFile(Array.Empty<OperatorStatSnapshot>());

        Assert.Equal(0, aggregated.ProcessedExclamations);
        Assert.Equal(0, aggregated.UniqueEiksFromProcessedExclamations);
        Assert.Equal(0, aggregated.TotalExclamationsAtPeriodStart);
        Assert.Empty(aggregated.Files);
    }
}
