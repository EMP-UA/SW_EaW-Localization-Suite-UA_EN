// =============================================================================
// EaWLocalizationTool.Core — DatTranslationTransfer.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Перенесення перекладу з DAT основної гри (Empire at War) у DAT доповнення
//     (Forces of Corruption) — і взагалі між будь-якими двома DAT рушія Alamo.
//
//     ДЖЕРЕЛО (ДОНОР) — це набір пар «ключ → англійський оригінал → переклад»:
//       • переклад у DAT: потрібні ДВА файли — ванільний (оригінал) і
//         перекладений (див. PairWithOriginals);
//       • переклад у TSV з колонкою OriginalText (експорт самого GUI) —
//         достатньо одного файлу.
//
//     ПОРЯДОК ЗІСТАВЛЕННЯ для кожного рядка цілі:
//       1. За КЛЮЧЕМ, якщо англійський текст донора й цілі збігається
//          (без урахування регістру й пробілів) — однозначний збіг.
//          Ключ у DAT однозначно задає контекст рядка, тому збіг за ключем
//          безпечніший за збіг за текстом.
//       2. За ТЕКСТОМ англійського оригіналу — для ключів, яких немає в донорі
//          (або текст під цим ключем у доповненні змінено). Спершу шукається
//          ТОЧНИЙ текст (з урахуванням регістру й пробілів), і лише за його
//          відсутності — нормалізований. Якщо донор має рівно один переклад
//          цього тексту — збіг однозначний; якщо кілька різних — конфлікт,
//          і рішення приймає користувач. Так «Fall out» і «Fall out » (з
//          пробілом) отримують кожен свій переклад, а не конфлікт.
//       3. Дрібна відмінність. Якщо ключ є в донорі, а англійські тексти
//          відрізняються лише дрібницею, що не змінює змісту (одрук, зайва чи
//          відсутня літера або розділовий знак), збіг за ключем приймається:
//          не менше 20 символів, не більше 3 символьних правок і не більше 5%
//          довжини, цифри однакові, різниця не лише в дефісі/пробілі
//          (див. IsMinorDifference). Такі рядки
//          переносяться за ключем без підтвердження й позначаються у звіті.
//       4. Інакше — відповідника немає. Якщо ключ є в донорі, але англійський
//          текст відрізняється й не зустрічається під іншим ключем, рядок
//          позначається як «текст змінено» і не переноситься.
//
//     Модуль нічого не знає про GUI та статуси вичитки: він лише повідомляє,
//     який переклад підходить рядку. Захист вичитаних рядків і запис результату
//     виконує викликач.
//
// EN: Translation transfer from the base game's DAT (Empire at War) into the
//     expansion's DAT (Forces of Corruption) — or between any two Alamo
//     engine DAT files.
//
//     THE SOURCE (DONOR) is a set of "key → English original → translation"
//     triples:
//       • translation in a DAT: TWO files are needed — the vanilla one
//         (original) and the translated one (see PairWithOriginals);
//       • translation in a TSV with an OriginalText column (the GUI's own
//         export) — one file is enough.
//
//     MATCHING ORDER for every target row:
//       1. By KEY, when the donor's and the target's English texts are equal
//          (ignoring case and whitespace) — an unambiguous match. A DAT key
//          identifies the string's context, so a key match is safer than a
//          text match.
//       2. By the English original TEXT — for keys absent from the donor (or
//          whose text differs in the expansion). The EXACT text (case and
//          whitespace included) is looked up first, and the normalized text
//          only when no exact one exists. If the donor has exactly one
//          translation of that text, the match is unambiguous; if it has
//          several distinct ones, it is a conflict and the user decides.
//          This way "Fall out" and "Fall out " (with a trailing space) each
//          get their own translation instead of a conflict.
//       3. Minor difference. If the key exists in the donor and the English texts
//          differ only by a trifle that does not change the meaning (a typo, an
//          extra or missing letter or punctuation mark), the key match is accepted:
//          at least 20 characters, at most 3 character edits and at most 5% of the
//          length, the same digits, not merely a hyphen/space (see IsMinorDifference). Such rows transfer by
//          key without a confirmation and are marked in the report.
//       4. Otherwise — no match. If the key exists in the donor but the English
//          text differs and does not occur under another key, the row is
//          reported as "text changed" and is not transferred.
//
//     The module knows nothing about the GUI or review statuses: it only
//     reports which translation fits a row. Protection of reviewed rows and
//     writing the result are the caller's responsibility.
// =============================================================================

