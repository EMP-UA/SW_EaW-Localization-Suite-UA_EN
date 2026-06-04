// ===================================================
//  Star Wars: EaW - Localization Tool
//  Created by EMP_UA
// ===================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using EaWLocalizationTool.Models;

namespace EaWLocalizationTool.Parsers;

/// <summary>
/// UA: Парсер для TXT файлів з інтелектуальною фільтрацією технічних рядків.
/// EN: Parser for TXT files with intelligent filtering of technical lines.
/// </summary>
public class TxtParser
{
    public List<TextEntry> Parse(string absolutePath, string relativePath)
    {
        var entries = new List<TextEntry>();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        byte[] fileBytes = File.ReadAllBytes(absolutePath);
        
        // UA: Визначаємо кодування: UTF-16 LE (BOM) або UTF-8
        // EN: Detect encoding: UTF-16 LE (BOM) or UTF-8
        Encoding enc = (fileBytes.Length >= 2 && fileBytes[0] == 0xFF && fileBytes[1] == 0xFE) ? Encoding.Unicode : Encoding.UTF8;

        string[] lines = File.ReadAllLines(absolutePath, enc);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // UA: Пропускаємо технічні рядки, які не потребують перекладу
            // EN: Skip technical lines that do not require translation
            if (IsLikelyTechnical(line)) continue;

            entries.Add(new TextEntry
            {
                FilePath = relativePath,
                FileType = "TXT",
                Key = i.ToString(),
                OriginalText = lines[i],
                OriginalEncodingName = enc.WebName
            });
        }
        return entries;
    }

    /// <summary>
    /// UA: Визначає, чи є рядок технічним кодом рушія Alamo.
    /// EN: Determines if a line is a technical Alamo engine code.
    /// </summary>
    private bool IsLikelyTechnical(string line)
    {
        // UA: 1. Рядки з багатьма табуляціями (дані SURFACE, анімації)
        // EN: 1. Lines with multiple tabs (SURFACE data, animations)
        if (line.Contains('\t') && line.Split('\t').Length > 2) return true;

        // UA: 2. Шляхи до ігрових ресурсів
        // EN: 2. Paths to game resources
        if (Regex.IsMatch(line, @"\.(wav|dds|tga|alo|lua|xml|txt)$", RegexOptions.IgnoreCase)) return true;

        // UA: 3. Ідентифікатори (ВЕЛИКІ_ЛІТЕРИ_З_ПІДКРЕСЛЕННЯМ без пробілів)
        // EN: 3. Identifiers (UPPERCASE_WITH_UNDERSCORES without spaces)
        if (line.Contains('_') && !line.Contains(' ') && line == line.ToUpperInvariant()) return true;

        // UA: 4. Складні технічні пресети (напр. SURFACE ei_trooper_move...)
        // EN: 4. Complex technical presets (e.g., SURFACE ei_trooper_move...)
        if (line.Split(' ').Length > 3 && line.Any(char.IsDigit) && line.Contains('_')) return true;

        return false;
    }
}