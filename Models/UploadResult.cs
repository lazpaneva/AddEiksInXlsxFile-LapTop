namespace AddEiksInXlsxFile.Models
{
    public class UploadResult
    {
        public string? File1Name { get; set; }
        public string? File2Name { get; set; }
        public int File1CompanyColumn { get; set; } = 1;
        public int File2CompanyColumn { get; set; } = 1;
        public string? Message { get; set; }
    }
}
