using AddEiksInXlsxFile.Services;
using ClosedXML.Excel;
using Xunit;

namespace AddEiksInXlsxFile.Tests.Services;

public class SearchOperatorServiceTests
{
    [Theory]
    [InlineData("123456789", true)]
    [InlineData("1234567890", true)]
    [InlineData("1234567890123", true)]
    [InlineData("12345", false)]
    [InlineData("12345678901", false)]
    [InlineData("abc123456789", false)]
    [InlineData("", false)]
    public void IsValidOperatorEik_validates_digit_lengths(string eik, bool expected)
    {
        Assert.Equal(expected, SearchOperatorService.IsValidOperatorEik(eik));
    }

    [Fact]
    public void TruncateText_short_text_unchanged()
    {
        Assert.Equal("Short row", SearchOperatorService.TruncateText("Short row"));
    }

    [Fact]
    public void TruncateText_long_text_cut_at_35()
    {
        var longText = new string('A', 40);
        var result = SearchOperatorService.TruncateText(longText);

        Assert.Equal(35, result.Length);
        Assert.Equal(longText.Substring(0, 35), result);
    }

    [Fact]
    public void GetOperatorResultPath_prefers_existing_operator_file_in_directory()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var source = Path.Combine(tempDir, "report-result.xlsx");
            File.WriteAllText(source, "source");
            var operatorFile = Path.Combine(tempDir, "report-result-operator-result.xlsx");
            File.WriteAllText(operatorFile, "operator");

            var result = SearchOperatorService.GetOperatorResultPath(source, tempDir);

            Assert.Equal(operatorFile, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ReadMissingEikRows_returns_only_rows_marked_with_bangs()
    {
        using var wb = CreateSearchWorkbook(
            ("Company A", "!!!!"),
            ("Company B", "123456789"),
            ("Company C", "!!!!"));

        var rows = SearchOperatorService.ReadMissingEikRows(wb.Worksheets.First(), companyCol: 1);

        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows[0].RowNumber);
        Assert.Equal("Company A", rows[0].CompanyName);
        Assert.Equal("!!!!", rows[0].Eik);
        Assert.Equal(4, rows[1].RowNumber);
        Assert.Equal("Company C", rows[1].CompanyName);
        Assert.Equal("Company A", rows[0].Normalized);
    }

    [Fact]
    public void ReadMissingEikRows_builds_full_and_truncated_row_text()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sheet1");
        ws.Cell(1, 1).Value = "Company";
        ws.Cell(1, 2).Value = "EIK";
        ws.Cell(1, 3).Value = "Note";
        ws.Cell(2, 1).Value = "ACME";
        ws.Cell(2, 2).Value = "!!!!";
        ws.Cell(2, 3).Value = new string('X', 50);

        var rows = SearchOperatorService.ReadMissingEikRows(ws, companyCol: 1);

        Assert.Single(rows);
        Assert.Contains("ACME | !!!! | ", rows[0].FullRowText);
        Assert.Equal(35, rows[0].TruncatedText.Length);
    }

    [Fact]
    public void ApplyOperatorEdits_applies_valid_eik_to_bang_cell()
    {
        using var wb = CreateSearchWorkbook(("ACME", "!!!!"));
        var ws = wb.Worksheets.First();
        var edits = new Dictionary<string, string> { ["2"] = "123456789" };

        var (applied, changedRows, errors) = SearchOperatorService.ApplyOperatorEdits(ws, 1, 2, edits);

        Assert.Equal(1, applied);
        Assert.Equal(1, changedRows);
        Assert.Empty(errors);
        Assert.Equal("123456789", ws.Cell(2, 2).GetString());
    }

    [Fact]
    public void ApplyOperatorEdits_rejects_invalid_eik()
    {
        using var wb = CreateSearchWorkbook(("ACME", "!!!!"));
        var ws = wb.Worksheets.First();
        var edits = new Dictionary<string, string> { ["2"] = "12345" };

        var (applied, changedRows, errors) = SearchOperatorService.ApplyOperatorEdits(ws, 1, 2, edits);

        Assert.Equal(0, applied);
        Assert.Equal(0, changedRows);
        Assert.Single(errors);
        Assert.Equal("!!!!", ws.Cell(2, 2).GetString());
    }

    [Fact]
    public void ApplyOperatorEdits_skips_row_not_marked_with_bangs()
    {
        using var wb = CreateSearchWorkbook(("ACME", "123456789"));
        var ws = wb.Worksheets.First();
        var edits = new Dictionary<string, string> { ["2"] = "987654321" };

        var (applied, changedRows, errors) = SearchOperatorService.ApplyOperatorEdits(ws, 1, 2, edits);

        Assert.Equal(0, applied);
        Assert.Equal(0, changedRows);
        Assert.Single(errors);
        Assert.Contains("not marked with !!!!", errors[0]);
    }

    [Fact]
    public void ApplyOperatorEdits_clears_cell_when_eik_empty()
    {
        using var wb = CreateSearchWorkbook(("ACME", "!!!!"));
        var ws = wb.Worksheets.First();
        var edits = new Dictionary<string, string> { ["2"] = "" };

        var (applied, changedRows, errors) = SearchOperatorService.ApplyOperatorEdits(ws, 1, 2, edits);

        Assert.Equal(0, applied);
        Assert.Equal(1, changedRows);
        Assert.Empty(errors);
        Assert.Equal(string.Empty, ws.Cell(2, 2).GetString());
    }

    [Fact]
    public void CountOperatorProgress_counts_processed_and_unique_eiks()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var sourcePath = Path.Combine(tempDir, "source.xlsx");
            using (var sourceWb = CreateSearchWorkbook(
                ("A", "!!!!"),
                ("B", "!!!!"),
                ("C", "123456789")))
            {
                sourceWb.SaveAs(sourcePath);
            }

            using var editedWb = CreateSearchWorkbook(
                ("A", "111111111"),
                ("B", "!!!!"),
                ("C", "123456789"));
            var editedWs = editedWb.Worksheets.First();
            var lastRow = editedWs.LastRowUsed()!.RowNumber();

            var progress = SearchOperatorService.CountOperatorProgress(sourcePath, editedWs, eikCol: 2, lastRow);

            Assert.Equal(2, progress.OriginalMissingRows);
            Assert.Equal(1, progress.ProcessedRows);
            Assert.Equal(1, progress.UniqueEiksCount);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void NormalizeCompanyNames_writes_normalized_values()
    {
        using var wb = CreateSearchWorkbook(("  \"ACME\"  ", "!!!!"));
        var ws = wb.Worksheets.First();
        var lastRow = ws.LastRowUsed()!.RowNumber();

        SearchOperatorService.NormalizeCompanyNames(ws, companyCol: 1, lastRow);

        Assert.Equal("ACME", ws.Cell(2, 1).GetString());
    }

    private static XLWorkbook CreateSearchWorkbook(params (string Company, string Eik)[] rows)
    {
        var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sheet1");
        ws.Cell(1, 1).Value = "Company";
        ws.Cell(1, 2).Value = "EIK";

        for (int i = 0; i < rows.Length; i++)
        {
            ws.Cell(i + 2, 1).Value = rows[i].Company;
            ws.Cell(i + 2, 2).Value = rows[i].Eik;
        }

        return wb;
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "AddEiksSearchTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
