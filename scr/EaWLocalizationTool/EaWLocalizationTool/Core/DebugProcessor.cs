// ===================================================
//  Star Wars: EaW - Localization Tool
//  Created by EMP_UA
// ===================================================

using System;
using EaWLocalizationTool.UI;

namespace EaWLocalizationTool.Core;

/// <summary>
/// UA: Клас для тестування повного циклу обробки.
/// EN: Class for testing the full processing cycle.
/// </summary>
public class DebugProcessor
{
    public void RunRoundTripTest(string og, string beforeDir, string reviewDir, string debugFinalDir)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine(Localizer.Get(">>> ЗАПУСК ПОВНОГО ТЕСТУ СУМІСНОСТІ (ROUND-TRIP)", ">>> STARTING FULL COMPATIBILITY TEST (ROUND-TRIP)"));
        Console.ResetColor();
        Console.WriteLine("================================================================================");

        // ЕТАП 1 / STAGE 1
        Console.WriteLine(Localizer.Get("\n[1/3] Експорт оригіналів у таблиці TSV...", "\n[1/3] Exporting originals to TSV tables..."));
        var (xml, dat, txt) = new FileProcessor().ExtractOriginals(og, beforeDir);
        Console.WriteLine(Localizer.Get($"\n   Готово! Знайдено: XML: {xml}, DAT: {dat}, TXT: {txt}", $"\n   Done! Found: XML: {xml}, DAT: {dat}, TXT: {txt}"));

        // ЕТАП 2 / STAGE 2
        Console.WriteLine(Localizer.Get("\n[2/3] Підготовка Review та групування TXT...", "\n[2/3] Review preparation and TXT grouping..."));
        int groupedTxt = new ReviewBuilder().BuildReviewFiles(beforeDir, beforeDir, reviewDir);
        Console.WriteLine(Localizer.Get($"\n   Готово! {groupedTxt} TXT файлів об'єднано в !MASTER_REVIEW_TXT.tsv", $"\n   Done! {groupedTxt} TXT files merged into !MASTER_REVIEW_TXT.tsv"));

        // ЕТАП 3 / STAGE 3
        Console.WriteLine(Localizer.Get("\n[3/3] Реконструкція ігрових файлів з таблиць...", "\n[3/3] Reconstructing game files from tables..."));
        var (reconstructed, copied) = new Repacker().RepackAll(reviewDir, og, debugFinalDir);

        // ФІНАЛЬНИЙ ЗВІТ / FINAL REPORT
        Console.WriteLine("\n================================================================================");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(Localizer.Get(">>> ТЕСТ ЗАВЕРШЕНО УСПІШНО!", ">>> TEST COMPLETED SUCCESSFULLY!"));
        Console.ResetColor();

        string summaryUa = $"   Статистика:\n   - Відтворено з таблиць: {reconstructed}\n   - Скопійовано (технічні): {copied}\n   - Всього файлів: {reconstructed + copied}";
        string summaryEn = $"   Statistics:\n   - Reconstructed from tables: {reconstructed}\n   - Copied (technical): {copied}\n   - Total files: {reconstructed + copied}";

        Console.WriteLine(Localizer.Get(summaryUa, summaryEn));
        Console.WriteLine("================================================================================");
    }
}