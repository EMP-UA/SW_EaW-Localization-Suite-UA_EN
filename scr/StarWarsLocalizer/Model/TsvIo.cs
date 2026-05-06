// ===================================================
//  Star Wars: EaW - Localization Tool
//  Created by EMP_UA
// ===================================================
//  Model/TsvIo.cs
//  UA: Утиліти читання та запису TSV-файлів.
//      Формат заголовка відповідає інструменту-екстрактору.
//  EN: TSV file read/write utilities.
//      Header format matches the extractor tool.
// ============================================================

using System.Text;

namespace EaWLocalizer.Model;

/// <summary>
/// UA: Статичний клас для роботи з TSV-файлами.
///     ReadFile — читання з BeforeAPI або AfterAPI.
///     WriteFile — запис у AfterAPI (з заголовком, UTF-8 без BOM).
/// EN: Static class for TSV file operations.
///     ReadFile — read from BeforeAPI or AfterAPI.
///     WriteFile — write to AfterAPI (with header, UTF-8 without BOM).
/// </summary>
public static class TsvIo
{
    // UA: Офіційний заголовок таблиці. Має збігатися з форматом екстрактора.
    //     Перевіряється при читанні (рядок-заголовок пропускається).
    // EN: Official table header. Must match the extractor's format.
    //     Verified during reading (the header line is skipped).
    public const string Header =
        "FilePath\tFileType\tKey\tOriginalText\tTranslatedText\tOriginalEncodingName";

    /// <summary>
    /// UA: Читає TSV-файл і повертає список рядків TsvRow.
    ///     Якщо файл не існує — повертає порожній список (не кидає виключення).
    ///     Пошкоджені рядки (менше 6 полів) мовчки пропускаються.
    /// EN: Reads a TSV file and returns a list of TsvRow records.
    ///     If the file doesn't exist — returns empty list (no exception).
    ///     Malformed rows (fewer than 6 fields) are silently skipped.
    /// </summary>
    public static List<TsvRow> ReadFile(string path)
    {
        var rows = new List<TsvRow>();
        if (!File.Exists(path)) return rows;

        foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
        {
            // UA: Пропускаємо заголовок та порожні рядки.
            // EN: Skip the header and blank lines.
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("FilePath\t"))
                continue;

            var row = ParseLine(line);
            if (row is not null) rows.Add(row);
        }
        return rows;
    }

    /// <summary>
    /// UA: Записує список рядків у TSV-файл.
    ///     Кодування: UTF-8 без BOM (для максимальної сумісності з редакторами).
    ///     Папка створюється автоматично якщо не існує.
    ///     Файл перезаписується повністю — це атомарний snapshot прогресу.
    /// EN: Writes a list of rows to a TSV file.
    ///     Encoding: UTF-8 without BOM (for maximum editor compatibility).
    ///     Directory is created automatically if it doesn't exist.
    ///     File is fully overwritten — this is an atomic progress snapshot.
    /// </summary>
    public static void WriteFile(string path, IEnumerable<TsvRow> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var sw = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        sw.WriteLine(Header);
        foreach (var r in rows)
            sw.WriteLine(r.ToTsvLine());
    }

    // ── PRIVATE HELPERS ──────────────────────────────────────────────────

    // UA: Парсить один рядок TSV у TsvRow.
    //     Split(maxCount: 6) — останнє поле може містити табуляції у тексті
    //     (після unescape) тому беремо рівно 6 перших ділільників.
    //     Повертає null якщо рядок пошкоджений.
    // EN: Parses one TSV line into a TsvRow.
    //     Split(maxCount: 6) — the last field may contain tabs in text
    //     (after unescape) so we take exactly the first 6 delimiters.
    //     Returns null if the line is malformed.
    static TsvRow? ParseLine(string line)
    {
        var p = line.Split('\t', 6);
        if (p.Length < 6) return null;

        return new TsvRow
        {
            FilePath             = p[0].Trim(),
            FileType             = p[1].Trim().ToLowerInvariant(),
            Key                  = p[2].Trim(),
            // UA: Розекрануємо "\\t" назад у реальний символ Tab.
            // EN: Unescape "\\t" back to a real Tab character.
            OriginalText         = p[3].Replace("\\t", "\t"),
            TranslatedText       = p[4].Replace("\\t", "\t"),
            OriginalEncodingName = p[5].Trim()
        };
    }
}