using System.Text.RegularExpressions;
using EaWLocalizationTool.Core.Models;

namespace EaWLocalizationTool.Core;

/// <summary>
/// UA: Спосіб, у який знайдено переклад для рядка цілі.
/// EN: How the translation for a target row was found.
/// </summary>
public enum TransferMatchKind
{
    /// <summary>UA: Відповідника немає. / EN: No match.</summary>
    None,

    /// <summary>UA: Збіг за ключем при однаковому англійському тексті. / EN: Key match with equal English text.</summary>
    Key,

    /// <summary>UA: Однозначний збіг за англійським текстом під іншим ключем. / EN: Unambiguous English-text match under another key.</summary>
    Text,

    /// <summary>UA: Кілька різних перекладів того самого тексту — потрібне рішення користувача. / EN: Several distinct translations of the same text — a user decision is required.</summary>
    Conflict,

    /// <summary>UA: Ключ є в донорі, але англійський текст інший. / EN: The key exists in the donor, but the English text differs.</summary>
    TextChanged
}

/// <summary>
/// UA: Результат зіставлення одного рядка цілі з донором.
///     Translation заповнено для Key і Text; Candidates — для Conflict.
///     MinorDifference — збіг за ключем, де англійські тексти відрізняються лише
///     дрібницею (див. IsMinorDifference).
/// EN: Result of matching one target row against the donor.
///     Translation is set for Key and Text; Candidates is set for Conflict.
///     MinorDifference — a key match whose English texts differ only by a trifle
///     (see IsMinorDifference).
/// </summary>
public readonly record struct TransferMatch(
    TransferMatchKind Kind,
    string? Translation,
    IReadOnlyList<string> Candidates,
    bool MinorDifference = false)
{
    public static TransferMatch NoMatch => new(TransferMatchKind.None, null, []);
}

/// <summary>
/// UA: Індекс донора: пошук за ключем і за нормалізованим англійським текстом.
/// EN: Donor index: lookup by key and by normalized English text.
/// </summary>
public sealed class DonorIndex
{
    internal Dictionary<string, (string Original, string Translation)> ByKey { get; } =
        new(StringComparer.Ordinal);

    internal Dictionary<string, List<string>> ByText { get; } =
        new(StringComparer.Ordinal);

    // UA: Той самий пошук за ТОЧНИМ англійським текстом (регістр і пробіли значущі).
    // EN: The same lookup by the EXACT English text (case and whitespace are significant).
    internal Dictionary<string, List<string>> ByExact { get; } =
        new(StringComparer.Ordinal);

    /// <summary>
    /// UA: Варіанти перекладу тексту: за точним текстом, а якщо його немає — за
    ///     нормалізованим; null — текст у донорі не зустрічається.
    /// EN: Translation variants of a text: by the exact text, or by the normalized
    ///     text when no exact one exists; null — the text does not occur in the donor.
    /// </summary>
    internal List<string>? Variants(string original)
    {
        if (ByExact.TryGetValue(original, out var exact)) return exact;
        return ByText.TryGetValue(DatTranslationTransfer.NormalizeText(original), out var normalized)
            ? normalized
            : null;
    }

    /// <summary>UA: Кількість ключів із перекладом. / EN: Number of keys that carry a translation.</summary>
    public int KeyCount => ByKey.Count;

    /// <summary>UA: Кількість різних англійських текстів із перекладом. / EN: Number of distinct English texts that carry a translation.</summary>
    public int TextCount => ByText.Count;

    /// <summary>UA: Пари, відхилені перевіркою набору символів. / EN: Pairs rejected by the character set check.</summary>
    public int RejectedCount { get; internal set; }

    internal bool ContainsKey(string key) => ByKey.ContainsKey(key);
}

