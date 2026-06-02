using System;

namespace AddEiksInXlsxFile.Models
{
    public class ProcessingStatistics
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string? InputFile1 { get; set; }
        public string? InputFile2 { get; set; }
        public string? OutputFilePath { get; set; }
        public int TotalRows { get; set; }
        public int MatchedCount { get; set; }
        public decimal SuccessRate { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
