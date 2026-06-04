// ===================================================
//  Star Wars: EaW - Localization Tool
//  Created by EMP_UA (Silence will fall)
// ===================================================

using System;
using System.IO;

namespace EaWLocalizationTool.Core;

/// <summary>
/// UA: Централізоване керування шляхами до робочих директорій.
/// EN: Centralized management of paths to working directories.
/// </summary>
public static class PathManager
{
    // UA: Шляхи тепер відносні до папки, з якої запущено програму.
    // EN: Paths are now relative to the folder where the application is launched.
    private static readonly string BasePath = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly string WorkspaceDir = Path.Combine(BasePath, "Workspace");

    /// <summary>
    /// UA: Шлях до папки з оригінальними файлами гри (OG).
    /// EN: Path to the folder with original game files (OG).
    /// </summary>
    public static readonly string OriginalDir = Path.Combine(WorkspaceDir, "OG");

    /// <summary>
    /// UA: Шлях до папки для експортованих таблиць перед перекладом.
    /// EN: Path to the folder for exported tables before translation.
    /// </summary>
    public static readonly string BeforeApiDir = Path.Combine(WorkspaceDir, "BeforeAPI");

    /// <summary>
    /// UA: Шлях до папки з таблицями після перекладу (напр. через API).
    /// EN: Path to the folder with tables after translation (e.g., via API).
    /// </summary>
    public static readonly string AfterApiDir = Path.Combine(WorkspaceDir, "AfterAPI");

    /// <summary>
    /// UA: Шлях до папки для файлів огляду (зіставлення оригіналу та перекладу).
    /// EN: Path to the folder for review files (matching original and translation).
    /// </summary>
    public static readonly string ReviewDir = Path.Combine(WorkspaceDir, "Review");

    /// <summary>
    /// UA: Шлях до папки для фінальних, перепакованих файлів гри.
    /// EN: Path to the folder for the final, repacked game files.
    /// </summary>
    public static readonly string FinalDir = Path.Combine(WorkspaceDir, "Final");

    /// <summary>
    /// UA: Шлях до папки для файлів, створених під час дебаг-тесту.
    /// EN: Path to the folder for files created during the debug test.
    /// </summary>
    public static readonly string DebugFinalDir = Path.Combine(WorkspaceDir, "DebugFinal");

    /// <summary>
    /// UA: Метод для ініціалізації базової структури папок.
    /// EN: Method to initialize the basic folder structure.
    /// </summary>
    public static void InitializeDirectories()
    {
        Directory.CreateDirectory(OriginalDir);
        Directory.CreateDirectory(BeforeApiDir);
        Directory.CreateDirectory(AfterApiDir);
        Directory.CreateDirectory(ReviewDir);
        Directory.CreateDirectory(FinalDir);
        Directory.CreateDirectory(DebugFinalDir);
    }
}