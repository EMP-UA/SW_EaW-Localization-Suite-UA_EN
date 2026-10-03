using System.Text.RegularExpressions;

namespace EaWLocalizationTool.GUI.Services;

/// <summary>
/// UA: Перевіряє чи переклад зберігає всі спецсимволи оригіналу.
///
///     ПЕРЕВІРЯЄТЬСЯ:
///     • Кількість переносів рядків \n та символів повернення каретки \r
///     • Пробіли й переноси на початку та в кінці рядка: мають збігатися з оригіналом
///     • Фігурні модифікатори {..}: наявність, вміст і кількість дужок { }
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
///     • Newline \n count and carriage-return \r count
///     • Whitespace and line breaks at the start and end of the string: must match the original
///     • Curly modifiers {..}: presence, content and the count of { } braces
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

    // UA: Фігурні модифікатори {..} — вміст і кількість перевіряються повністю:
    //     модифікатор не перекладається, тому його втрата чи зміна є помилкою.
    // EN: Curly modifiers {..} — content and count are checked fully: a modifier
    //     is not translated, so losing or altering it is an error.
    private static readonly Regex RxBraceTag = new(
        @"\{[^{}]*\}",
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

        // ── 1b. Повернення каретки / Carriage returns ─────────────────────────
        int origCr  = original.Count(c => c == '\r');
        int transCr = translated.Count(c => c == '\r');
        if (origCr != transCr)
            issues.Add($"\\r: {origCr}→{transCr}");

        // ── 1c. Пробіли й переноси на краях / Edge whitespace ──────────────────
        // UA: Порівнюється точна послідовність пробільних символів на початку та в
        //     кінці: втрачений кінцевий пробіл чи перенос змінює відображення в грі.
        // EN: The exact run of whitespace characters at the start and the end is
        //     compared: a lost trailing space or line break changes in-game display.
        string origLead = LeadingWhitespace(original), transLead = LeadingWhitespace(translated);
        if (!string.Equals(origLead, transLead, StringComparison.Ordinal))
            issues.Add($"початок / start: «{Show(origLead)}»→«{Show(transLead)}»");

        string origTail = TrailingWhitespace(original), transTail = TrailingWhitespace(translated);
        if (!string.Equals(origTail, transTail, StringComparison.Ordinal))
            issues.Add($"кінець / end: «{Show(origTail)}»→«{Show(transTail)}»");

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

        // ── 5. Фігурні модифікатори / Curly modifiers ──────────────────────────
        // UA: Спершу вміст пар {..}, потім окремо кількість дужок — це ловить і
        //     непарні «{» чи «}».
        // EN: First the content of {..} pairs, then the brace counts separately —
        //     this also catches unpaired "{" or "}".
        var origBrace  = ExtractSorted(RxBraceTag, original);
        var transBrace = ExtractSorted(RxBraceTag, translated);
        int origOpen  = original.Count(c => c == '{');
        int transOpen = translated.Count(c => c == '{');
        int origClose  = original.Count(c => c == '}');
        int transClose = translated.Count(c => c == '}');
        if (!origBrace.SequenceEqual(transBrace) ||
            origOpen != transOpen || origClose != transClose)
            issues.Add($"{{…}}: {origBrace.Count}→{transBrace.Count}" +
                       (origOpen != transOpen || origClose != transClose
                           ? $" ({{ {origOpen}→{transOpen}, }} {origClose}→{transClose})"
                           : ""));

        bool hasIssue = issues.Count > 0;
        return (hasIssue, string.Join("  ·  ", issues));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// UA: Повертає переклад, у якого пробіли й переноси на початку та в кінці взято з
    ///     оригіналу. Сам текст (усе між краями) не змінюється ні на символ; якщо
    ///     переклад складається лише з пробільних символів, він повертається без змін.
    /// EN: Returns the translation with the whitespace and line breaks at its start and
    ///     end taken from the original. The text itself (everything between the edges)
    ///     is not changed by a single character; a translation made only of whitespace
    ///     is returned unchanged.
    /// </summary>
    public static string AlignEdgeWhitespace(string original, string translated)
    {
        if (string.IsNullOrEmpty(translated)) return translated;

        int start = 0;
        while (start < translated.Length && char.IsWhiteSpace(translated[start])) start++;
        if (start == translated.Length) return translated;

        int end = translated.Length;
        while (end > start && char.IsWhiteSpace(translated[end - 1])) end--;

        string core = translated[start..end];
        string aligned = LeadingWhitespace(original) + core + TrailingWhitespace(original);

        // UA: Запобіжник: між краями має лишитися той самий текст.
        // EN: Safety net: the text between the edges must stay identical.
        return aligned.Trim() == core ? aligned : translated;
    }

    private static string LeadingWhitespace(string text)
    {
        int i = 0;
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        return text[..i];
    }

    private static string TrailingWhitespace(string text)
    {
        int i = text.Length;
        while (i > 0 && char.IsWhiteSpace(text[i - 1])) i--;
        return text[i..];
    }

    // UA: Видимий запис пробільних символів для повідомлення: пробіл — ␠, перенос — \n тощо.
    // EN: A visible rendering of whitespace for the message: space — ␠, line break — \n, etc.
    private static string Show(string whitespace)
    {
        if (whitespace.Length == 0) return "∅";

        var sb = new System.Text.StringBuilder();
        foreach (char c in whitespace)
        {
            sb.Append(c switch
            {
                ' ' => "␠",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => $"U+{(int)c:X4}"
            });
        }
        return sb.ToString();
    }

    private static List<string> ExtractSorted(Regex rx, string text) =>
        rx.Matches(text).Select(m => m.Value).OrderBy(s => s).ToList();
}