public static class DatTranslationTransfer
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// UA: Ключ зіставлення тексту: пробільні символи згорнуто до одного пробілу,
    ///     краї обрізано, регістр відкинуто.
    /// EN: Text matching key: whitespace collapsed to a single space, ends
    ///     trimmed, case ignored.
    /// </summary>
    public static string NormalizeText(string text) =>
        WhitespaceRegex.Replace(text, " ").Trim().ToUpperInvariant();

    /// <summary>
    /// UA: Поєднує оригінали ванільного DAT з перекладами (словник Key → текст,
    ///     з перекладеного DAT або TSV) у пари для BuildIndex.
    ///     Рядки без перекладу пропускаються.
    /// EN: Combines the vanilla DAT's originals with translations (a Key → text
    ///     dictionary from a translated DAT or TSV) into pairs for BuildIndex.
    ///     Rows without a translation are skipped.
    /// </summary>
    public static List<(string Key, string? Original, string Translation)> PairWithOriginals(
        IEnumerable<DatEntry> originals,
        IReadOnlyDictionary<string, string> translations)
    {
        var pairs = new List<(string, string?, string)>();
        foreach (var entry in originals)
        {
            if (translations.TryGetValue(entry.Key, out var translation) &&
                !string.IsNullOrWhiteSpace(translation))
            {
                pairs.Add((entry.Key, entry.OriginalText, translation));
            }
        }
        return pairs;
    }

    /// <summary>
    /// UA: Будує індекс донора. Пари без оригіналу, з порожнім перекладом або з
    ///     перекладом, що збігається з оригіналом (неперекладений рядок),
    ///     відкидаються. При дублікатах ключа береться перший запис; повторні
    ///     варіанти перекладу одного тексту (однакові після нормалізації)
    ///     зливаються в один.
    /// EN: Builds the donor index. Pairs without an original, with an empty
    ///     translation, or whose translation equals the original (an
    ///     untranslated row) are dropped. For duplicate keys the first record
    ///     wins; repeated translation variants of one text (equal after
    ///     normalization) collapse into one.
    /// </summary>
    public static DonorIndex BuildIndex(
        IEnumerable<(string Key, string? Original, string Translation)> donorPairs)
    {
        var index = new DonorIndex();

        foreach (var (key, original, translation) in donorPairs)
        {
            if (string.IsNullOrWhiteSpace(original)) continue;
            if (string.IsNullOrWhiteSpace(translation)) continue;
            if (!TextGuard.IsClean(translation)) { index.RejectedCount++; continue; }

            string normalizedOriginal = NormalizeText(original);
            string normalizedTranslation = NormalizeText(translation);
            if (normalizedTranslation == normalizedOriginal) continue;

            index.ByKey.TryAdd(key, (original, translation));

            if (!index.ByText.TryGetValue(normalizedOriginal, out var variants))
                index.ByText[normalizedOriginal] = variants = [];
            if (!variants.Any(v => NormalizeText(v) == normalizedTranslation))
                variants.Add(translation);

            if (!index.ByExact.TryGetValue(original, out var exactVariants))
                index.ByExact[original] = exactVariants = [];
            if (!exactVariants.Any(v => NormalizeText(v) == normalizedTranslation))
                exactVariants.Add(translation);
        }

        return index;
    }

    /// <summary>
    /// UA: Підбирає переклад для одного рядка цілі за правилами з заголовка файлу.
    /// EN: Picks a translation for one target row using the rules from the file header.
    /// </summary>
    public static TransferMatch Match(DonorIndex index, string targetKey, string targetOriginal)
    {
        if (string.IsNullOrWhiteSpace(targetOriginal)) return TransferMatch.NoMatch;

        string normalized = NormalizeText(targetOriginal);

        bool keyFound = index.ByKey.TryGetValue(targetKey, out var byKey);
        if (keyFound && NormalizeText(byKey.Original) == normalized)
            return new TransferMatch(TransferMatchKind.Key, byKey.Translation, []);

        var variants = index.Variants(targetOriginal);
        if (variants is not null)
        {
            return variants.Count == 1
                ? new TransferMatch(TransferMatchKind.Text, variants[0], [])
                : new TransferMatch(TransferMatchKind.Conflict, null, variants);
        }

        if (keyFound && IsMinorDifference(targetOriginal, byKey.Original))
            return new TransferMatch(TransferMatchKind.Key, byKey.Translation, [], MinorDifference: true);

        return keyFound
            ? new TransferMatch(TransferMatchKind.TextChanged, null, [])
            : TransferMatch.NoMatch;
    }

    private static string WithoutHyphens(string text) =>
        WhitespaceRegex.Replace(text.Replace('-', ' '), " ").Trim();

    /// <summary>UA: Найбільше символьних правок для «дрібної» відмінності. / EN: The most character edits for a "minor" difference.</summary>
    public const int MinorMaxEdits = 3;

    /// <summary>UA: Найбільша частка правок від довжини тексту. / EN: The largest share of edits relative to the text length.</summary>
    public const double MinorMaxRatio = 0.05;

    /// <summary>UA: Найкоротший текст, для якого відмінність може бути дрібною. / EN: The shortest text for which a difference can be minor.</summary>
    public const int MinorMinLength = 20;

    /// <summary>
    /// UA: Чи відрізняються два англійські тексти лише дрібницею, що не змінює змісту
    ///     (одрук, зайва або відсутня літера чи розділовий знак). Механічні умови, усі разом:
    ///     довжина не менша за MinorMinLength; не більше MinorMaxEdits символьних правок
    ///     (відстань Левенштейна за нормалізованими текстами) і не більше MinorMaxRatio
    ///     довжини; набір цифр однаковий; різниця не зводиться до дефіса/пробіла (інше
    ///     написання назви). Тексти, що відрізняються словами чи числами, під цю умову
    ///     не підпадають і потребують рішення користувача.
    /// EN: Whether two English texts differ only by a trifle that does not change the
    ///     meaning (a typo, an extra or missing letter or punctuation mark). Mechanical
    ///     conditions, all together: a length of at least MinorMinLength; at most
    ///     MinorMaxEdits character edits (Levenshtein distance on the normalized texts) and
    ///     at most MinorMaxRatio of the length; the same digits; the difference is not
    ///     merely a hyphen/space (another spelling of a name). Texts that differ by words
    ///     or numbers do not qualify and need a user decision.
    /// </summary>
    public static bool IsMinorDifference(string a, string b)
    {
        string x = NormalizeText(a), y = NormalizeText(b);
        if (x == y) return false;

        int longest = Math.Max(x.Length, y.Length);
        if (longest < MinorMinLength) return false;
        if (Math.Abs(x.Length - y.Length) > MinorMaxEdits) return false;
        if (new string(x.Where(char.IsDigit).ToArray()) != new string(y.Where(char.IsDigit).ToArray()))
            return false;

        // UA: Різниця лише в дефісі/пробілі («Abregado-rae» / «Abregado Rae») — це інше написання
        //     назви, а не одрук: рішення за користувачем.
        // EN: A difference only in hyphen/space ("Abregado-rae" / "Abregado Rae") is another
        //     spelling of a name, not a typo: the user decides.
        if (WithoutHyphens(x) == WithoutHyphens(y)) return false;

        int limit = Math.Min(MinorMaxEdits, (int)Math.Floor(longest * MinorMaxRatio));
        if (limit < 1) return false;

        // UA: Відстань Левенштейна з відсіканням: рядок матриці, що перевищив limit, зупиняє обчислення.
        // EN: Levenshtein distance with a cut-off: a matrix row exceeding limit stops the computation.
        var previous = new int[y.Length + 1];
        var current = new int[y.Length + 1];
        for (int j = 0; j <= y.Length; j++) previous[j] = j;

        for (int i = 1; i <= x.Length; i++)
        {
            current[0] = i;
            int rowMin = current[0];
            for (int j = 1; j <= y.Length; j++)
            {
                int cost = x[i - 1] == y[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
                if (current[j] < rowMin) rowMin = current[j];
            }
            if (rowMin > limit) return false;
            (previous, current) = (current, previous);
        }

        return previous[y.Length] <= limit;
    }

    /// <summary>
    /// UA: Що знає донор про рядок цілі: англійський текст і переклад за ключем,
    ///     а також варіанти перекладу за англійським текстом рядка. Нічого не змінює.
    /// EN: What the donor knows about a target row: the English text and translation
    ///     by key, and the translation variants by the row's English text. Changes nothing.
    /// </summary>
    public static TransferRowInfo Inspect(DonorIndex index, string key, string original)
    {
        string? donorOriginal = null, byKeyTranslation = null;
        if (index.ByKey.TryGetValue(key, out var donorEntry))
            (donorOriginal, byKeyTranslation) = donorEntry;

        IReadOnlyList<string> byText =
            !string.IsNullOrWhiteSpace(original) && index.Variants(original) is { } variants
                ? variants
                : [];

        return new TransferRowInfo(donorOriginal, byKeyTranslation, byText);
    }

    /// <summary>
    /// UA: Чи потрібне рішення користувача для рядка: ключ є в донорі, але англійський
    ///     текст за цим ключем інший. Це рядки TextChanged (фрази в донорі немає зовсім)
    ///     і рядки Text, де фраза знайдена під іншим ключем, хоча сам ключ має інший текст.
    ///     Конфлікти вирішуються окремо (CollectConflicts).
    /// EN: Whether a row needs a user decision: the key exists in the donor, but the
    ///     English text under that key differs. These are TextChanged rows (the phrase
    ///     is absent from the donor entirely) and Text rows where the phrase was found
    ///     under another key although this key carries a different text.
    ///     Conflicts are resolved separately (CollectConflicts).
    /// </summary>
    public static bool NeedsReview(TransferMatch match, TransferRowInfo info) =>
        match.Kind == TransferMatchKind.TextChanged ||
        (match.Kind == TransferMatchKind.Text && info.DonorOriginal is not null);

    /// <summary>
    /// UA: Попередній аналіз без жодних змін: скільки рядків цілі збігається з донором
    ///     за ключем, скільки — лише за англійським текстом, скільки має конфлікт
    ///     і скільки не має відповідника. Технічні рядки в підрахунок збігів не входять.
    /// EN: Dry-run analysis that changes nothing: how many target rows match the donor
    ///     by key, how many only by English text, how many are in conflict and how
    ///     many have no match. Technical rows are excluded from the match counts.
    /// </summary>
    public static TransferAnalysis Analyze(
        DonorIndex index,
        IEnumerable<(string Key, string Original, bool Technical)> targets)
    {
        int rows = 0, byKey = 0, byText = 0, conflicts = 0, textChanged = 0, none = 0, keyTextDiffer = 0;
        var targetKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (key, original, technical) in targets)
        {
            targetKeys.Add(key);
            if (technical) continue;
            rows++;

            var match = Match(index, key, original);
            switch (match.Kind)
            {
                case TransferMatchKind.Key:
                    byKey++;
                    // UA: Чи дав би збіг за англійським текстом інший переклад, ніж збіг за ключем
                    // EN: Whether an English-text match would give a different translation than the key match
                    if (index.Variants(original) is { } variants &&
                        (variants.Count != 1 ||
                         NormalizeText(variants[0]) != NormalizeText(match.Translation ?? "")))
                        keyTextDiffer++;
                    break;
                case TransferMatchKind.Text: byText++; break;
                case TransferMatchKind.Conflict: conflicts++; break;
                case TransferMatchKind.TextChanged: textChanged++; break;
                default: none++; break;
            }
        }

        int donorOnly = index.ByKey.Keys.Count(k => !targetKeys.Contains(k));

        return new TransferAnalysis(
            index.KeyCount, index.TextCount, index.RejectedCount,
            rows, byKey, byText, conflicts, textChanged, none, donorOnly, keyTextDiffer);
    }

    /// <summary>
    /// UA: Будує звіт зіставлення для всіх нетехнічних рядків цілі: вид збігу, англійський
    ///     текст цілі й донора, переклад за ключем, варіанти перекладу за англійським
    ///     текстом і прапорець розбіжності між ними. Нічого не змінює.
    /// EN: Builds the matching report for all non-technical target rows: match kind, the
    ///     target's and the donor's English text, the translation by key, the translation
    ///     variants by English text and a flag for a discrepancy between them. Changes nothing.
    /// </summary>
    public static List<TransferReportRow> BuildReport(
        DonorIndex index,
        IEnumerable<(string Key, string Original, bool Technical)> targets)
    {
        var rows = new List<TransferReportRow>();

        foreach (var (key, original, technical) in targets)
        {
            if (technical) continue;

            var match = Match(index, key, original);
            var (donorOriginal, byKeyTranslation, byText) = Inspect(index, key, original);

            bool differ = match.Kind == TransferMatchKind.Key && byText.Count > 0 &&
                          (byText.Count != 1 ||
                           NormalizeText(byText[0]) != NormalizeText(byKeyTranslation ?? ""));

            rows.Add(new TransferReportRow(
                key, match.Kind, differ, original, donorOriginal, byKeyTranslation, byText,
                match.MinorDifference));
        }

        return rows;
    }

    /// <summary>
    /// UA: Записує звіт у TSV (UTF-8 з BOM) для перегляду в Excel / Google Sheets.
    /// EN: Writes the report to a TSV (UTF-8 with BOM) for Excel / Google Sheets.
    /// </summary>
    public static void WriteReportTsv(string path, IEnumerable<TransferReportRow> rows)
    {
        static string Esc(string? s) =>
            (s ?? "").Replace("\r", "").Replace("\n", "\\n").Replace("\t", " ");

        using var writer = new StreamWriter(path, false,
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        writer.WriteLine(
            "Key\tMatch\tKeyTextDiffer\tTargetEnglish\tDonorEnglishByKey\tTranslationByKey\tTranslationsByText");

        foreach (var row in rows)
        {
            string kind = row.Kind switch
            {
                TransferMatchKind.Key when row.MinorDifference => "KeyMinorDiff",
                TransferMatchKind.Key => "Key",
                TransferMatchKind.Text => "TextOnly",
                TransferMatchKind.Conflict => "Conflict",
                TransferMatchKind.TextChanged => "TextChanged",
                _ => "NoMatch"
            };

            writer.WriteLine(string.Join('\t',
                Esc(row.Key), kind, row.KeyTextDiffer ? "yes" : "",
                Esc(row.TargetOriginal), Esc(row.DonorOriginalByKey), Esc(row.TranslationByKey),
                Esc(string.Join(" ‖ ", row.TranslationsByText))));
        }
    }

    /// <summary>
    /// UA: Збирає конфлікти для набору рядків цілі: англійський текст рядка (точний,
    ///     як у цілі) → різні варіанти перекладу в донорі. Рядки, що мають однозначний
    ///     збіг за ключем, у конфлікти не потрапляють.
    /// EN: Collects conflicts for a set of target rows: the row's English text (exact,
    ///     as in the target) → distinct translation variants in the donor. Rows with an unambiguous
    ///     key match are not conflicts.
    /// </summary>
    public static Dictionary<string, IReadOnlyList<string>> CollectConflicts(
        DonorIndex index,
        IEnumerable<(string Key, string Original)> targets)
    {
        var conflicts = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var (key, original) in targets)
        {
            var match = Match(index, key, original);
            if (match.Kind == TransferMatchKind.Conflict)
                conflicts[original] = match.Candidates;
        }

        return conflicts;
    }
}

