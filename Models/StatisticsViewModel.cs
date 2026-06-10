using System;

namespace AddEiksInXlsxFile.Models
{
    public class StatisticsViewModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int ProcessedExclamations { get; set; }
        public int UniqueEiksFromProcessedExclamations { get; set; }
        public int TotalExclamationsAtPeriodStart { get; set; }
    }
}
