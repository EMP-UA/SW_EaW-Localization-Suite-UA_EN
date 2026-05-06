using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MegExtractor
{
    class Program
    {
        // Structure to store information about each file in the archive 
        // Структура для зберігання інформації про кожен файл в архіві
        struct FileInfoEntry
        {
            public uint Crc;
            public uint Index;
            public uint Size;
            public uint Offset;
            public uint NameIndex;
        }

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            Console.WriteLine("=================================================");
            Console.WriteLine(" MEG File Extractor (Star Wars: Empire at War)");
            Console.WriteLine(" Created by EMP_UA");
            Console.WriteLine("=================================================\n");

            string megPath = "";

            // Support Drag-and-Drop / Підтримка Drag-and-Drop (можна перетягнути файл на .exe)
            if (args.Length > 0)
            {
                megPath = args[0];
            }
            else
            {
                Console.Write("Enter the full path to the .meg file (or drag and drop it here):\nВведіть повний шлях до файлу .meg (або перетягніть його сюди): ");
                megPath = Console.ReadLine().Trim('"'); // Remove quotes if any / Прибираємо лапки, якщо є
            }

            if (!File.Exists(megPath))
            {
                Console.WriteLine("\n[ERROR] File not found! Check the path.");
                Console.WriteLine("[ПОМИЛКА] Файл не знайдено! Перевірте шлях.");
                Console.ReadLine();
                return;
            }

            // Create an output directory next to the .meg file / Створюємо папку для розпакування поруч із .meg файлом
            string outputDir = Path.Combine(Path.GetDirectoryName(megPath), Path.GetFileNameWithoutExtension(megPath) + "_extracted");

            try
            {
                ExtractMeg(megPath, outputDir);
                Console.WriteLine("\n[SUCCESS] All files have been extracted to:");
                Console.WriteLine("[УСПІХ] Всі файли розпаковано у папку:");
                Console.WriteLine(outputDir);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERROR] An error occurred during extraction / Виникла помилка під час розпакування: {ex.Message}");
            }

            Console.WriteLine("\nPress Enter to exit / Натисніть Enter для виходу...");
            Console.ReadLine();
        }

        static void ExtractMeg(string megPath, string outputDir)
        {
            using (FileStream fs = new FileStream(megPath, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs, Encoding.ASCII))
            {
                // 1. Read Header / Читаємо заголовок
                uint numFileNames = br.ReadUInt32();
                uint numFiles = br.ReadUInt32();

                Console.WriteLine($"\nFiles found in archive / Знайдено файлів у архіві: {numFiles}");
                Console.WriteLine("Extracting... / Розпакування...\n");

                // 2. Read Filename Table / Читаємо таблицю імен
                List<string> fileNames = new List<string>((int)numFileNames);
                for (int i = 0; i < numFileNames; i++)
                {
                    ushort nameLen = br.ReadUInt16();
                    byte[] nameBytes = br.ReadBytes(nameLen);
                    string name = Encoding.ASCII.GetString(nameBytes);
                    fileNames.Add(name);
                }

                // 3. Read File Info Table / Читаємо таблицю інформації про файли
                List<FileInfoEntry> entries = new List<FileInfoEntry>((int)numFiles);
                for (int i = 0; i < numFiles; i++)
                {
                    FileInfoEntry entry = new FileInfoEntry
                    {
                        Crc = br.ReadUInt32(),
                        Index = br.ReadUInt32(),
                        Size = br.ReadUInt32(),
                        Offset = br.ReadUInt32(),
                        NameIndex = br.ReadUInt32()
                    };
                    entries.Add(entry);
                }

                // 4. Extract Data / Витягуємо безпосередньо дані
                foreach (var entry in entries)
                {
                    // Get filename by index / Отримуємо ім'я файлу за індексом
                    string rawName = fileNames[(int)entry.NameIndex];

                    // Normalize paths for Windows / Нормалізуємо шляхи для Windows (міняємо / на \ і прибираємо початковий слеш)
                    string cleanName = rawName.Replace('/', '\\').TrimStart('\\');
                    string outPath = Path.Combine(outputDir, cleanName);

                    // Create subdirectories / Створюємо всі необхідні підпапки
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath));

                    // Go to file offset and read data / Переходимо до місця, де лежить файл, і читаємо його
                    fs.Seek(entry.Offset, SeekOrigin.Begin);
                    byte[] data = br.ReadBytes((int)entry.Size);

                    // Save to disk / Зберігаємо на диск
                    File.WriteAllBytes(outPath, data);
                    Console.WriteLine($"Extracted / Розпаковано: {cleanName}");
                }
            }
        }
    }
}