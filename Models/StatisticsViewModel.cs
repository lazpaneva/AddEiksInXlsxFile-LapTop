using System;
using System.Collections.Generic;

namespace AddEiksInXlsxFile.Models
{
    public class StatisticsViewModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string? SelectedUserId { get; set; }
        public List<string> Users { get; set; } = new();
        public int ProcessedExclamations { get; set; }
        public int UniqueEiksFromProcessedExclamations { get; set; }
        public int TotalExclamationsAtPeriodStart { get; set; }
        public List<FileStatisticsViewModel> Files { get; set; } = new();
    }

    public class FileStatisticsViewModel
    {
        public string FileName { get; set; } = string.Empty;
        public int ProcessedExclamations { get; set; }
        public int UniqueEiksFromProcessedExclamations { get; set; }
        public int TotalExclamationsAtPeriodStart { get; set; }
    }
}
