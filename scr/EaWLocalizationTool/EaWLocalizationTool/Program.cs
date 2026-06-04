// ===================================================
//  Star Wars: EaW - Localization Tool
//  Created by EMP_UA
// ===================================================

using System;
using System.Text;
using EaWLocalizationTool.Core;
using EaWLocalizationTool.UI;

namespace EaWLocalizationTool;

/// <summary>
/// UA: Головний клас програми. Керує життєвим циклом локалізації.
/// EN: Main application class. Manages the localization lifecycle.
/// </summary>
internal class Program
{
    static void Main(string[] args)
    {
        // UA: Реєстрація кодувань для підтримки ANSI / EN: Register encodings for ANSI support
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        // UA: Вибір мови інтерфейсу / EN: Interface language selection
        Console.WriteLine("Select language / Оберіть мову:");
        Console.WriteLine("1. English");
        Console.WriteLine("2. Українська");
        Console.Write("> ");
        Localizer.UseUkrainian = Console.ReadLine() == "2";

        // UA: Ініціалізуємо робочі папки / EN: Initialize working directories
        PathManager.InitializeDirectories();

        while (true)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(Localizer.MenuHeader);
            Console.ResetColor();
            
            // UA: Використовуємо оновлену змінну з Localizer
            // EN: Using the updated variable from Localizer
            Console.WriteLine(Localizer.InfoPaths);
            Console.WriteLine();

            Console.WriteLine(Localizer.MenuStep1);
            Console.WriteLine(Localizer.MenuStep2);
            Console.WriteLine(Localizer.MenuStep3);
            Console.WriteLine(Localizer.MenuDebug);
            Console.WriteLine(Localizer.MenuExit);
            Console.Write($"\n{Localizer.SelectOption}");

            string? choice = Console.ReadLine();
            try
            {
                bool operationCompleted = false;
                switch (choice)
                {
                    case "1":
                        new FileProcessor().ExtractOriginals(PathManager.OriginalDir, PathManager.BeforeApiDir);
                        operationCompleted = true;
                        break;
                    case "2":
                        new ReviewBuilder().BuildReviewFiles(PathManager.BeforeApiDir, PathManager.AfterApiDir, PathManager.ReviewDir);
                        operationCompleted = true;
                        break;
                    case "3":
                        new Repacker().RepackAll(PathManager.ReviewDir, PathManager.OriginalDir, PathManager.FinalDir);
                        operationCompleted = true;
                        break;
                    case "4":
                        new DebugProcessor().RunRoundTripTest(PathManager.OriginalDir, PathManager.BeforeApiDir, PathManager.ReviewDir, PathManager.DebugFinalDir);
                        operationCompleted = true;
                        break;
                    case "0":
                        return;
                }

                if (operationCompleted)
                {
                    Console.WriteLine($"\n{Localizer.OpCompleted}");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n{Localizer.Error}: {ex.Message}");
                Console.WriteLine(ex.StackTrace); // UA: Додаємо стек для легшого дебагу / EN: Add stack trace for easier debugging
                Console.ResetColor();
            }
            Console.WriteLine(Localizer.PressAnyKey);
            Console.ReadKey();
        }
    }
}