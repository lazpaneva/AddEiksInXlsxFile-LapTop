using System;

namespace AddEiksInXlsxFile.Models
{
    public class StatisticsViewModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int TotalRowsChecked { get; set; }
        public int UniqueEiks { get; set; }
    }
}
