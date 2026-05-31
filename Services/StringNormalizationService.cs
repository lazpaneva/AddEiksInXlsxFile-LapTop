using System.Text.RegularExpressions;

namespace AddEiksInXlsxFile.Services
{
    public static class StringNormalizationService
    {
        public static string NormalizeCompanyName(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var s = input.Trim();

            // Remove common quote characters
            var quotes = new[] { '"', '\'', '\u2018', '\u2019', '\u201C', '\u201D', '\u00AB', '\u00BB', '\u201A', '\u201B' };
            foreach (var q in quotes) s = s.Replace(q.ToString(), string.Empty);

            // Replace a broad set of dash characters with ASCII hyphen
            var dashes = new[] { '\u2013', '\u2014', '\u2010', '\u2011', '\u2012', '\u2015', '\u2212', '\uFF0D', '\u2E3A', '\u2E3B', '\u2017' };
            foreach (var d in dashes) s = s.Replace(d.ToString(), "-");

            // Collapse multiple whitespace characters into a single space
            s = Regex.Replace(s, "\\s+", " ");

            return s.Trim();
        }
    }
}
