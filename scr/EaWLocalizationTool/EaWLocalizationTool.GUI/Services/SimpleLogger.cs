using System;
using System.IO;

namespace EaWLocalizationTool.GUI.Services;

/// <summary>
/// UA: Простий потокобезпечний логер для запису помилок та подій.
///     Автоматично створює папку logs поруч із виконуваним файлом.
/// EN: Simple thread-safe logger for recording errors and events.
///     Automatically creates logs folder next to the executable.
/// </summary>
public static class SimpleLogger
{
    private static readonly string LogPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "logs", "app.log");

    private static readonly object _lock = new();

    public static void Log(string message, string level = "INFO")
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(LogPath, logEntry);
            }
        }
        catch { }
    }

    public static void LogError(Exception ex, string context = "")
    {
        Log($"{context} | {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", "ERROR");
    }
}