/// <summary>
/// UA: Підсумок перенесення для рядка стану та вікна підсумку.
///     ByKey / ByText / FromConflicts — рядки, текст яких ЗМІНЕНО;
///     AlreadyEqual — збіг є, але переклад уже такий самий;
///     ProtectedSkipped — збіг є, але рядок вичитаний;
///     TextChanged — англійський текст рядка в цілі відрізняється від донора;
///     Unmatched — у донорі немає застосовного перекладу (або конфлікт пропущено);
///     KeyMatched / TextMatched / ConflictRows — усі рядки зі збігом відповідного виду,
///     незалежно від того, чи їх змінено, пропущено як вичитані чи вони вже однакові.
/// EN: Transfer summary for the status line and the summary dialog.
///     ByKey / ByText / FromConflicts — rows whose text was CHANGED;
///     AlreadyEqual — a match exists, but the translation is already identical;
///     ProtectedSkipped — a match exists, but the row is reviewed;
///     TextChanged — the target's English text differs from the donor's;
///     Unmatched — no applicable translation in the donor (or a conflict was skipped);
///     KeyMatched / TextMatched / ConflictRows — all rows with a match of that kind,
///     whether they were changed, skipped as reviewed or already identical.
/// </summary>
public readonly record struct TransferSummary(
    int ByKey,
    int ByText,
    int FromConflicts,
    int AlreadyEqual,
    int ProtectedSkipped,
    int TextChanged,
    int Unmatched,
    int KeyMatched = 0,
    int TextMatched = 0,
    int ConflictRows = 0,
    int FromReview = 0)
{
    /// <summary>UA: Загальна кількість змінених рядків. / EN: Total number of changed rows.</summary>
    public int Changed => ByKey + ByText + FromConflicts + FromReview;
}

