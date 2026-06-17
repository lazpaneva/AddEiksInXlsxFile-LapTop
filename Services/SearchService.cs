using System.Collections.Concurrent;

namespace AddEiksInXlsxFile.Services
{
    /// <summary>
    /// Проста in-memory служба за съхраняване на редактирани EIK стойности,
    /// индексирани по нормализирано наименование на фирма.
    /// </summary>
    public class SearchService
    {
        // store edited eiks by normalized company name
        private readonly ConcurrentDictionary<string, string> _edits = new();

        /// <summary>
        /// Записва или обновява редактирано EIK за дадено нормализирано име.
        /// </summary>
        public void SetEdit(string normalizedName, string eik)
        {
            _edits[normalizedName ?? string.Empty] = eik ?? string.Empty;
        }

        /// <summary>
        /// Опитва да върне редактирано EIK за дадено нормализирано име.
        /// </summary>
        public bool TryGetEdit(string normalizedName, out string? eik)
        {
            return _edits.TryGetValue(normalizedName ?? string.Empty, out eik);
        }

        /// <summary>
        /// Връща всички редакции като само-четим речник.
        /// </summary>
        public IReadOnlyDictionary<string, string> GetAllEdits() => _edits;

        /// <summary>
        /// Изчиства всички записани редакции.
        /// </summary>
        public void Clear() => _edits.Clear();
    }
}
