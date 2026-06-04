using System.Text;
using EaWLocalizationTool.Core.Models;

namespace EaWLocalizationTool.Core;

/// <summary>
/// UA: Єдина точка роботи з бінарними DAT файлами рушія Alamo.
///     Використовується і консоллю (Repacker), і GUI (DatService).
///
///     ГАРАНТІЇ БЕЗПЕКИ при WriteSafe:
///     • CRC32      → побайтова копія з оригіналу (без перерахунку)
///     • Ключі      → побайтова копія з оригіналу (без перекодування)
///     • Порядок    → ідентичний оригіналу
///     • Кількість  → ідентична оригіналу
///     • Змінюється → лише байти значень (перекладений текст)
///
/// EN: Single point of entry for Alamo engine binary DAT files.
///     Used by both console (Repacker) and GUI (DatService).
///
///     SAFETY GUARANTEES in WriteSafe:
///     • CRC32     → byte-perfect copy from original (no recomputation)
///     • Keys      → byte-perfect copy from original (no re-encoding)
///     • Order     → identical to original
///     • Count     → identical to original
///     • Changed   → only value bytes (translated text)
/// </summary>
public static class DatProcessor
{
    // ── Parse ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// UA: Читає DAT файл з диску, повертає записи та сирі байти.
    /// EN: Reads DAT file from disk, returns entries and raw bytes.
    /// </summary>
    public static (List<DatEntry> Entries, byte[] RawBytes) ReadFile(string path)
    {
        byte[] raw = File.ReadAllBytes(path);
        return (Parse(raw), raw);
    }

    /// <summary>
    /// UA: Парсить бінарний буфер DAT. Зберігає сирі байти CRC32 і KeyLength.
    /// EN: Parses binary DAT buffer. Stores raw CRC32 and KeyLength bytes.
    /// </summary>
    public static List<DatEntry> Parse(byte[] raw)
    {
        if (raw.Length < 4)
            throw new InvalidDataException("DAT файл занадто малий / DAT file too small");

        uint count = BitConverter.ToUInt32(raw, 0);
        if (count == 0 || count > 500_000)
            throw new InvalidDataException($"Некоректна кількість записів: {count} / Invalid record count: {count}");

        // UA: Читаємо таблицю індексів, зберігаємо сирі байти
        // EN: Read index table, store raw bytes
        var index = new (byte[] CrcRaw, uint Vl, byte[] KlRaw, uint Kl)[count];
        int off = 4;
        for (int i = 0; i < count; i++)
        {
            index[i] = (
                raw[off..(off + 4)],
                BitConverter.ToUInt32(raw, off + 4),
                raw[(off + 8)..(off + 12)],
                BitConverter.ToUInt32(raw, off + 8)
            );
            off += 12;
        }

        // UA: Обчислюємо позиції блоків
        // EN: Calculate block positions
        int  valStart      = 4 + (int)count * 12;
        long totalValBytes = index.Sum(r => (long)r.Vl * 2);
        int  keyStart      = valStart + (int)totalValBytes;

        // UA: Парсимо записи
        // EN: Parse entries
        var entries = new List<DatEntry>((int)count);
        int vp = valStart, kp = keyStart;

        for (int i = 0; i < count; i++)
        {
            var (crcRaw, vl, klRaw, kl) = index[i];

            string text = Encoding.Unicode.GetString(raw, vp, (int)(vl * 2)).TrimEnd('\0');
            vp += (int)(vl * 2);

            string key = Encoding.ASCII.GetString(raw, kp, (int)kl).TrimEnd('\0');
            kp += (int)kl;

            entries.Add(new DatEntry
            {
                OriginalIndex = i,
                Key           = key,
                OriginalText  = text,
                RawCrc32      = crcRaw,
                RawKeyLength  = klRaw,
                KeyLength     = kl,
            });
        }

        return entries;
    }

    // ── Safe Write ────────────────────────────────────────────────────────────

