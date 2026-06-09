using System.Collections.Generic;

namespace AddEiksInXlsxFile.Models
{
    public class SearchRow
    {
        public int RowNumber { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string Eik { get; set; } = string.Empty;
        public string Normalized { get; set; } = string.Empty;
        public string FullRowText { get; set; } = string.Empty;
        public string TruncatedText { get; set; } = string.Empty;
        public string InputEik { get; set; } = string.Empty;
    }

    public class SearchViewModel
    {
        public string SourceFile { get; set; } = string.Empty;
        public List<SearchRow> Rows { get; set; } = new();
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalRows { get; set; }
        public int TotalPages { get; set; }
        public int File2Col { get; set; } = 1;
    }
}
