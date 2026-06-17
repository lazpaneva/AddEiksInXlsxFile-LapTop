using System.Text.RegularExpressions;

namespace AddEiksInXlsxFile.Services
{
    /// <summary>
    /// Съдържа помощни методи за нормализиране на наименования на фирми
    /// преди съпоставяне (премахване на кавички, тирета, свръхпразни пространства и т.н.).
    /// </summary>
    public static class StringNormalizationService
    {
        /// <summary>
        /// Нормализира фирмено име за последователно съпоставяне.
        /// Премахва различни видове кавички, замества тирета с ASCII hyphen
        /// и свива множество празни пространства.
        /// </summary>
        public static string NormalizeCompanyName(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var s = input.Trim();

            s = s.Replace("\"", " ")
                .Replace("”", " ")
                .Replace("„", " ")
                .Replace("“", " ")
                .Replace("«", " ")
                .Replace("»", " ")
                .Replace("—", "-")
                .Replace(" -", "-")
                .Replace("- ", "-")
                .Replace("   ", " ")
                .Replace("  ", " ")
                .Replace("  ", " ")
                .Replace("  ", " ")
                .Replace(". ", ".")
                .Replace(", ,", " ")
                .Replace(",,", " ")
                .Replace(":", " ")
                .Replace(";", " ")
                .Replace(" \n", " ")
                .Replace("\n", " ");

            // Remove common quote characters
            // Цел: отстраняване на разнообразни кавички, за да не влияят на сравненията
            var quotes = new[] { '"', '\'', '\u2018', '\u2019', '\u201C', '\u201D', '\u00AB', '\u00BB', '\u201A', '\u201B' };
            foreach (var q in quotes) s = s.Replace(q.ToString(), string.Empty);

            // Replace a broad set of dash characters with ASCII hyphen
            // Цел: стандартизиране на тиретата, тъй като някои са различни unicode символи
            var dashes = new[] { '\u2013', '\u2014', '\u2010', '\u2011', '\u2012', '\u2015', '\u2212', '\uFF0D', '\u2E3A', '\u2E3B', '\u2017' };
            foreach (var d in dashes) s = s.Replace(d.ToString(), "-");

            // Collapse multiple whitespace characters into a single space
            s = Regex.Replace(s, "\\s+", " ");

            return s.Trim();
        }
    }
}