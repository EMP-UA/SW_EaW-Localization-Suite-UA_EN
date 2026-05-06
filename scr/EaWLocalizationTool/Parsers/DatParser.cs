// ===================================================
//  Star Wars: EaW - Localization Tool
//  Created by EMP_UA
// ===================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using EaWLocalizationTool.Models;

namespace EaWLocalizationTool.Parsers
{
    /// <summary>
    /// UA: Парсер бінарних DAT файлів.
    /// EN: Parser for binary DAT files.
    /// </summary>
    public class DatParser
    {
        public List<TextEntry> Parse(string absolutePath, string relativePath)
        {
            var entries = new List<TextEntry>();
            try
            {
                using var fs = new FileStream(absolutePath, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(fs);

                if (fs.Length < 4) return entries;

                // UA: 1. Читаємо заголовок (Кількість записів)
                // EN: 1. Read header (Record count)
                uint keyCount = reader.ReadUInt32();
                if (keyCount == 0 || keyCount > 500000) return entries;

                // UA: 2. Читаємо таблицю індексів
                // EN: 2. Read index table
                var indexTable = new List<IndexRecord>();
                for (int i = 0; i < keyCount; i++)
                {
                    indexTable.Add(new IndexRecord
                    {
                        Crc32 = reader.ReadUInt32(),
                        ValueLength = reader.ReadUInt32(),
                        KeyLength = reader.ReadUInt32()
                    });
                }

                // UA: 3. Розраховуємо стартові позиції (як у PGDatTableHolder)
                // EN: 3. Calculate starting positions (like in PGDatTableHolder)
                long startingIndexValues = 4 + (keyCount * 12);
                long valueTableSize = 0;
                foreach (var record in indexTable)
                {
                    valueTableSize += record.ValueLength * 2; // UA/EN: sizeof(char) = 2
                }
                long startingIndexKeys = startingIndexValues + valueTableSize;

                // UA: 4. Читаємо дані
                // EN: 4. Read data
                for (int i = 0; i < keyCount; i++)
                {
                    var record = indexTable[i];

                    // UA: Читаємо ключ (ASCII) / EN: Read key (ASCII)
                    fs.Seek(startingIndexKeys, SeekOrigin.Begin);
                    byte[] keyBytes = reader.ReadBytes((int)record.KeyLength);
                    string key = Encoding.ASCII.GetString(keyBytes).TrimEnd('\0');
                    startingIndexKeys += record.KeyLength;

                    // UA: Читаємо значення (UTF-16 LE) / EN: Read value (UTF-16 LE)
                    fs.Seek(startingIndexValues, SeekOrigin.Begin);
                    byte[] valueBytes = reader.ReadBytes((int)record.ValueLength * 2);
                    string value = Encoding.Unicode.GetString(valueBytes).TrimEnd('\0');
                    startingIndexValues += record.ValueLength * 2;

                    entries.Add(new TextEntry
                    {
                        FilePath = relativePath,
                        FileType = "DAT",
                        Key = key,
                        OriginalText = value
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[DAT Error] {relativePath}: {ex.Message}");
            }
            return entries;
        }

        // UA: Допоміжна структура для зберігання індексів
        // EN: Helper structure for storing indexes
        private class IndexRecord
        {
            public uint Crc32 { get; set; }
            public uint ValueLength { get; set; }
            public uint KeyLength { get; set; }
        }
    }
}