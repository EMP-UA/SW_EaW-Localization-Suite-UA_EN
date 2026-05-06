// ===================================================
//  Star Wars: EaW - Localization Tool
//  Created by EMP_UA
// ===================================================

using System.Diagnostics;
using EaWLocalizer.UI;

namespace EaWLocalizer.Core;

public class RateLimiter
{
    public static readonly RateLimiter Instance = new();
    private RateLimiter() { }

    readonly Stopwatch _sw = Stopwatch.StartNew();
    readonly Queue<TimeSpan> _times = new();
    readonly TimeSpan _window = TimeSpan.FromMinutes(1);
    readonly SemaphoreSlim _sem = new(1, 1);

    long _nextAllowedTicks = 0;

    public void ApplyBackoff(TimeSpan duration)
    {
        long target = (_sw.Elapsed + duration).Ticks;
        long current = Interlocked.Read(ref _nextAllowedTicks);
        while (target > current)
        {
            long prev = Interlocked.CompareExchange(ref _nextAllowedTicks, target, current);
            if (prev == current) break;
            current = prev;
        }
    }

    public double GetCurrentRPM()
    {
        var now = _sw.Elapsed;
        lock (_times) { return _times.Count(t => now - t <= _window); }
    }

    public async Task ThrottleAsync()
    {
        if (RpdCounter.Total >= Config.MaxRpd)
            throw new RpdLimitReachedException(RpdCounter.Total, Config.MaxRpd);

        await _sem.WaitAsync();
        try
        {
            var now = _sw.Elapsed;

            // UA: 1. Глобальна затримка (Backoff)
            // EN: 1. Global Backoff
            var nextAllowed = TimeSpan.FromTicks(Interlocked.Read(ref _nextAllowedTicks));
            if (now < nextAllowed) { await Task.Delay(nextAllowed - now); now = _sw.Elapsed; }

            // UA: 2. Ковзне вікно
            // EN: 2. Sliding Window
            lock (_times)
            {
                while (_times.Count > 0 && now - _times.Peek() > _window) _times.Dequeue();
                if (_times.Count >= (int)(Config.DesiredRpm * Config.SafetyFactor))
                {
                    var wait = _window - (now - _times.Peek()) + TimeSpan.FromMilliseconds(200);
                    
                    // UA: Блокуюче очікування всередині семафора тут допустиме
                    // EN: Blocking wait inside semaphore is okay here
                    Task.Delay(wait).Wait(); 
                    now = _sw.Elapsed;
                }
                _times.Enqueue(now);
            }

            RpdCounter.Increment();

            // UA: 3. Екстрена перевірка
            // EN: 3. Emergency Check
            if (GetCurrentRPM() >= Config.EmergencyRpmCeiling && Config.SelectedTier == ModelTier.FreeLite)
            {
                ApplyBackoff(Config.EmergencyBackoff);
            }
        }
        finally { _sem.Release(); }
    }
}