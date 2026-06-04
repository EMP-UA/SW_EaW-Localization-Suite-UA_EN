using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace EaWLocalizationTool.GUI;

/// <summary>
/// UA: Менеджер тем та розміру шрифту. Зберігає налаштування у JSON.
///
///     ШРИФТИ:
///     • FontBase / FontSm / FontTiny — шрифт ДАНИХ у таблиці (масштабується A-/A+)
///     • FontUI — шрифт ХРОМУ програми (кнопки шапки, фільтри) — ФІКСОВАНИЙ 11pt
///       Це стандартна практика UX: масштаб контенту ≠ масштаб інтерфейсу.
///
/// EN: Theme and font size manager. Persists settings to JSON.
///
///     FONTS:
///     • FontBase / FontSm / FontTiny — DATA font in table (scales with A-/A+)
///     • FontUI — program CHROME font (header buttons, filters) — FIXED 11pt
///       This is standard UX practice: content scale ≠ interface scale.
/// </summary>
public static class ThemeManager
{
    public static bool   IsDark   { get; private set; } = true;
    public static double FontSize { get; private set; } = 12.0;

    private static readonly string SettingsPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "ui_settings.json");

    private static readonly (string Key, Color Dark, Color Light)[] Palette =
    [
        // ── Фони / Backgrounds ────────────────────────────────────────────────
        ("BgBase",       Hex("#0D0A14"), Hex("#F8F5FF")),
        ("BgSurface",    Hex("#130F1E"), Hex("#FFFFFF")),
        ("BgCard",       Hex("#1C1530"), Hex("#EDE5FF")),
        ("BgCardHov",    Hex("#25203F"), Hex("#E0D4FF")),
        // ── Рамки / Borders ───────────────────────────────────────────────────
        ("BdNorm",       Hex("#2E1F5E"), Hex("#C8AAFF")),
        ("BdAcc",        Hex("#4A2F8A"), Hex("#9B70F0")),
        // ── Текст / Text ──────────────────────────────────────────────────────
        ("TextPrim",     Hex("#EAE0FF"), Hex("#1A0A3D")),
        ("TextSec",      Hex("#B0A0D0"), Hex("#4A3070")),   // UA: підвищено контраст / EN: improved contrast
        ("TextDim",      Hex("#7A60A8"), Hex("#7A60A0")),   // UA: підвищено з #5A4080 / EN: raised from #5A4080
        // ── Акцент EMP_UA / Accent ────────────────────────────────────────────
        ("Accent",       Hex("#8B5CF6"), Hex("#6B2FD4")),
        ("AccentDark",   Hex("#6D3AD9"), Hex("#5520B0")),
        // ── Прапор / Ukrainian flag ───────────────────────────────────────────
        ("FlagYellow",   Hex("#F5C518"), Hex("#C4960A")),
        ("FlagBlue",     Hex("#0057B8"), Hex("#004A99")),
        // ── Статус / Status ───────────────────────────────────────────────────
        ("StatusGreen",  Hex("#2ECC71"), Hex("#1A8A4A")),
        ("StatusRed",    Hex("#E04444"), Hex("#C0392B")),
        ("StatusAmber",  Hex("#E8A020"), Hex("#B07800")),
        ("SafeColor",    Hex("#22DD88"), Hex("#128A58")),
        // ── DataGrid ──────────────────────────────────────────────────────────
        ("RowBg",        Hex("#130F1E"), Hex("#FFFFFF")),
        ("RowBgAlt",     Hex("#170D26"), Hex("#F5F0FF")),
        ("RowHov",       Hex("#1F1737"), Hex("#EDE5FF")),
        ("RowSel",       Hex("#372378"), Hex("#D4C0FF")),
        ("HdrBg",        Hex("#0F0B18"), Hex("#EDE5FF")),
        ("GridLine",     Hex("#211641"), Hex("#D8CCEE")),
        // ── Поле вводу / Input ────────────────────────────────────────────────
        ("InputBg",      Hex("#0D0A14"), Hex("#FFFFFF")),
        ("InputBd",      Hex("#3A2A72"), Hex("#B090E8")),
        // ── Кнопка "Очистити технічні" / "Clear Technical" button ────────────
        // UA: Має чітко виділятись в обох темах як "небезпечна" дія
        // EN: Must clearly stand out in both themes as a "dangerous" action
        ("OrangeBg",     Hex("#3D1A00"), Hex("#FFF0E0")),
        ("OrangeBd",     Hex("#FF6600"), Hex("#CC4400")),
        ("OrangeFg",     Hex("#FFAA55"), Hex("#882200")),
    ];

    // ── Публічні методи / Public API ──────────────────────────────────────────

    public static void Toggle()             => Apply(!IsDark);
    public static void IncreaseFontSize()   => SetFontSize(FontSize + 1.0);
    public static void DecreaseFontSize()   => SetFontSize(FontSize - 1.0);

    public static void SetFontSize(double size)
    {
        FontSize = Math.Clamp(size, 10.0, 20.0);
        ApplyFontSizes();
    }

    public static void LoadAndApply()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var s = JsonSerializer.Deserialize<Settings>(json);
                if (s is not null)
                {
                    IsDark   = s.IsDark;
                    FontSize = Math.Clamp(s.FontSize, 10.0, 20.0);
                }
            }
        }
        catch { }
        Apply(IsDark);
    }

    public static void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(
                new Settings { IsDark = IsDark, FontSize = FontSize },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    // ── Внутрішнє / Internal ──────────────────────────────────────────────────

    private static void Apply(bool dark)
    {
        IsDark = dark;
        var res = Application.Current.Resources;
        foreach (var (key, darkColor, lightColor) in Palette)
        {
            var brush = new SolidColorBrush(dark ? darkColor : lightColor);
            brush.Freeze();
            res[key] = brush;
        }
        ApplyFontSizes();
    }

    private static void ApplyFontSizes()
    {
        var res = Application.Current.Resources;

        // UA: Шрифти ДАНИХ (масштабуються кнопками A-/A+)
        // EN: DATA fonts (scale with A-/A+ buttons)
        res["FontBase"] = FontSize;
        res["FontSm"]   = Math.Max(FontSize - 1.5, 9.0);
        res["FontTiny"] = Math.Max(FontSize - 2.5, 8.5);

        // UA: Шрифт ХРОМУ інтерфейсу — ФІКСОВАНИЙ, не змінюється від A-/A+
        //     Кнопки шапки, підписи прогресу, фільтри тулбару.
        //     Без цього при шрифті 20pt кнопки виповзають за межі вікна.
        // EN: Interface CHROME font — FIXED, not changed by A-/A+
        //     Header buttons, progress labels, toolbar filters.
        //     Without this at 20pt font, buttons overflow window bounds.
        res["FontUI"]     = 11.0;
        res["FontUILg"]   = 12.0;
    }

    private static Color Hex(string hex)
    {
        hex = hex.TrimStart('#');
        return Color.FromRgb(
            Convert.ToByte(hex[0..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }

    private sealed class Settings
    {
        public bool   IsDark   { get; set; } = true;
        public double FontSize { get; set; } = 12.0;
    }
}
