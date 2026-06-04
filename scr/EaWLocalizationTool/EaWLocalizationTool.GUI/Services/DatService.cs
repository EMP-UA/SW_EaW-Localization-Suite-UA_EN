using System.IO;
using EaWLocalizationTool.Core;
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
    /// UA: Читає DAT як джерело перекладу.
    ///     ВИПРАВЛЕНО: foreach + TryAdd ігнорує дублікати ключів (TEXT_END_OF_DATA та ін.).
    ///     Технічні рядки (лише пробіли) виключаються — вони завжди беруться з оригіналу.
    /// EN: Reads DAT as translation source.
    ///     FIXED: foreach + TryAdd ignores duplicate keys (TEXT_END_OF_DATA etc.).
    ///     Technical entries (whitespace only) excluded — always use original bytes.
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

    // ── Safe write ────────────────────────────────────────────────────────────

    /// <summary>
    /// UA: Безпечний запис через DatProcessor.WriteSafe.
    ///     ВИПРАВЛЕНО: foreach + перезапис замість .ToDictionary() —
    ///     .ToDictionary() кидає виняток при дублікатах (TEXT_END_OF_DATA).
    ///     Записи де Translated порожній → беруться оригінальні байти.
    /// EN: Safe write via DatProcessor.WriteSafe.
    ///     FIXED: foreach + overwrite instead of .ToDictionary() —
    ///     .ToDictionary() throws on duplicates (TEXT_END_OF_DATA).
    ///     Entries where Translated is empty → original bytes are used.
    /// </summary>
    public static void WriteSafe(string outputPath, byte[] origRaw, List<TranslationEntry> entries)
    {
        // UA: Використовуємо foreach щоб уникнути виняток на дублікатах ключів
        //     При дублікатах: останній запис виграє (всі мають однакове значення)
        // EN: Use foreach to avoid exception on duplicate keys
        //     For duplicates: last entry wins (all have the same value anyway)
        var translations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var e in entries.Where(e => !string.IsNullOrEmpty(e.Translated)))
            translations[e.Key] = e.Translated;

        DatProcessor.WriteSafe(outputPath, origRaw, translations);
    }

    // ── Export ────────────────────────────────────────────────────────────────

    public static void ExportTsv(string path, IEnumerable<TranslationEntry> entries)
    {
        var list  = entries.ToList();
        var trans = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var e in list.Where(e => e.IsTranslated))
            trans[e.Key] = e.Translated;
        DatProcessor.ExportTsv(path, list.Select(e => e.Core), trans);
    }
}
