// ===================================================
//  Star Wars: EaW - Localization Tool
//  Created by EMP_UA
// ===================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using EaWLocalizationTool.Models;
using EaWLocalizationTool.Parsers;
using EaWLocalizationTool.UI;

namespace EaWLocalizationTool.Core;

/// <summary>
/// UA: Керує експортом тексту. Групує TXT у єдиний мастер-файл.
/// EN: Manages text export. Groups TXT into a single master file.
/// </summary>
public class FileProcessor
{
    private readonly CsvConfiguration _config = new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = "\t" };

    /// <summary>
    /// UA: Витягує оригінальні тексти з файлів гри (.xml, .dat, .txt) та перетворює їх на .tsv
    /// EN: Extracts original texts from game files (.xml, .dat, .txt) and converts them to .tsv
    /// </summary>
    public (int xml, int dat, int txt) ExtractOriginals(string source, string target)
    {
        var allFiles = Directory.GetFiles(source, "*.*", SearchOption.AllDirectories);
        var files = allFiles.Where(f =>
            f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)).ToList();

        int xmlCount = 0, datCount = 0, txtCount = 0;
        var masterTxtEntries = new List<TextEntry>();

        for (int i = 0; i < files.Count; i++)
        {
            string filePath = files[i];
            string rel = filePath.Substring(source.Length);
            ConsoleHelper.DrawProgressBar(i + 1, files.Count, rel);
            string ext = Path.GetExtension(filePath).ToLower();

            if (ext == ".txt")
            {
                masterTxtEntries.AddRange(new TxtParser().Parse(filePath, rel));
                txtCount++;
            }
            else
            {
                var entries = ext == ".xml" ? new XmlParser().Parse(filePath, rel) : new DatParser().Parse(filePath, rel);
                if (ext == ".xml") xmlCount++; else datCount++;

                if (entries.Any())
                {
                    string outPath = target + rel + ".tsv";
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                    using var writer = new StreamWriter(outPath, false, Encoding.UTF8);
                    using var csv = new CsvWriter(writer, _config);
                    csv.WriteRecords(entries);
                }
            }
        }

        // UA: Зберігаємо всі відфільтровані TXT рядки в один файл
        // EN: Save all filtered TXT lines into a single file
        if (masterTxtEntries.Any())
        {
            string masterPath = Path.Combine(target, "!MASTER_BEFORE_API_TXT.tsv");
            using var writer = new StreamWriter(masterPath, false, Encoding.UTF8);
            using var csv = new CsvWriter(writer, _config);
            csv.WriteRecords(masterTxtEntries);
        }

        return (xmlCount, datCount, txtCount);
    }
}