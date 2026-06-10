namespace AddEiksInXlsxFile.Services
{
    public class ProcessResult
    {
        public string OutputFileName { get; set; } = string.Empty;
        public string OutputFilePath { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public int MatchedCount { get; set; }
        public int UniqueEiksCount { get; set; }
        public string? ErrorMessage { get; set; }
    }
}