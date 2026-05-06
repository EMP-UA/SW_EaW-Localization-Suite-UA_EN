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
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using CsvHelper;
using CsvHelper.Configuration;
using EaWLocalizationTool.Models;
using EaWLocalizationTool.UI;

namespace EaWLocalizationTool.Core;

/// <summary>
/// UA: Реконструює ігрові файли з таблиць Review, підставляючи переклади у вихідні формати.
/// EN: Reconstructs game files from Review tables, injecting translations into original formats.
/// </summary>
public class Repacker
{
    private readonly CsvConfiguration _csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        Delimiter = "\t",
        HeaderValidated = null,
        MissingFieldFound = null
    };

    public (int reconstructed, int copied) RepackAll(string reviewDir, string originalDir, string finalDir)
    {
        var allOriginalFiles = Directory.GetFiles(originalDir, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)).ToList();

        // UA: Завантажуємо MASTER TXT та одразу групуємо по відносному шляху файлу.
        // EN: Load MASTER TXT and immediately group by relative file path.
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
            string originalPath = allOriginalFiles[i];
            string relativePath = originalPath.Substring(originalDir.Length);
            string normalizedRelPath = relativePath.Replace("\\", "/");
            string finalPath = finalDir + relativePath;

            ConsoleHelper.DrawProgressBar(i + 1, allOriginalFiles.Count, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

            string extension = Path.GetExtension(originalPath).ToLower();
            bool isProcessed = false;

            if (extension == ".txt")
            {
                txtTranslations.TryGetValue(normalizedRelPath, out var entries);
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
                    if (extension == ".xml") RepackXml(tsvEntries, originalPath, finalPath);
                    else RepackDat(tsvEntries, originalPath, finalPath);
                    reconstructed++;
                    isProcessed = true;
                }
            }

            // UA: Файл без таблиці перекладу — копіюємо як є (технічні файли)
            // EN: File without a translation table — copy as-is (technical files)
            if (!isProcessed) { File.Copy(originalPath, finalPath, true); copied++; }
        }
        return (reconstructed, copied);
    }

    private void RepackTxt(List<TextEntry> entries, string originalPath, string finalPath)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        byte[] fileBytes = File.ReadAllBytes(originalPath);

        Encoding enc = (fileBytes.Length >= 2 && fileBytes[0] == 0xFF && fileBytes[1] == 0xFE)
            ? Encoding.Unicode
            : Encoding.UTF8;

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

    private void RepackXml(List<TextEntry> entries, string originalPath, string finalPath)
    {
        XDocument doc = XDocument.Load(originalPath, LoadOptions.PreserveWhitespace);
        foreach (var entry in entries)
        {
            var element = doc.XPathSelectElement(entry.Key);
            if (element != null) element.Value = entry.TranslatedText ?? entry.OriginalText;
        }

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "\t",
            OmitXmlDeclaration = true,
            Encoding = new UTF8Encoding(false),
            NewLineHandling = NewLineHandling.None
        };
        using var sw = new StreamWriter(finalPath, false, new UTF8Encoding(false));
        sw.Write("<?xml version=\"1.0\" ?>");
        using var xmlWriter = XmlWriter.Create(sw, settings);
        doc.Save(xmlWriter);
    }

    private void RepackDat(List<TextEntry> entries, string originalPath, string finalPath)
    {
        // UA: Будуємо карту перекладів: ключ → перекладений текст
        // EN: Build translation map: key → translated text
        var translationMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            translationMap.TryAdd(entry.Key, entry.TranslatedText ?? entry.OriginalText);
        }

        // UA: Читаємо оригінальний DAT щоб отримати ТОЧНІ CRC32 і порядок записів.
        //     Оригінальний файл є єдиним джерелом правди для рушія Alamo.
        // EN: Read original DAT to get EXACT CRC32 values and record order.
        //     The original file is the single source of truth for the Alamo engine.
        var originalRecords = ReadOriginalDatStructure(originalPath);
        if (originalRecords.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n[DAT Warning] Cannot read original structure, skipping: {originalPath}");
            Console.ResetColor();
            return;
        }

        using var fs = new FileStream(finalPath, FileMode.Create);
        using var writer = new BinaryWriter(fs);

        writer.Write((uint)originalRecords.Count);

        var valueBytesAll = new List<byte[]>();
        var keyBytesAll = new List<byte[]>();

        foreach (var record in originalRecords)
        {
            // UA: Підставляємо переклад якщо є; інакше — оригінальний текст
            // EN: Inject translation if available; otherwise keep original text
            string textOut = translationMap.TryGetValue(record.Key, out var translated) && !string.IsNullOrWhiteSpace(translated)
                ? translated + "\0"
                : record.OriginalValue + "\0";

            byte[] vB = Encoding.Unicode.GetBytes(textOut);
            byte[] kB = Encoding.ASCII.GetBytes(record.Key + "\0");

            // UA: CRC32 береться напряму з оригінального файлу — не перераховується!
            // EN: CRC32 taken directly from original file — not recomputed!
            writer.Write(record.Crc32);
            writer.Write((uint)(vB.Length / 2));
            writer.Write((uint)kB.Length);

            valueBytesAll.Add(vB);
            keyBytesAll.Add(kB);
        }

        foreach (var v in valueBytesAll) writer.Write(v);
        foreach (var k in keyBytesAll) writer.Write(k);
    }

    // UA: Зчитує структуру оригінального DAT (CRC32 + ключ + значення) у порядку з файлу.
    // EN: Reads original DAT structure (CRC32 + key + value) in file order.
    private List<OriginalDatRecord> ReadOriginalDatStructure(string path)
    {
        var records = new List<OriginalDatRecord>();
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            if (fs.Length < 4) return records;
            uint keyCount = reader.ReadUInt32();
            if (keyCount == 0 || keyCount > 500_000) return records;

            var index = new List<(uint Crc32, uint ValueLength, uint KeyLength)>((int)keyCount);
            for (int i = 0; i < keyCount; i++)
            {
                uint crc = reader.ReadUInt32();
                uint vl = reader.ReadUInt32();
                uint kl = reader.ReadUInt32();
                index.Add((crc, vl, kl));
            }

            long posValues = 4L + (long)keyCount * 12L;
            long posKeys = posValues + index.Sum(r => (long)r.ValueLength * 2);

            foreach (var (crc, vl, kl) in index)
            {
                fs.Seek(posValues, SeekOrigin.Begin);
                string value = Encoding.Unicode.GetString(reader.ReadBytes((int)(vl * 2))).TrimEnd('\0');
                posValues += vl * 2;

                fs.Seek(posKeys, SeekOrigin.Begin);
                string key = Encoding.ASCII.GetString(reader.ReadBytes((int)kl)).TrimEnd('\0');
                posKeys += kl;

                records.Add(new OriginalDatRecord(crc, key, value));
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[DAT ReadOriginal Error] {ex.Message}");
            Console.ResetColor();
        }
        return records;
    }

    private List<TextEntry> ReadReviewTsv(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8);
        using var csv = new CsvReader(reader, _csvConfig);
        return csv.GetRecords<TextEntry>().ToList();
    }

    // UA: Запис оригінального DAT-файлу (внутрішня структура). Використовуємо record для C# 10+
    // EN: Original DAT file record (internal structure). Using record for C# 10+
    private record OriginalDatRecord(uint Crc32, string Key, string OriginalValue);
}