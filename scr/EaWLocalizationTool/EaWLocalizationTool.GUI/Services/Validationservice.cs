using System.Text.RegularExpressions;

namespace EaWLocalizationTool.GUI.Services;

/// <summary>
/// UA: Перевіряє чи переклад зберігає всі спецсимволи оригіналу.
///
///     ПЕРЕВІРЯЄТЬСЯ:
///     • Кількість переносів рядків \n
///     • Формат-рядки EaW: %s %d %i %u %f %g (БЕЗ space-flag щоб не спрацьовував на "30% chance")
///     • КІЛЬКІСТЬ (не вміст!) груп у дужках [..] — вміст може бути перекладений
///
///     НЕ ПЕРЕВІРЯЄТЬСЯ:
///     • %% (escaped percent — EaW не використовує цей синтаксис)
///     • Рідкісні специфікатори %c %p %x тощо (не зустрічаються у рядках гри)
///
/// EN: Checks if translation preserves all special sequences from original.
///
///     CHECKS:
///     • Newline \n count
///     • EaW format specifiers: %s %d %i %u %f %g (NO space-flag to avoid "30% chance" false positives)
///     • COUNT (not content!) of bracket groups [..] — content may be translated
///
///     NOT CHECKED:
///     • %% (escaped percent — EaW doesn't use this syntax)
///     • Rare specifiers %c %p %x etc. (not found in game strings)
/// </summary>
public static class ValidationService
{
    // UA: Лише реальні EaW формат-специфікатори, БЕЗ пробілу у флагах.
    //     Старий варіант [-+0 #]* некоректно матчив "30% chance" як %c зі space-flag.
    // EN: Only real EaW format specifiers, WITHOUT space in flags.
    //     Old variant [-+0 #]* incorrectly matched "30% chance" as %c with space-flag.
    private static readonly Regex RxFormat = new(
        @"%[-+0#]*\d*(?:\.\d+)?[sdifugGeE]",
        RegexOptions.Compiled);

    // UA: Теги у квадратних дужках — лише КІЛЬКІСТЬ перевіряється, не вміст.
    //     [Imperial Captain] і [Імперський капітан] — однакова кількість груп → OK.
    //     Але [c: 1 0 0] в оригіналі і відсутність у перекладі → попередження.
    // EN: Square bracket tags — only COUNT checked, not content.
    //     [Imperial Captain] and [Imperialнй капітан] — same count of groups → OK.
    //     But [c: 1 0 0] in original and missing in translation → warning.
    private static readonly Regex RxBracketTag = new(
        @"\[[^\]]*\]",
        RegexOptions.Compiled);

    // UA: Кутові HTML-подібні теги — вміст і кількість перевіряються повністю
    // EN: Angle HTML-like tags — content and count checked fully
    private static readonly Regex RxAngleTag = new(
        @"<[A-Za-z/][^>]*>",
        RegexOptions.Compiled);

    /// <summary>
    /// UA: Перевіряє переклад відносно оригіналу.
    ///     Повертає (true, "опис") при проблемі, (false, "") якщо все OK.
    /// EN: Validates translated text against original.
    ///     Returns (true, "description") on issue, (false, "") if all OK.
    /// </summary>
    public static (bool HasIssue, string Description) Check(
        string original, string translated)
    {
        // UA: Порожній переклад — не помилка валідації (є окремий фільтр "Без перекладу")
        // EN: Empty translation — not a validation error (separate "Untranslated" filter handles it)
        if (string.IsNullOrEmpty(translated)) return (false, "");

        var issues = new List<string>();

        // ── 1. Переноси рядків / Newlines ─────────────────────────────────────
        int origNl  = original.Count(c => c == '\n');
        int transNl = translated.Count(c => c == '\n');
        if (origNl != transNl)
            issues.Add($"\\n: {origNl}→{transNl}");

        // ── 2. Формат-рядки / Format specifiers ───────────────────────────────
        // UA: Витягуємо і сортуємо щоб порівняти незалежно від порядку
        // EN: Extract and sort to compare regardless of order
        var origFmt  = ExtractSorted(RxFormat, original);
        var transFmt = ExtractSorted(RxFormat, translated);
        if (!origFmt.SequenceEqual(transFmt))
        {
            string o = origFmt.Count  > 0 ? string.Join(" ", origFmt)  : "—";
            string t = transFmt.Count > 0 ? string.Join(" ", transFmt) : "—";
            issues.Add($"формат / format: [{o}]→[{t}]");
        }

        // ── 3. Квадратні теги — лише кількість / Bracket tags — count only ────
        // UA: Вміст [..] може бути перекладеним (напр. назви персонажів).
        //     Перевіряємо лише кількість груп, щоб не було зайвих/відсутніх.
        // EN: Content of [..] may be translated (e.g. character names).
        //     Check only count of groups so none are extra or missing.
        int origBr  = RxBracketTag.Matches(original).Count;
        int transBr = RxBracketTag.Matches(translated).Count;
        if (origBr != transBr)
            issues.Add($"[теги/tags]: {origBr}→{transBr}");

        // ── 4. Кутові теги — повна перевірка / Angle tags — full check ─────────
        var origAngle  = ExtractSorted(RxAngleTag, original);
        var transAngle = ExtractSorted(RxAngleTag, translated);
        if (!origAngle.SequenceEqual(transAngle))
            issues.Add($"<теги/tags>: {origAngle.Count}→{transAngle.Count}");

        bool hasIssue = issues.Count > 0;
        return (hasIssue, string.Join("  ·  ", issues));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<string> ExtractSorted(Regex rx, string text) =>
        rx.Matches(text).Select(m => m.Value).OrderBy(s => s).ToList();
}
