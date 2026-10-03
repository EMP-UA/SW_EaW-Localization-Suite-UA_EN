using System.IO;
using EaWLocalizationTool.Core;
using EaWLocalizationTool.Core.Models;
using EaWLocalizationTool.GUI.Models;

namespace EaWLocalizationTool.GUI.Services;

/// <summary>
/// UA: Тонка обгортка над DatProcessor (Core) для потреб GUI.
///     Конвертує DatEntry → TranslationEntry (ViewModel з INotifyPropertyChanged).
/// EN: Thin wrapper over DatProcessor (Core) for GUI needs.
///     Converts DatEntry → TranslationEntry (ViewModel with INotifyPropertyChanged).
/// </summary>
public static class DatService
{
    // ── Parse ─────────────────────────────────────────────────────────────────

    public static (List<TranslationEntry> Entries, byte[] RawBytes) ParseOriginal(string path)
    {
        var (coreEntries, raw) = DatProcessor.ReadFile(path);
        var entries = coreEntries.Select(e => new TranslationEntry(e)).ToList();
        return (entries, raw);
    }

    // ── Translation sources ───────────────────────────────────────────────────

    public static Dictionary<string, string> ParseTsv(string path)
        => DatProcessor.ParseTsvTranslations(path);

    /// <summary>
    /// UA: Статуси вичитки з робочого TSV (колонка ReviewStatus): Key → статус.
    /// EN: Review statuses from a working TSV (ReviewStatus column): Key → status.
    /// </summary>
    public static Dictionary<string, string> ParseTsvReviews(string path)
        => DatProcessor.ParseTsvReviews(path);

    /// <summary>
    /// UA: Читає DAT як джерело перекладу.
    ///     Дублікати ключів (TEXT_END_OF_DATA та ін.) ігноруються: перший запис виграє.
    ///     Технічні рядки (лише пробіли) виключаються — вони завжди беруться з оригіналу.
    /// EN: Reads a DAT as a translation source.
    ///     Duplicate keys (TEXT_END_OF_DATA etc.) are ignored: the first entry wins.
    ///     Technical entries (whitespace only) are excluded — they always use the original bytes.
    /// </summary>
    public static Dictionary<string, string> ParseDatAsTranslation(string path)
    {
        var (entries, _) = DatProcessor.ReadFile(path);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var e in entries)
        {
            // UA: Пропускаємо технічні рядки (пробільні роздільники)
            // EN: Skip technical entries (whitespace separators)
            if (string.IsNullOrWhiteSpace(e.OriginalText)) continue;
            // UA: TryAdd — перший запис виграє при дублікатах ключів
            // EN: TryAdd — first entry wins for duplicate keys
            map.TryAdd(e.Key, e.OriginalText);
        }
        return map;
    }

    // ── Cross-DAT donor ───────────────────────────────────────────────────────

    /// <summary>
    /// UA: Читає пари «ключ / оригінал / переклад» зі TSV-донора.
    ///     Original = null, якщо у файлі немає колонки оригіналу.
    /// EN: Reads "key / original / translation" pairs from a TSV donor.
    ///     Original = null when the file has no original column.
    /// </summary>
    public static List<(string Key, string? Original, string Translation)> ParseTsvPairs(string path)
        => DatProcessor.ParseTsvPairs(path);

    /// <summary>
    /// UA: Читає записи ванільного DAT-донора (ключ + англійський оригінал).
    /// EN: Reads the entries of a vanilla donor DAT (key + English original).
    /// </summary>
    public static List<DatEntry> ReadDonorOriginals(string path)
        => DatProcessor.ReadFile(path).Entries;

    // ── Working file identity ─────────────────────────────────────────────────

    /// <summary>
    /// UA: Короткий хеш набору ключів оригінального DAT (SHA-256, перші 8 hex-символів).
    ///     Основна гра й доповнення мають однакові імена файлів, але різні набори ключів,
    ///     тому хеш відрізняє їхні робочі TSV.
    /// EN: Short hash of the original DAT's key set (SHA-256, first 8 hex characters).
    ///     The base game and the expansion share file names but not key sets, so the
    ///     hash tells their working TSV files apart.
    /// </summary>
    public static string KeySetHash(IEnumerable<string> keys)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join('\n', keys));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))[..8].ToLowerInvariant();
    }

    // ── Safe write ────────────────────────────────────────────────────────────

    /// <summary>
    /// UA: Безпечний запис через DatProcessor.WriteSafe.
    ///     Словник перекладів збирається циклом, а не .ToDictionary():
    ///     останній запис виграє при дублікатах ключів (TEXT_END_OF_DATA тощо),
    ///     і виняток не виникає.
    ///     Записи, де Translated порожній, отримують оригінальні байти.
    /// EN: Safe write via DatProcessor.WriteSafe.
    ///     The translation dictionary is built with a loop rather than .ToDictionary():
    ///     the last entry wins for duplicate keys (TEXT_END_OF_DATA etc.)
    ///     and no exception is thrown.
    ///     Entries where Translated is empty receive the original bytes.
    /// </summary>
    public static void WriteSafe(string outputPath, byte[] origRaw, List<TranslationEntry> entries)
    {
        var translations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var e in entries.Where(e => !string.IsNullOrEmpty(e.Translated)))
            translations[e.Key] = e.Translated;

        DatProcessor.WriteSafe(outputPath, origRaw, translations);
    }

    // ── Export ────────────────────────────────────────────────────────────────

    /// <summary>
    /// UA: Експортує всі записи у TSV: Key, OriginalText, TranslatedText, ReviewStatus.
    ///     Такий файл можна завантажити назад як джерело перекладу (②) —
    ///     разом з перекладами відновлюються й статуси вичитки.
    /// EN: Exports all entries to TSV: Key, OriginalText, TranslatedText, ReviewStatus.
    ///     The file can be loaded back as a translation source (②) —
    ///     review statuses are restored together with the translations.
    /// </summary>
    public static void ExportTsv(string path, IEnumerable<TranslationEntry> entries)
    {
        var list    = entries.ToList();
        var trans   = new Dictionary<string, string>(StringComparer.Ordinal);
        var reviews = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var e in list)
        {
            if (e.IsTranslated) trans[e.Key] = e.Translated;
            if (e.HasReviewMark) reviews[e.Key] = e.ReviewStatus.Trim();
        }

        DatProcessor.ExportTsv(path, list.Select(e => e.Core), trans, reviews);
    }
}
