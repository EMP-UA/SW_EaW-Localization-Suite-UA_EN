using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using CsvHelper;
using CsvHelper.Configuration;
using EaWLocalizationTool.Core;      // UA: новий Core проєкт / EN: new Core project
using EaWLocalizationTool.Models;
using EaWLocalizationTool.UI;

namespace EaWLocalizationTool.Core;

/// <summary>
/// UA: Реконструює ігрові файли з таблиць Review, підставляючи переклади у вихідні формати.
///     DAT файли: використовує DatProcessor.WriteSafe — побайтова гарантія структури.
/// EN: Reconstructs game files from Review tables, injecting translations into original formats.
///     DAT files: uses DatProcessor.WriteSafe — byte-perfect structure guarantee.
/// </summary>
public class Repacker
{
    private readonly CsvConfiguration _csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        Delimiter        = "\t",
        HeaderValidated  = null,
        MissingFieldFound = null
    };

    public (int reconstructed, int copied) RepackAll(string reviewDir, string originalDir, string finalDir)
    {
        var allOriginalFiles = Directory.GetFiles(originalDir, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)).ToList();

        // UA: Завантажуємо MASTER TXT та групуємо по відносному шляху
        // EN: Load MASTER TXT and group by relative file path
        var txtTranslations = new Dictionary<string, List<TextEntry>>(StringComparer.OrdinalIgnoreCase);
        string masterTxtPath = Path.Combine(reviewDir, "!MASTER_REVIEW_TXT.tsv");
        if (File.Exists(masterTxtPath))
        {
            foreach (var group in ReadReviewTsv(masterTxtPath).GroupBy(e => e.FilePath))
                txtTranslations[group.Key.Replace("\\", "/")] = group.ToList();
        }

        int reconstructed = 0, copied = 0;

        for (int i = 0; i < allOriginalFiles.Count; i++)
        {
            string originalPath    = allOriginalFiles[i];
            string relativePath    = originalPath.Substring(originalDir.Length);
            string normalizedRel   = relativePath.Replace("\\", "/");
            string finalPath       = finalDir + relativePath;

            ConsoleHelper.DrawProgressBar(i + 1, allOriginalFiles.Count, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

            string extension  = Path.GetExtension(originalPath).ToLower();
            bool   isProcessed = false;

            if (extension == ".txt")
            {
                txtTranslations.TryGetValue(normalizedRel, out var entries);
                RepackTxt(entries ?? new List<TextEntry>(), originalPath, finalPath);
                reconstructed++;
                isProcessed = true;
            }
            else
            {
                string reviewTsvPath = reviewDir + relativePath + ".tsv";
                if (File.Exists(reviewTsvPath))
                {
                    var tsvEntries = ReadReviewTsv(reviewTsvPath);
                    if (extension == ".xml")
                        RepackXml(tsvEntries, originalPath, finalPath);
                    else if (extension == ".dat")
                        RepackDat(tsvEntries, originalPath, finalPath); // ← оновлений метод
                    reconstructed++;
                    isProcessed = true;
                }
            }

            if (!isProcessed) { File.Copy(originalPath, finalPath, true); copied++; }
        }

        return (reconstructed, copied);
    }

    // ── TXT ───────────────────────────────────────────────────────────────────

    private void RepackTxt(List<TextEntry> entries, string originalPath, string finalPath)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        byte[]   fileBytes = File.ReadAllBytes(originalPath);
        Encoding enc = (fileBytes.Length >= 2 && fileBytes[0] == 0xFF && fileBytes[1] == 0xFE)
            ? Encoding.Unicode : Encoding.UTF8;

        string[] lines = File.ReadAllLines(originalPath, enc);

        var transMap = new Dictionary<int, string>();
        foreach (var e in entries)
            transMap.TryAdd(int.Parse(e.Key), e.TranslatedText ?? e.OriginalText);

        using var writer = new StreamWriter(finalPath, false, new UnicodeEncoding(false, true));
        for (int i = 0; i < lines.Length; i++)
        {
            if (transMap.TryGetValue(i, out var translated)) writer.WriteLine(translated);
            else writer.WriteLine(lines[i]);
        }
    }

    // ── XML ───────────────────────────────────────────────────────────────────

    private void RepackXml(List<TextEntry> entries, string originalPath, string finalPath)
    {
        XDocument doc = XDocument.Load(originalPath, LoadOptions.PreserveWhitespace);
        foreach (var entry in entries)
        {
            var element = doc.XPathSelectElement(entry.Key);
            if (element != null)
                element.Value = entry.TranslatedText ?? entry.OriginalText;
        }

        var settings = new XmlWriterSettings
        {
            Indent = true, IndentChars = "\t",
            OmitXmlDeclaration = true,
            Encoding = new UTF8Encoding(false),
            NewLineHandling = NewLineHandling.None
        };
        using var sw = new StreamWriter(finalPath, false, new UTF8Encoding(false));
        sw.Write("<?xml version=\"1.0\" ?>");
        using var xmlWriter = XmlWriter.Create(sw, settings);
        doc.Save(xmlWriter);
    }

    // ── DAT ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// UA: Перепаковує DAT файл використовуючи DatProcessor.WriteSafe.
    ///     ГАРАНТІЯ: CRC32, ключі та порядок записів — побайтова копія оригіналу.
    ///     Більше не перераховує CRC32 і не перекодує ключі.
    ///
    /// EN: Repacks DAT file using DatProcessor.WriteSafe.
    ///     GUARANTEE: CRC32, keys and record order — byte-perfect copy from original.
    ///     No longer recomputes CRC32 or re-encodes keys.
    /// </summary>
    private void RepackDat(List<TextEntry> entries, string originalPath, string finalPath)
    {
        // UA: Будуємо словник перекладів з TSV записів
        // EN: Build translation dictionary from TSV entries
        var translations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var e in entries)
        {
            string text = e.TranslatedText ?? e.OriginalText;
            if (!string.IsNullOrEmpty(text))
                translations.TryAdd(e.Key, text);
        }

        // UA: Читаємо оригінальний бінарний файл і безпечно записуємо новий
        //     DatProcessor.WriteSafe гарантує побайтове збереження структури
        // EN: Read original binary file and safely write new one
        //     DatProcessor.WriteSafe guarantees byte-perfect structure preservation
        byte[] origRaw = File.ReadAllBytes(originalPath);
        DatProcessor.WriteSafe(finalPath, origRaw, translations);
    }

    // ── TSV Reader ────────────────────────────────────────────────────────────

    private List<TextEntry> ReadReviewTsv(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8);
        using var csv    = new CsvReader(reader, _csvConfig);
        return csv.GetRecords<TextEntry>().ToList();
    }
}
