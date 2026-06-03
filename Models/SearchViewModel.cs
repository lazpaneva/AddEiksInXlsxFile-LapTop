using System.Collections.Generic;

namespace AddEiksInXlsxFile.Models
{
    public class SearchRow
    {
        public int RowNumber { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string Eik { get; set; } = string.Empty;
        public string Normalized { get; set; } = string.Empty;
    }

    public class SearchViewModel
    {
        public string SourceFile { get; set; } = string.Empty;
        public List<SearchRow> Rows { get; set; } = new();
    }
}
