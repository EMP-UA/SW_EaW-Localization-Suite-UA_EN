// ===================================================
//  Star Wars: EaW - Localization Tool
//  Created by EMP_UA
// ===================================================

using System;
using System.IO;
using System.Text;
using EaWLocalizer.Core;
using EaWLocalizer.Translation;
using EaWLocalizer.UI;

namespace EaWLocalizer;

/// <summary>
/// UA: Точка входу. Повний цикл: Мова -> Модель -> Скан -> Ключ -> Переклад.
/// EN: Entry point. Full cycle: Language -> Model -> Scan -> Key -> Translate.
/// </summary>
internal class Program
{
    static async Task Main(string[] args)
    {
        // UA: Ініціалізація кодувань. EN: Encoding initialization.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        // ── 1. LANGUAGE & MODEL SELECTION ──
        Locale.SelectLanguage();
        var tier = Locale.SelectModelTier();
        Config.ApplyProfile(tier);
        Console.Title = Locale.S("header");

        // ── 2. DIRECTORIES & PIPELINE ──
        // UA: Папки створюються автоматично на основі Config.WorkspaceDir
        // EN: Directories are created automatically based on Config.WorkspaceDir
        Directory.CreateDirectory(Config.AfterApiDir);
        Directory.CreateDirectory(Config.ReviewDir);

        foreach (var f in new[] { Config.LogPath, Config.RpmLogPath })
            if (File.Exists(f)) File.Delete(f);

        var pipeline = new TsvPipeline();

        // ── 3. LOAD STATE ──
        TranslationCache.Instance.Load();
        RpdCounter.Load();
        pipeline.LoadState();

        // UA: Безпечне завершення (Ctrl+C). EN: Safe exit (Ctrl+C).
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n{Locale.S("interrupted")}");
            TranslationCache.Instance.Save();
            RpdCounter.Save();
            pipeline.SaveState();
            Console.WriteLine(Locale.S("state_saved"));
            Environment.Exit(0);
        };

        // ── 4. HEADER ──
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine($"   {Locale.S("header")}");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(string.Format(Locale.S("status_model"), Config.Model, Config.BatchSize));
        Console.WriteLine($"  BeforeAPI  : {Config.BeforeApiDir}");
        Console.WriteLine(string.Format(Locale.S("rpd_status"), RpdCounter.Total, Config.MaxRpd, Math.Max(0, Config.MaxRpd - RpdCounter.Total)));
        Console.ResetColor();

        // ── 5. PHASE 1: SCAN ──
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  {Locale.S("scan_phase")}");
        Console.ResetColor();

        var scan = pipeline.ScanAsync();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        // UA: Вивід статистики сканування
        // EN: Displaying scan statistics
        Console.WriteLine(string.Format(Locale.S("scan_summary"), 
            scan.TotalFiles, scan.CacheHits, scan.Technical, scan.UniqueStringsForApi, scan.FilesNeedingApi));
        Console.ResetColor();

        // ── 6. API KEY & PHASE 2 ──
        if (scan.NeedsApi)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"\n{Locale.S("ask_api_key")}");
            Console.ResetColor();

            var keyBuilder = new StringBuilder();
            while (true)
            {
                var k = Console.ReadKey(intercept: true);
                if (k.Key == ConsoleKey.Enter) break;
                if (k.Key == ConsoleKey.Backspace && keyBuilder.Length > 0)
                {
                    keyBuilder.Remove(keyBuilder.Length - 1, 1);
                    Console.Write("\b \b");
                }
                else if (!char.IsControl(k.KeyChar))
                {
                    keyBuilder.Append(k.KeyChar);
                    Console.Write('*');
                }
            }
            Console.WriteLine();

            string apiKey = keyBuilder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine(Locale.S("no_key"));
                return;
            }
            GeminiClient.Instance.ApiKey = apiKey;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n  {Locale.S("phase_translate")}");
            Console.ResetColor();

            await pipeline.TranslateAsync(scan);
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  {Locale.S("all_done_no_api")}");
            Console.ResetColor();
        }

        // ── 7. FINAL SAVE ──
        TranslationCache.Instance.Save();
        RpdCounter.Save();
        pipeline.SaveState();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n{Locale.S("session_done")}");
        Console.ResetColor();
        
        // UA: Звуковий сигнал завершення
        // EN: Completion sound signal
        Console.Beep(800, 300);
        Console.Beep(1000, 300);
    }
}