/// <summary>
/// UA: Результат попереднього аналізу (див. Analyze).
///     DonorKeys / DonorTexts — ключів і різних англійських текстів у донорі;
///     DonorRejected — пар донора, відхилених перевіркою набору символів;
///     TargetRows — рядків цілі без технічних;
///     ByKey / ByText — збіг за ключем / лише за англійським текстом;
///     Conflicts — рядки, текст яких має кілька різних перекладів у донорі;
///     TextChanged — ключ є в донорі, але англійський текст інший;
///     NoMatch — відповідника немає;
///     DonorOnlyKeys — ключі донора, яких немає в цілі;
///     KeyTextDiffer — серед збігів за ключем рядки, для яких збіг за англійським
///     текстом дав би інший переклад (або кілька варіантів).
/// EN: Result of the dry-run analysis (see Analyze).
///     DonorKeys / DonorTexts — keys and distinct English texts in the donor;
///     DonorRejected — donor pairs rejected by the character set check;
///     TargetRows — target rows excluding technical ones;
///     ByKey / ByText — match by key / by English text only;
///     Conflicts — rows whose text has several distinct translations in the donor;
///     TextChanged — the key exists in the donor but the English text differs;
///     NoMatch — no match;
///     DonorOnlyKeys — donor keys absent from the target;
///     KeyTextDiffer — among key matches, rows for which an English-text match would
///     give a different translation (or several variants).
/// </summary>
public readonly record struct TransferAnalysis(
    int DonorKeys,
    int DonorTexts,
    int DonorRejected,
    int TargetRows,
    int ByKey,
    int ByText,
    int Conflicts,
    int TextChanged,
    int NoMatch,
    int DonorOnlyKeys,
    int KeyTextDiffer = 0)
{
    /// <summary>UA: Рядків, для яких є що переносити. / EN: Rows that have something to transfer.</summary>
    public int Transferable => ByKey + ByText + Conflicts;
}

