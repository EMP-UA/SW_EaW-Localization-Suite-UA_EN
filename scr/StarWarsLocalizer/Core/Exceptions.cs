// ===================================================
//  Star Wars: EaW - Localization Tool
//  Created by EMP_UA
// ============================================================
//  Core/Exceptions.cs
//  UA: Власні виключення проєкту.
//  EN: Project-specific exceptions.
// ============================================================

namespace EaWLocalizer.Core;

/// <summary>
/// UA: Кидається коли вичерпано добовий ліміт запитів до API (RPD).
///     Перехоплюється у TsvPipeline.RunAsync() та коректно
///     завершує сесію зі збереженням прогресу.
/// EN: Thrown when the daily API request quota (RPD) is exhausted.
///     Caught in TsvPipeline.RunAsync() to cleanly end the session
///     while saving all progress.
/// </summary>
public class RpdLimitReachedException : Exception
{
    public int Used { get; }
    public int Max  { get; }

    public RpdLimitReachedException(int used, int max)
        : base($"Daily RPD limit reached: {used}/{max}. Restart tomorrow (UTC).")
    {
        Used = used;
        Max  = max;
    }
}
