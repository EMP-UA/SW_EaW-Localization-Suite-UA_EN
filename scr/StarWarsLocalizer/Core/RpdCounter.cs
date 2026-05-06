// ===================================================
//  Star Wars: EaW - Localization Tool
//  Created by EMP_UA
// ===================================================

using System;
using System.IO;
using System.Threading;

namespace EaWLocalizer.Core;

/// <summary>
/// UA: Статичний лічильник RPD (Requests Per Day).
///     Total = запити минулих сесій сьогодні + запити поточної сесії.
///     Increment() викликається лише з RateLimiter.ThrottleAsync().
/// EN: Static RPD (Requests Per Day) counter.
///     Total = previous sessions today + current session requests.
///     Increment() is called only from RateLimiter.ThrottleAsync().
/// </summary>
public static class RpdCounter
{
    // UA: Запити із попередніх сесій сьогодні (завантажуються з диску).
    // EN: Requests from previous sessions today (loaded from disk).
    static int _loadedToday = 0;

    // UA: Запити поточної сесії (скидається при кожному запуску).
    // EN: Requests in the current session (reset on each launch).
    static int _sessionCount = 0;

    /// <summary>UA: Запити поточної сесії. EN: Current session requests.</summary>
    public static int Session => _sessionCount;

    /// <summary>UA: Загальна кількість сьогодні. EN: Total requests today.</summary>
    public static int Total => _loadedToday + _sessionCount;

    /// <summary>
    /// UA: Атомарно збільшує лічильник сесії на 1.
    ///     Викликається лише всередині RateLimiter після успішного throttle.
    /// EN: Atomically increments the session counter by 1.
    ///     Called only inside RateLimiter after a successful throttle.
    /// </summary>
    public static void Increment() => Interlocked.Increment(ref _sessionCount);

    /// <summary>
    /// UA: Повертає "epoch" поточного вікна квоти API як рядок для збереження.
    ///     Gemini скидає ліміти о 00:00 PST = 08:00 UTC.
    ///     Ми зберігаємо не просто дату, а "UTC timestamp останнього скиду"
    ///     у форматі "yyyy-MM-dd HH" — тобто дату + годину початку вікна.
    ///     Це усуває двозначність: о 07:30 UTC поточне вікно почалось
    ///     "вчора о 08:00", а о 09:00 UTC — "сьогодні о 08:00".
    /// EN: Returns the current API quota window "epoch" as a string for storage.
    ///     Gemini resets limits at 00:00 PST = 08:00 UTC.
    ///     We store not just a date but the "UTC timestamp of the last reset"
    ///     as "yyyy-MM-dd HH" — date + reset hour.
    ///     This removes ambiguity: at 07:30 UTC the current window started
    ///     "yesterday at 08:00", at 09:00 UTC — "today at 08:00".
    /// </summary>
    static string GetQuotaWindowKey()
    {
        const int ResetHourUtc = 8; // 00:00 PST = 08:00 UTC
        var now = DateTime.UtcNow;
        
        // UA: Якщо до скиду — вікно почалось вчора о 08:00 UTC.
        //     Якщо після скиду — вікно почалось сьогодні о 08:00 UTC.
        // EN: Before reset — window started yesterday at 08:00 UTC.
        //     After reset — window started today at 08:00 UTC.
        var windowStart = now.Hour < ResetHourUtc
            ? now.Date.AddDays(-1)
            : now.Date;
            
        return windowStart.ToString("yyyy-MM-dd");
    }

    public static void Load()
    {
        if (!File.Exists(Config.RpdCounterPath)) return;
        try
        {
            var lines = File.ReadAllLines(Config.RpdCounterPath);
            
            // UA: Формат файлу: рядок 1 = ключ вікна квоти, рядок 2 = лічильник.
            //     Якщо ключ не збігається → інше вікно квоти → скидаємо до 0.
            // EN: File format: line 1 = quota window key, line 2 = counter.
            //     If key doesn't match → different quota window → reset to 0.
            if (lines.Length >= 2
                && lines[0].Trim() == GetQuotaWindowKey()
                && int.TryParse(lines[1], out int n))
            {
                _loadedToday = Math.Max(0, n);
            }
        }
        catch { /* non-fatal — start from zero */ }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Config.RpdCounterPath)!);
            File.WriteAllLines(Config.RpdCounterPath, new[]
            {
                GetQuotaWindowKey(),
                Total.ToString()
            });
        }
        catch { /* non-fatal */ }
    }
}