/// <summary>
/// UA: Відомості донора про рядок цілі (див. Inspect): англійський текст і переклад за
///     ключем (null, якщо ключа в донорі немає) та варіанти перекладу за англійським текстом.
/// EN: The donor's data about a target row (see Inspect): the English text and translation
///     by key (null when the donor has no such key) and the translation variants by English text.
/// </summary>
public readonly record struct TransferRowInfo(
    string? DonorOriginal,
    string? TranslationByKey,
    IReadOnlyList<string> ByText);

/// <summary>
/// UA: Рядок звіту зіставлення (див. BuildReport).
///     KeyTextDiffer — збіг за ключем, але збіг за англійським текстом дав би інший
///     переклад або кілька варіантів. MinorDifference — збіг за ключем, де англійські
///     тексти відрізняються лише дрібницею (див. IsMinorDifference).
/// EN: A matching report row (see BuildReport).
///     KeyTextDiffer — a key match, but an English-text match would give a different
///     translation or several variants. MinorDifference — a key match whose English
///     texts differ only by a trifle (see IsMinorDifference).
/// </summary>
public sealed record TransferReportRow(
    string Key,
    TransferMatchKind Kind,
    bool KeyTextDiffer,
    string TargetOriginal,
    string? DonorOriginalByKey,
    string? TranslationByKey,
    IReadOnlyList<string> TranslationsByText,
    bool MinorDifference = false);
