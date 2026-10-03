// =============================================================================
// EaWLocalizationTool.GUI — AppFolders.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Структура робочих тек поруч із програмою. Усі теки створюються під час
//     запуску, якщо їх немає; нічого не пишеться в теки гри чи мода.
//
//       original\    оригінальні (англійські) DAT гри
//       translated\  збережені перекладені DAT
//       work\        робочі TSV зі статусами вичитки та автозбереження
//       transfer\    звіти перенесення перекладу
//
//     Першу пару тек (original, translated) дзеркалить структуру гри, тож вміст
//     копіюється з гри й у гру без зміни шляхів:
//       GameData\Data\Text\     основна гра  (Star Wars Empire at War\GameData\Data\Text\)
//       corruption\Data\Text\   доповнення   (Star Wars Empire at War\corruption\Data\Text\)
//     work має лише підтеки GameData, corruption. Файл, який не вдалося віднести
//     до жодної гри, потрапляє в підтеку other.
//     Обидва DAT у грі називаються mastertextfile_english.dat, тому підтеки
//     потрібні, щоб файли основної гри й доповнення не змішувалися.
// EN: Folder layout next to the application. All folders are created at startup
//     when missing; nothing is written into the game or mod folders.
//
//       original\    the game's original (English) DATs
//       translated\  saved translated DATs
//       work\        working TSVs with review statuses, and autosaves
//       transfer\    translation transfer reports
//
//     The first pair (original, translated) mirrors the game's structure, so
//     content is copied from and into the game without changing paths:
//       GameData\Data\Text\     base game   (Star Wars Empire at War\GameData\Data\Text\)
//       corruption\Data\Text\   expansion   (Star Wars Empire at War\corruption\Data\Text\)
//     work has only the GameData and corruption subfolders. A file that fits
//     neither game goes to the other subfolder.
//     Both DATs are named mastertextfile_english.dat in the game, so the
//     subfolders keep the base game's and the expansion's files apart.
// =============================================================================

using System.IO;

namespace EaWLocalizationTool.GUI.Services;

/// <summary>
/// UA: Гра, до якої належить DAT.
/// EN: The game a DAT belongs to.
/// </summary>
public enum GameKind { Base, Corruption, Other }

public static class AppFolders
{
    public static string Root => AppContext.BaseDirectory;

    public static string OriginalRoot => Path.Combine(Root, "original");
    public static string TranslatedRoot => Path.Combine(Root, "translated");
    public static string WorkRoot => Path.Combine(Root, "work");
    public static string TransferRoot => Path.Combine(Root, "transfer");

    /// <summary>
    /// UA: Ім'я підтеки для гри.
    /// EN: Subfolder name for a game.
    /// </summary>
    public static string SubfolderName(GameKind kind) => kind switch
    {
        GameKind.Base => "GameData",
        GameKind.Corruption => "corruption",
        _ => "other"
    };

    public static string Original(GameKind kind) => GameTree(OriginalRoot, kind);
    public static string Translated(GameKind kind) => GameTree(TranslatedRoot, kind);
    public static string Work(GameKind kind) => Path.Combine(WorkRoot, SubfolderName(kind));

    // UA: Підтека гри; для основної гри та доповнення — з продовженням Data\Text, як у грі.
    // EN: A game's subfolder; for the base game and the expansion it continues with Data\Text, as in the game.
    private static string GameTree(string root, GameKind kind) =>
        kind == GameKind.Other
            ? Path.Combine(root, SubfolderName(kind))
            : Path.Combine(root, SubfolderName(kind), "Data", "Text");

    /// <summary>
    /// UA: Створює всі теки, яких бракує. Помилка створення не критична: програма
    ///     створить теку ще раз під час запису.
    /// EN: Creates every missing folder. A creation failure is not critical: the
    ///     application creates the folder again when it writes.
    /// </summary>
    public static void EnsureCreated()
    {
        try
        {
            foreach (var kind in new[] { GameKind.Base, GameKind.Corruption })
            {
                Directory.CreateDirectory(Original(kind));
                Directory.CreateDirectory(Translated(kind));
                Directory.CreateDirectory(Work(kind));
            }
            Directory.CreateDirectory(TransferRoot);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "AppFolders.EnsureCreated");
        }
    }

    /// <summary>
    /// UA: Визначає гру за шляхом до файлу: сегмент «corruption» — доповнення,
    ///     сегмент «GameData» — основна гра (так само і в теках цієї програми).
    ///     Інакше — Other.
    /// EN: Determines the game from a file path: a "corruption" segment means the
    ///     expansion, a "GameData" segment means the base game (this also holds
    ///     for the application's own folders). Otherwise — Other.
    /// </summary>
    public static GameKind Detect(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return GameKind.Other;

        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Any(s => s.Equals("corruption", StringComparison.OrdinalIgnoreCase)))
            return GameKind.Corruption;
        if (segments.Any(s => s.Equals("GameData", StringComparison.OrdinalIgnoreCase)))
            return GameKind.Base;

        return GameKind.Other;
    }

    /// <summary>
    /// UA: Мітка часу для імен файлів: «yyMMdd HHmm» (напр. «261003 1234»).
    /// EN: Timestamp for file names: "yyMMdd HHmm" (e.g. "261003 1234").
    /// </summary>
    public static string Stamp() => DateTime.Now.ToString("yyMMdd HHmm");

    /// <summary>
    /// UA: Найновіший файл за маскою в теці (null, якщо теки чи файлів немає).
    /// EN: The newest file matching a pattern in a folder (null when the folder or files are missing).
    /// </summary>
    public static string? LatestFile(string folder, string pattern)
    {
        try
        {
            if (!Directory.Exists(folder)) return null;
            return new DirectoryInfo(folder)
                .EnumerateFiles(pattern, SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => f.FullName)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}
