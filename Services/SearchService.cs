using System.Collections.Concurrent;

namespace AddEiksInXlsxFile.Services
{
    public class SearchService
    {
        // store edited eiks by normalized company name
        private readonly ConcurrentDictionary<string, string> _edits = new();

        public void SetEdit(string normalizedName, string eik)
        {
            _edits[normalizedName ?? string.Empty] = eik ?? string.Empty;
        }

        public bool TryGetEdit(string normalizedName, out string? eik)
        {
            return _edits.TryGetValue(normalizedName ?? string.Empty, out eik);
        }

        public IReadOnlyDictionary<string, string> GetAllEdits() => _edits;

        public void Clear() => _edits.Clear();
    }
}
