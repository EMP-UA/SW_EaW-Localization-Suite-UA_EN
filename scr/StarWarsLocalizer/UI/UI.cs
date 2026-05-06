// ===================================================
//  Star Wars: EaW - Localization Tool
//  Created by EMP_UA
// ===================================================

namespace EaWLocalizer.UI;

public static class Locale
{
    public static AppLanguage Lang { get; private set; } = AppLanguage.UA;

    public static void SelectLanguage()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("┌──────────────────────────────────────────────┐");
        Console.WriteLine("│   Оберіть мову / Select interface language    │");
        Console.WriteLine("│   [1]  Українська (UA)                        │");
        Console.WriteLine("│   [2]  English    (EN)                        │");
        Console.WriteLine("└──────────────────────────────────────────────┘");
        Console.ResetColor();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.KeyChar == '1') { Lang = AppLanguage.UA; break; }
            if (key.KeyChar == '2') { Lang = AppLanguage.EN; break; }
        }
    }

    public static Core.ModelTier SelectModelTier()
    {
        Console.WriteLine(S("select_model_title"));
        Console.WriteLine("  [1] Gemini 3.1 Flash Lite (Free)");
        // UA: Відображаємо актуальну версію 2.5 Flash
        // EN: Displaying current 2.5 Flash version
        Console.WriteLine("  [2] Gemini 2.5 Flash (Paid Tier 1)");
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.KeyChar == '1') return Core.ModelTier.FreeLite;
            if (key.KeyChar == '2') return Core.ModelTier.PaidFlash;
        }
    }

    public static string S(string key) => _strings.TryGetValue(key, out var p) ? (Lang == AppLanguage.UA ? p[0] : p[1]) : $"[{key}]";

    static readonly Dictionary<string, string[]> _strings = new()
    {
        ["header"] = new[] { "Star Wars: EaW ЛОКАЛІЗАТОР v1.1", "Star Wars: EaW LOCALIZER v1.1" },
        ["select_model_title"] = new[] { "\nОберіть модель API:", "\nSelect API model:" },
        ["ask_api_key"] = new[] { "Введіть API KEY: ", "Enter API KEY: " },
        ["scan_phase"] = new[] { "Фаза 1: Сканування файлів...", "Phase 1: Scanning files..." },
        ["phase_translate"] = new[] { "Фаза 2: Переклад через API...", "Phase 2: API Translation..." },
        ["all_done_no_api"] = new[] { "API не потрібен, все готово.", "No API needed, all done." },
        ["session_done"] = new[] { "Завершено!", "Complete!" },
        ["status_model"] = new[] { "  Модель: {0} | Батч: {1}", "  Model: {0} | Batch: {1}" },
        ["scan_summary"] = new[] { "  Файлів: {0} | Кеш: {1} | До API: {3}", "  Files: {0} | Cache: {1} | For API: {3}" },
        ["file_header"] = new[] { "[{0}/{1}] {2}", "[{0}/{1}] {2}" },
        ["batch_status"] = new[] { "  Пакет {0}/{1} | RPM: {2:F1}", "  Batch {0}/{1} | RPM: {2:F1}" },
        ["no_key"] = new[] { "Ключ відсутній!", "Key missing!" },
        ["interrupted"] = new[] { "Перервано!", "Interrupted!" },
        ["state_saved"] = new[] { "Стан збережено.", "State saved." },
        ["rpd_status"] = new[] { "  RPD: {0}/{1} (Залишок: {2})", "  RPD: {0}/{1} (Left: {2})" },
        ["cache_loaded"] = new[] { "  Кеш: {0} записів.", "  Cache: {0} entries." }
    };
}