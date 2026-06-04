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
using EaWLocalizationTool.UI;

namespace EaWLocalizationTool.Core;

/// <summary>
/// UA: Формує файли Review, поєднуючи оригінальні тексти з перекладеними.
/// EN: Builds Review files by merging original texts with translated ones.
/// </summary>
public class ReviewBuilder
{
    private readonly CsvConfiguration _config = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        Delimiter = "\t",
        HeaderValidated = null,
        MissingFieldFound = null,
        // UA: FIX #2: Ігноруємо помилки форматування (напр. неекрановані лапки), щоб програма не "падала" на пошкоджених файлах від API.
        // EN: FIX #2: Ignore formatting errors (e.g., unescaped quotes) so the program doesn't crash on corrupted files from API.
        BadDataFound = null
    };

    public int BuildReviewFiles(string beforeDir, string afterDir, string reviewDir)
    {
        var files = Directory.GetFiles(beforeDir, "*.tsv", SearchOption.AllDirectories);
        int txtGroupedCount = 0;

        foreach (var file in files)
        {
            string rel = file.Substring(beforeDir.Length);
            ConsoleHelper.DrawProgressBar(Array.IndexOf(files, file) + 1, files.Length, rel);

            var originalEntries = Read(file);
            if (!originalEntries.Any()) continue;

            string transFile;
            string fileName = Path.GetFileName(file);
            bool isMaster = fileName.StartsWith("!MASTER");

            // UA: FIX #1: Спеціальна логіка для пошуку файлу !MASTER_AFTER_API_TXT.tsv
            // EN: FIX #1: Special logic for finding the !MASTER_AFTER_API_TXT.tsv file
            if (isMaster && fileName.Contains("_BEFORE_"))
            {
                string afterFileName = fileName.Replace("_BEFORE_", "_AFTER_");
                transFile = Path.Combine(afterDir, afterFileName);
            }
            else
            {
                transFile = afterDir + rel;
            }

            if (File.Exists(transFile))
            {
                var translatedEntries = Read(transFile);
                if (translatedEntries.Any())
                {
                    if (isMaster)
                    {
                        var transDict = translatedEntries
                            .GroupBy(x => (FilePath: x.FilePath.Replace("\\", "/"), x.Key))
                            .ToDictionary(g => g.Key, g => g.First().TranslatedText);

                        foreach (var entry in originalEntries)
                        {
                            var normalizedKey = (FilePath: entry.FilePath.Replace("\\", "/"), entry.Key);
                            if (transDict.TryGetValue(normalizedKey, out var translatedText) && !string.IsNullOrEmpty(translatedText))
                            {
                                entry.TranslatedText = translatedText;
                            }
                        }
                    }
                    else // XML/DAT
                    {
                        var transDict = new Dictionary<string, string>();
                        foreach (var entry in translatedEntries)
                        {
                            // UA: Використовуємо TryAdd, щоб уникнути падіння на дублікатах ключів у DAT
                            // EN: Use TryAdd to avoid crashing on duplicate keys in DAT
                            transDict.TryAdd(entry.Key, entry.TranslatedText);
                        }

                        foreach (var entry in originalEntries)
                        {
                            if (transDict.TryGetValue(entry.Key, out var translatedText) && !string.IsNullOrEmpty(translatedText))
                            {
                                entry.TranslatedText = translatedText;
                            }
                        }
                    }
                }
            }

            if (isMaster)
            {
                Save(originalEntries, Path.Combine(reviewDir, "!MASTER_REVIEW_TXT.tsv"), true);
                txtGroupedCount = originalEntries.Select(x => x.FilePath).Distinct().Count();
            }
            else
            {
                Save(originalEntries, reviewDir + rel, false);
            }
        }
        return txtGroupedCount;
    }

    private List<TextEntry> Read(string p)
    {
        try
        {
            using var r = new StreamReader(p, Encoding.UTF8);
            using var c = new CsvReader(r, _config);
            return c.GetRecords<TextEntry>().ToList();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[READ ERROR] {p}\n  {ex.Message}");
            Console.ResetColor();
            return new List<TextEntry>();
        }
    }

    private void Save(List<TextEntry> entries, string path, bool isMaster)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var w = new StreamWriter(path, false, Encoding.UTF8);
        using var c = new CsvWriter(w, _config);

        if (isMaster)
        {
            c.WriteRecords(entries.Select(x => new { x.FilePath, x.Key, x.OriginalText, x.TranslatedText }));
        }
        else
        {
            c.WriteRecords(entries.Select(x => new { x.Key, x.OriginalText, x.TranslatedText }));
        }
    }
}