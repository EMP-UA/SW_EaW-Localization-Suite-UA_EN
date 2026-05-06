// ===================================================
//  Star Wars: EaW - Localization Tool
//  Created by EMP_UA
// ============================================================
//  Translation/Validator.cs
//  UA: Валідація. Виправлено регулярні вирази для ігрових діалогів.
//  EN: Validation. Fixed regex for in-game dialogues.
// ============================================================

using System.Text.RegularExpressions;
using EaWLocalizer.Core;

namespace EaWLocalizer.Translation;

public static class Validator
{
    static readonly Regex TechTokens = new(@"\\[nNtTrR]|%[sdiufg\d]|\{\d+\}|\[COLOR[^\]]*\]|\[BR\]|\[TAB\]|<[^>]+>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex InvalidCyrillicMarkers = new("[ыэёъЫЭЁЪ]", RegexOptions.Compiled);

    // UA: Використовуємо \b для точного пошуку слів.
    // EN: Use \b for precise word matching.
    static readonly Regex TranslationRefusalPhrase = new(@"\b(я\s+не\s+можу|вибачте|вибач|ось\s+переклад|це\s+переклад|не\s+можу\s+(перекласти|допомогти|виконати|надати))\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex OriginalRefusalPhrase = new(@"\b(can'?t|cannot|unable|sorry|i\s+can'?t|i\s+cannot|unavailable)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex AlwaysHallucinationPhrase = new(@"\b(here\s+is\s+(your\s+)?translation|ось\s+переклад|це\s+переклад|i\s+will\s+(now\s+)?translate|ось\s+мій\s+переклад|переклад\s*:)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsTranslatable(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        string t = text.Trim();
        if (t.Length <= 2 || t.Contains('_') || t.Contains('/') || t.Contains('\\')) return false;
        if (Regex.IsMatch(t, @"^[0-9\.\-\+,]+$")) return false;
        if (Regex.IsMatch(t, @"^[A-Z0-9\s]+$") && !t.Any(char.IsLower)) return false;
        if (!t.Contains(' ') && Regex.IsMatch(t, @"\d")) return false;
        if (!t.Contains(' ') && !t.Any(char.IsLower)) return false;
        return true;
    }

    public static (bool ok, string reason) ValidateTranslation(string original, string translated)
    {
        if (string.IsNullOrWhiteSpace(translated)) return (false, "Empty translation");
        if (AlwaysHallucinationPhrase.IsMatch(translated)) return (false, "Hallucination phrase detected (meta-commentary)");

        // UA: Якщо в оригіналі є "can't", то в перекладі "не можу" — це не помилка.
        // EN: If original has "can't", then "не можу" in translation is not an error.
        if (TranslationRefusalPhrase.IsMatch(translated) && !OriginalRefusalPhrase.IsMatch(original))
            return (false, "Hallucination phrase detected (model refusal)");

        if (InvalidCyrillicMarkers.IsMatch(translated)) return (false, "Hallucination-marker characters detected");

        if (translated.Length > 300)
        {
            string stripped = TechTokens.Replace(translated, " ");
            bool hasCyrillic = stripped.Any(ch => (ch >= 'А' && ch <= 'я') || "ІіЇїЄєҐґ".Contains(ch));
            if (!hasCyrillic) return (false, "No Cyrillic characters — likely untranslated");
        }
        return (true, "OK");
    }

    public static string CleanTranslation(string s, string context = "")
    {
        if (string.IsNullOrEmpty(s) || !InvalidCyrillicMarkers.IsMatch(s)) return s;
        File.AppendAllText(Config.LogPath, $"[{DateTime.UtcNow:HH:mm:ss}] Cleaned [{context}]: {s}\n");
        return s.Replace("ы", "и").Replace("Ы", "И").Replace("э", "е").Replace("Э", "Е").Replace("ё", "е").Replace("Ё", "Е").Replace("ъ", "");
    }
}