// ===================================================
//  Star Wars: EaW - Localization Tool
//  Created by EMP_UA
// ============================================================
//  Translation/TranslationCache.cs
//  UA: Глобальний кеш перекладів між сесіями.
//      Ключ = нормалізований OriginalText.
//      Значення = TranslatedText.
//      Кожне влучення в кеш заощаджує один RPD-запит.
//  EN: Global translation cache across sessions.
//      Key = normalised OriginalText.
//      Value = TranslatedText.
//      Each cache hit saves one RPD request.
// ============================================================

using System.Text;
using System.Text.RegularExpressions;
using EaWLocalizer.Core;

namespace EaWLocalizer.Translation;

/// <summary>
/// UA: Singleton-кеш. Доступний як TranslationCache.Instance.
///     Thread-safe через lock (_lock).
///     При перевищенні MaxCacheEntries видаляє найстаріші 10% (LRU-like eviction).
/// EN: Singleton cache. Accessible as TranslationCache.Instance.
///     Thread-safe via lock (_lock).
///     When MaxCacheEntries is exceeded, removes the oldest 10% (LRU-like eviction).
/// </summary>
public class TranslationCache
{
    public static readonly TranslationCache Instance = new();
    TranslationCache() { }

    readonly object _lock = new();
    readonly Dictionary<string, string> _data = new(StringComparer.Ordinal);

    // UA: Нормалізуємо пробіли, щоб незначні різниці (подвійний пробіл,
    //     non-breaking space) не призводили до промахів кешу.
    // EN: Normalise whitespace so minor differences (double space,
    //     non-breaking space) don't cause cache misses.
    public string NormaliseKey(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace('\u00A0', ' ').Replace('\u2009', ' ');
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    /// <summary>
    /// UA: Спробує знайти переклад для src. Повертає true при влученні.
    /// EN: Tries to find a translation for src. Returns true on hit.
    /// </summary>
    public bool TryGet(string src, out string tr)
    {
        lock (_lock)
            return _data.TryGetValue(NormaliseKey(src), out tr!);
    }

    /// <summary>
    /// UA: Додає переклад до кешу. Якщо ключ вже існує — ігнорує (перша версія пріоритетна).
    /// EN: Adds a translation to the cache. If key exists — ignores (first version takes priority).
    /// </summary>
    public void Add(string src, string tr)
    {
        lock (_lock)
        {
            var key = NormaliseKey(src);
            if (_data.ContainsKey(key)) return;

            // UA: LRU eviction: видаляємо 10% найстаріших при переповненні.
            // EN: LRU eviction: remove oldest 10% on overflow.
            if (_data.Count >= Config.MaxCacheEntries)
                foreach (var k in _data.Keys.Take(Config.MaxCacheEntries / 10).ToList())
                    _data.Remove(k);

            _data[key] = tr;
        }
    }

    /// <summary>
    /// UA: Завантажує кеш із диску (base64 key|value, по одному на рядок).
    ///     Пошкоджені рядки мовчки пропускаються.
    /// EN: Loads the cache from disk (base64 key|value, one per line).
    ///     Malformed lines are silently skipped.
    /// </summary>
    public void Load()
    {
        if (!File.Exists(Config.GlobalCachePath)) return;
        try
        {
            lock (_lock)
            {
                foreach (var line in File.ReadAllLines(Config.GlobalCachePath))
                {
                    var p = line.Split('|', 2);
                    if (p.Length != 2) continue;
                    try
                    {
                        var key = NormaliseKey(
                            Encoding.UTF8.GetString(Convert.FromBase64String(p[0])));
                        var val = Encoding.UTF8.GetString(Convert.FromBase64String(p[1]));
                        if (!_data.ContainsKey(key)) _data[key] = val;
                    }
                    catch { /* skip malformed entry */ }
                }
            }
        }
        catch (Exception ex)
        {
            File.AppendAllText(Config.LogPath,
                $"[{DateTime.UtcNow:HH:mm:ss}] Cache.Load failed: {ex.Message}\n");
        }
    }

    /// <summary>
    /// UA: Зберігає кеш на диск. LRU eviction виконується перед записом.
    ///     Формат: base64(key)|base64(value) — стійкий до Unicode.
    /// EN: Saves the cache to disk. LRU eviction runs before writing.
    ///     Format: base64(key)|base64(value) — Unicode-safe.
    /// </summary>
    public void Save()
    {
        try
        {
            lock (_lock)
            {
                // UA: Повторна перевірка ліміту перед записом.
                // EN: Re-check limit before writing.
                if (_data.Count >= Config.MaxCacheEntries)
                    foreach (var k in _data.Keys.Take(Config.MaxCacheEntries / 10).ToList())
                        _data.Remove(k);

                File.WriteAllLines(Config.GlobalCachePath,
                    _data.Select(kv =>
                        $"{Convert.ToBase64String(Encoding.UTF8.GetBytes(kv.Key))}" +
                        $"|{Convert.ToBase64String(Encoding.UTF8.GetBytes(kv.Value ?? ""))}"));
            }
        }
        catch (Exception ex)
        {
            File.AppendAllText(Config.LogPath,
                $"[{DateTime.UtcNow:HH:mm:ss}] Cache.Save failed: {ex.Message}\n");
        }
    }

    /// <summary>UA: Поточна кількість записів у кеші. EN: Current number of cache entries.</summary>
    public int Count { get { lock (_lock) return _data.Count; } }
}