    /// <summary>
    /// UA: БЕЗПЕЧНИЙ ЗАПИС.
    ///     Читає оригінальний бінарний файл і замінює лише байти значень.
    ///     CRC32, ключі та порядок записів — побайтова копія оригіналу.
    ///     Записи без перекладу отримують оригінальні байти значень.
    ///
    /// EN: SAFE WRITE.
    ///     Reads original binary file and replaces only value bytes.
    ///     CRC32, keys and record order — byte-perfect copy from original.
    ///     Records without translation receive original value bytes.
    /// </summary>
    public static void WriteSafe(
        string outputPath,
        byte[] origRaw,
        IReadOnlyDictionary<string, string> translations)
    {
        uint count = BitConverter.ToUInt32(origRaw, 0);

        // UA: Зчитуємо індексну таблицю зі збереженням сирих байтів
        // EN: Read index table keeping raw bytes
        var index = new (byte[] CrcRaw, uint Vl, byte[] KlRaw, uint Kl)[(int)count];
        int off = 4;
        for (int i = 0; i < count; i++)
        {
            index[i] = (
                origRaw[off..(off + 4)],
                BitConverter.ToUInt32(origRaw, off + 4),
                origRaw[(off + 8)..(off + 12)],
                BitConverter.ToUInt32(origRaw, off + 8)
            );
            off += 12;
        }

        // UA: Копіюємо блок ключів побайтово
        // EN: Copy key block byte-perfect
        int  valStart      = 4 + (int)count * 12;
        long origTotalValB = index.Sum(r => (long)r.Vl * 2);
        int  keyStart      = valStart + (int)origTotalValB;
        int  totalKeyBytes = index.Sum(r => (int)r.Kl);
        byte[] keyBlock    = origRaw[keyStart..(keyStart + totalKeyBytes)];

        // UA: Оригінальні діапазони значень (для резервного копіювання)
        // EN: Original value ranges (for fallback copy)
        var origValRanges = new (int Start, int Len)[(int)count];
        int vp = valStart;
        for (int i = 0; i < count; i++)
        {
            origValRanges[i] = (vp, (int)(index[i].Vl * 2));
            vp += (int)(index[i].Vl * 2);
        }

        // UA: Читаємо ключі з блоку ключів (для пошуку перекладу)
        // EN: Read keys from key block (for translation lookup)
        var keys = new string[(int)count];
        int kp = 0;
        for (int i = 0; i < count; i++)
        {
            keys[i] = Encoding.ASCII.GetString(keyBlock, kp, (int)index[i].Kl).TrimEnd('\0');
            kp += (int)index[i].Kl;
        }

        // UA: Будуємо нові масиви значень
        //     Є переклад → UTF-16 LE. Немає → оригінальні байти.
        // EN: Build new value arrays.
        //     Has translation → UTF-16 LE. None → original bytes.
        var newVals = new byte[(int)count][];
        for (int i = 0; i < count; i++)
        {
            if (translations.TryGetValue(keys[i], out var t) && !string.IsNullOrEmpty(t))
                newVals[i] = Encoding.Unicode.GetBytes(t);
            else
                newVals[i] = origRaw[origValRanges[i].Start..(origValRanges[i].Start + origValRanges[i].Len)];
        }

        // UA: Записуємо вихідний файл
        // EN: Write output file
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var w  = new BinaryWriter(fs);

        w.Write(count);

        for (int i = 0; i < count; i++)
        {
            w.Write(index[i].CrcRaw);                       // CRC32 — побайтово / byte-perfect
            w.Write((uint)(newVals[i].Length / 2));          // нова довжина / new length
            w.Write(index[i].KlRaw);                        // KeyLength — побайтово / byte-perfect
        }

        foreach (var v in newVals) w.Write(v);
        w.Write(keyBlock); // ключі — побайтово / keys — byte-perfect
    }

    // ── TSV ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// UA: Читає TSV файл перекладу → словник Key → TranslatedText.
    ///     Підтримує колонки з будь-якою назвою що містить "Translat".
    /// EN: Reads translation TSV → Key → TranslatedText dictionary.
    ///     Supports columns with any name containing "Translat".
    /// </summary>
    public static Dictionary<string, string> ParseTsvTranslations(string path)
    {
        var    map   = new Dictionary<string, string>(StringComparer.Ordinal);
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        if (lines.Length < 2) return map;

        string[] headers = lines[0].TrimStart('\uFEFF').Split('\t');
        int ki = Array.FindIndex(headers, h => h.Trim().Equals("Key", StringComparison.OrdinalIgnoreCase));
        int ti = Array.FindIndex(headers, h => h.Trim().Contains("Translat", StringComparison.OrdinalIgnoreCase));

        if (ki < 0 || ti < 0)
            throw new InvalidDataException("Не знайдено колонки Key/TranslatedText / Key/TranslatedText columns not found");

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] cols = lines[i].Split('\t');
            if (Math.Max(ki, ti) >= cols.Length) continue;
            string key = cols[ki].Trim();
            string val = cols[ti].Replace("\\n", "\n");
            if (!string.IsNullOrEmpty(key)) map[key] = val;
        }

        return map;
    }

    /// <summary>
    /// UA: Експортує записи у TSV файл (UTF-8 з BOM).
    /// EN: Exports entries to TSV file (UTF-8 with BOM).
    /// </summary>
    public static void ExportTsv(string path, IEnumerable<DatEntry> entries,
        IReadOnlyDictionary<string, string>? translations = null)
    {
        static string Esc(string s) => s.Replace("\n", "\\n").Replace("\t", " ");

        using var sw = new StreamWriter(path, false,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        sw.WriteLine("Key\tOriginalText\tTranslatedText");
        foreach (var e in entries)
        {
            string trans = translations?.TryGetValue(e.Key, out var t) == true ? t : "";
            sw.WriteLine($"{Esc(e.Key)}\t{Esc(e.OriginalText)}\t{Esc(trans)}");
        }
    }
}
