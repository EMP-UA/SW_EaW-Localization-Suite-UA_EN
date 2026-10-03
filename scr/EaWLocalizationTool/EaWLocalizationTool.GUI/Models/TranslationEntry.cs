using System.ComponentModel;
using EaWLocalizationTool.Core;
using EaWLocalizationTool.Core.Models;
using EaWLocalizationTool.GUI.Services;

namespace EaWLocalizationTool.GUI.Models;

/// <summary>
/// UA: Вид позначки вичитки: None — «-» або порожньо (не вичитано), Plus — «+»,
///     Unsure — «+/-», Comment — будь-який інший текст.
/// EN: Kind of review mark: None — "-" or empty (not reviewed), Plus — "+",
///     Unsure — "+/-", Comment — any other text.
/// </summary>
public enum ReviewKind { None, Plus, Unsure, Comment }

/// <summary>
/// UA: ViewModel запису DAT для GUI.
///     Обгортає DatEntry з Core та додає GUI-логіку:
///     INotifyPropertyChanged, редагований переклад, IsTechnical, валідація спецсимволів.
/// EN: DAT record ViewModel for GUI.
///     Wraps DatEntry from Core and adds GUI logic:
///     INotifyPropertyChanged, editable translation, IsTechnical, special char validation.
/// </summary>
public class TranslationEntry : INotifyPropertyChanged
{
    private string _translated = "";
    private bool _modified;
    private bool _hasValidationIssue;
    private string _validationWarning = "";
    private string _reviewStatus = "";

    // ── Core data (не дублюється / not duplicated) ────────────────────────────
    public DatEntry Core { get; }

    public int OriginalIndex => Core.OriginalIndex;
    public string Key => Core.Key;
    public string Original => Core.OriginalText;
    public byte[] RawCrc32 => Core.RawCrc32;
    public byte[] RawKeyLength => Core.RawKeyLength;
    public uint KeyLength => Core.KeyLength;

    public TranslationEntry(DatEntry core) => Core = core;

    // ══════════════════════════════════════════════════════════════════════════
    // ТЕХНІЧНИЙ РЯДОК / TECHNICAL ENTRY
    // ══════════════════════════════════════════════════════════════════════════

    // UA: Технічні префікси — рядок має ПОЧИНАТИСЯ з них.
    // EN: Technical prefixes — entry must START with these.
    private static readonly string[] _techPrefixes =
    [
        "[TBL]",            // масова заглушка порожніх ключів / mass placeholder
        "[["                // заглушки UI Alamo ("[[ CAPTION ]]", "[[ BUTTON ]]")
    ];

    // UA: Технічні фрази — можуть бути В БУДЬ-ЯКОМУ місці рядка 
    //     (навіть після імені персонажа, напр. "Boba Fett: DO NOT USE THIS LINE").
    // EN: Technical phrases — can be ANYWHERE in the string 
    //     (even after character name, e.g. "Boba Fett: DO NOT USE THIS LINE").
    private static readonly string[] _techContains =
    [
        "UNUSED",           // "UNUSED PROLOG LINE", "Boba Fett: UNUSED" тощо / etc.
        "PLACEHOLDER",      // загальна заглушка / generic placeholder
        "DO NOT USE",       // вирізані репліки / deprecated voice lines
        "DO NOT DISPLAY"    // вказівка приховування тексту / text display block instruction
    ];

    /// <summary>
    /// UA: True якщо рядок є технічним і НЕ потребує перекладу.
    ///
    ///     Правила визначення (перевіряються послідовно):
    ///
    ///     1. ПОРОЖНІЙ / EMPTY
    ///        Значення null або порожній рядок → технічний.
    ///
    ///     2. БЕЗ ЛІТЕР / NO LETTERS
    ///        Значення складається лише з не-літерних символів:
    ///        пробіли (включно з Unicode \u00A0, \u2003 тощо),
    ///        роздільники "___", "---", "===", "···", невидимі символи.
    ///        char.IsLetter() розуміє всі Unicode літери (латиниця, кирилиця тощо).
    ///
    ///     3. ІМ'Я КЛАВІШІ / KEY NAME  (PREFIX: TEXT_KEY_)
    ///        Записи виду TEXT_KEY_DELETE, TEXT_KEY_NUMPAD_6, TEXT_KEY_TAB тощо.
    ///        Це назви фізичних клавіш, не UI-текст — зазвичай не перекладаються.
    ///        ВИНЯТОК: TEXT_KEYBIND_* (мають речення) та TEXT_KEYBOARD_* (назви розділів)
    ///        не підпадають під це правило.
    ///
    ///     4. ТЕХНІЧНИЙ ПРЕФІКС / TECHNICAL PREFIX
    ///        Значення починається з відомого префікса розробника (див. _techPrefixes).
    ///        Приклад: "[TBL]", "[[ CAPTION ]]" → технічний.
    ///
    ///     5. ТЕХНІЧНА ФРАЗА / TECHNICAL PHRASE
    ///        Значення містить технічну фразу в будь-якій частині рядка (див. _techContains).
    ///        Приклад: "Boba Fett: DO NOT USE THIS LINE" → технічний.
    ///
    ///     6. ТЕХНІЧНИЙ КЛЮЧ / TECHNICAL KEY
    ///        Жорстко задані ключі, які є маркерами рушія (напр. TEXT_END_OF_DATA).
    ///
    ///     ⚠ УВАГА: переклад технічних рядків може ЗЛАМАТИ гру!
    ///        (crawl-текст, роздільники довідки, форматні блоки)
    ///        Залишайте Translated ПОРОЖНІМ — WriteSafe збереже оригінальні байти.
    ///
    /// EN: True if entry is technical and does NOT need translation.
    ///
    ///     Detection rules (checked in order):
    ///
    ///     1. EMPTY
    ///        Value is null or empty string → technical.
    ///
    ///     2. NO LETTERS
    ///        Value contains only non-letter characters:
    ///        whitespace (including Unicode \u00A0, \u2003 etc.),
    ///        separators "___", "---", "===", "···", invisible chars.
    ///        char.IsLetter() understands all Unicode letters (Latin, Cyrillic etc.).
    ///
    ///     3. KEY NAME  (PREFIX: TEXT_KEY_)
    ///        Entries like TEXT_KEY_DELETE, TEXT_KEY_NUMPAD_6, TEXT_KEY_TAB etc.
    ///        These are physical key names, not UI text — usually not translated.
    ///        EXCEPTION: TEXT_KEYBIND_* (contain sentences) and TEXT_KEYBOARD_*
    ///        (section names) do NOT fall under this rule.
    ///
    ///     4. TECHNICAL PREFIX
    ///        Value starts with a known developer prefix (see _techPrefixes).
    ///        Example: "[TBL]", "[[ CAPTION ]]" → technical.
    ///
    ///     5. TECHNICAL PHRASE
    ///        Value contains a technical phrase anywhere in the string (see _techContains).
    ///        Example: "Boba Fett: DO NOT USE THIS LINE" → technical.
    ///
    ///     6. TECHNICAL KEY
    ///        Hardcoded keys that act as engine markers (e.g. TEXT_END_OF_DATA).
    ///
    ///     ⚠ WARNING: translating technical entries may BREAK the game!
    ///        (crawl text, help separators, format blocks)
    ///        Keep Translated EMPTY — WriteSafe will preserve original bytes.
    /// </summary>
    public bool IsTechnical =>
        // UA: Правило 1 — порожній / EN: Rule 1 — empty
        string.IsNullOrEmpty(Original)
        // UA: Правило 2 — немає літер (пробіли, роздільники, невидимі символи)
        // EN: Rule 2 — no letters (whitespace, separators, invisible chars)
        || !Original.Any(char.IsLetter)
        // UA: Правило 3 — ім'я клавіші (TEXT_KEY_, але НЕ TEXT_KEYBIND_ чи TEXT_KEYBOARD_)
        // EN: Rule 3 — key name (TEXT_KEY_, but NOT TEXT_KEYBIND_ or TEXT_KEYBOARD_)
        || (Key.StartsWith("TEXT_KEY_", StringComparison.OrdinalIgnoreCase)
            && !Key.StartsWith("TEXT_KEYBIND_", StringComparison.OrdinalIgnoreCase)
            && !Key.StartsWith("TEXT_KEYBOARD_", StringComparison.OrdinalIgnoreCase))
        // UA: Правило 4 — починається з технічного префіксу (напр. "[TBL]")
        // EN: Rule 4 — starts with technical prefix
        || _techPrefixes.Any(p =>
            Original.StartsWith(p, StringComparison.OrdinalIgnoreCase))
        // UA: Правило 5 — містить технічну фразу в будь-якому місці (напр. після імені "Boba Fett: DO NOT USE")
        // EN: Rule 5 — contains technical phrase anywhere
        || _techContains.Any(p =>
            Original.Contains(p, StringComparison.OrdinalIgnoreCase))
        // UA: Правило 6 — жорстко задані технічні ключі (маркер кінця файлу)
        // EN: Rule 6 — hardcoded technical keys (end of file marker)
        || Key.Equals("TEXT_END_OF_DATA", StringComparison.OrdinalIgnoreCase);

    // ══════════════════════════════════════════════════════════════════════════
    // ПЕРЕКЛАД / TRANSLATION
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// UA: Перекладений текст. Зміна позначає запис як Modified і запускає валідацію.
    /// EN: Translated text. Changing marks as Modified and triggers validation.
    /// </summary>
    public string Translated
    {
        get => _translated;
        set
        {
            if (_translated == value) return;
            if (!TextGuard.IsClean(value))
            {
                RejectedCount++;
                OnPropertyChanged(nameof(Translated));
                return;
            }
            _translated = value;
            _modified = true;
            OnPropertyChanged(nameof(Translated));
            OnPropertyChanged(nameof(IsModified));
            OnPropertyChanged(nameof(IsTranslated));
            RunValidation();
        }
    }

    public bool IsModified => _modified;
    public bool IsTranslated => !string.IsNullOrEmpty(_translated);

    /// <summary>
    /// UA: Кількість текстів, відхилених перевіркою набору символів (з початку роботи програми).
    /// EN: Number of texts rejected by the character set check (since the application started).
    /// </summary>
    public static int RejectedCount { get; private set; }

    /// <summary>
    /// UA: Встановлює переклад без позначки Modified (для завантаження з файлу).
    ///     Також запускає валідацію.
    /// EN: Sets translation without Modified flag (for loading from file).
    ///     Also runs validation.
    /// </summary>
    public void SetTranslatedSilent(string value)
    {
        if (!TextGuard.IsClean(value))
        {
            RejectedCount++;
            return;
        }
        _translated = value;
        _modified = false;
        OnPropertyChanged(nameof(Translated));
        OnPropertyChanged(nameof(IsModified));
        OnPropertyChanged(nameof(IsTranslated));
        RunValidation();
    }

    /// <summary>
    /// UA: Очищає переклад — WriteSafe використає оригінальні байти.
    ///     Використовується для технічних рядків та відміни змін.
    /// EN: Clears translation — WriteSafe will use original bytes.
    ///     Used for technical entries and reverting changes.
    /// </summary>
    public void ClearTranslation()
    {
        _translated = "";
        _modified = false;
        _hasValidationIssue = false;
        _validationWarning = "";
        OnPropertyChanged(nameof(Translated));
        OnPropertyChanged(nameof(IsModified));
        OnPropertyChanged(nameof(IsTranslated));
        OnPropertyChanged(nameof(HasValidationIssue));
        OnPropertyChanged(nameof(ValidationWarning));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ВИЧИТКА / REVIEW
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// UA: Позначка для рядків, які ще не вичитували.
    /// EN: The mark for rows that have not been reviewed yet.
    /// </summary>
    public const string NotReviewedMark = "-";

    /// <summary>
    /// UA: Статус вичитки — вільний текст: «+» (вичитано), «+/-» (вичитано, є сумнів),
    ///     «-» (не вичитано) або довільний коментар до рядка. Це метадані GUI: у DAT
    ///     вони НЕ записуються, а зберігаються лише в робочому TSV (колонка ReviewStatus).
    ///     Не впливає на IsModified: вичитка не змінює текст гри.
    /// EN: Review status — free text: "+" (reviewed), "+/-" (reviewed, in doubt),
    ///     "-" (not reviewed) or an arbitrary per-row comment. GUI metadata: it is NOT
    ///     written to the DAT and is kept only in the working TSV (ReviewStatus column).
    ///     Does not affect IsModified: a review mark does not change game text.
    /// </summary>
    public string ReviewStatus
    {
        get => _reviewStatus;
        set
        {
            // UA: Значення не обрізається в сеттері: поле вводу пише сюди на кожне натискання
            //     клавіші, і зміна тексту під курсором ламала б набір пробілів.
            // EN: The value is not trimmed in the setter: the input field writes here on every
            //     keystroke, and altering the text under the caret would swallow typed spaces.
            value ??= "";
            if (_reviewStatus == value) return;
            _reviewStatus = value;
            OnPropertyChanged(nameof(ReviewStatus));
            OnPropertyChanged(nameof(ReviewMarkKind));
            OnPropertyChanged(nameof(WasReviewed));
            OnPropertyChanged(nameof(IsReviewCompleted));
        }
    }

    /// <summary>
    /// UA: Вид позначки вичитки (порожній текст і «-» — None).
    /// EN: Kind of the review mark (empty text and "-" are None).
    /// </summary>
    public ReviewKind ReviewMarkKind => _reviewStatus.Trim() switch
    {
        "" or NotReviewedMark => ReviewKind.None,
        "+" => ReviewKind.Plus,
        "+/-" => ReviewKind.Unsure,
        _ => ReviewKind.Comment
    };

    /// <summary>
    /// UA: True, якщо вичитувач уже торкався рядка: позначка — будь-що, крім «-» та порожнього
    ///     тексту. За цим критерієм рядок захищений від перезапису при перенесенні
    ///     перекладу, а його статус вмикає автозбереження.
    /// EN: True when a reviewer has touched the row: the mark is anything except "-" and
    ///     empty text. This is the criterion that protects a row from being overwritten
    ///     during a translation transfer, and its status triggers autosave.
    /// </summary>
    public bool WasReviewed => ReviewMarkKind != ReviewKind.None;

    /// <summary>
    /// UA: True, якщо рядок вичитано: позначка точно «+» або «+/-».
    /// EN: True when the row is reviewed: the mark is exactly "+" or "+/-".
    /// </summary>
    public bool IsReviewCompleted => ReviewMarkKind is ReviewKind.Plus or ReviewKind.Unsure;

    /// <summary>
    /// UA: True, якщо поле вичитки заповнене (у т.ч. «-»); такі статуси потрапляють у робочий TSV.
    /// EN: True when the review field is filled (including "-"); such statuses go to the working TSV.
    /// </summary>
    public bool HasReviewMark => !string.IsNullOrWhiteSpace(_reviewStatus);

    /// <summary>
    /// UA: Ставить «-» нетехнічному рядку з порожнім статусом вичитки.
    ///     Технічні рядки вичитці не підлягають і лишаються без статусу.
    /// EN: Sets "-" on a non-technical row whose review status is empty.
    ///     Technical rows are not reviewed and stay without a status.
    /// </summary>
    public void EnsureReviewMark()
    {
        if (!HasReviewMark && !IsTechnical)
            ReviewStatus = NotReviewedMark;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ДУБЛІКАТИ / DUPLICATES
    // ══════════════════════════════════════════════════════════════════════════

    private int _duplicateCount;
    private bool _duplicateDiffer;
    private int _similarCount;
    private string _duplicateTip = "";

    /// <summary>
    /// UA: Кількість нетехнічних рядків із таким самим англійським текстом (разом із
    ///     цим); 0 — дубліката немає. Заповнюється зовні після зміни набору рядків
    ///     або перекладів.
    /// EN: The number of non-technical rows with the same English text (including
    ///     this one); 0 — no duplicates. Filled from outside after rows or
    ///     translations change.
    /// </summary>
    public int DuplicateCount => _duplicateCount;

    /// <summary>UA: Рядок має дублікати. / EN: The row has duplicates.</summary>
    public bool IsDuplicate => _duplicateCount >= 2;

    /// <summary>
    /// UA: У групі (точні дублікати разом зі схожими рядками) переклади різняться за
    ///     змістом: у точних дублікатів — будь-яка різниця, крім пробілів і переносів на
    ///     краях; між схожими рядками (оригінали різняться регістром чи пробілами) —
    ///     різниця, більша за регістр і пробіли. Порожній переклад теж вважається відмінним.
    /// EN: Within the group (exact duplicates together with similar rows) the translations
    ///     differ in meaning: any difference among exact duplicates except edge whitespace
    ///     and line breaks; among similar rows (originals differing in case or whitespace)
    ///     a difference beyond case and whitespace. An empty translation counts as different too.
    /// </summary>
    public bool DuplicateDiffer => _duplicateDiffer;

    /// <summary>
    /// UA: Кількість ІНШИХ рядків, англійський текст яких відрізняється лише регістром
    ///     чи пробілами (на краях і всередині); це різні рядки гри, а не дублікати.
    /// EN: The number of OTHER rows whose English text differs only in case or
    ///     whitespace (at the ends and inside); these are different game strings,
    ///     not duplicates.
    /// </summary>
    public int SimilarCount => _similarCount;

    /// <summary>
    /// UA: Підказка до колонки: кількість точних і схожих рядків, у чому саме вони
    ///     відрізняються (регістр, пробіл чи перенос на початку / в кінці) і чи різні переклади.
    /// EN: The column's tooltip: the number of exact and similar rows, exactly how they
    ///     differ (case, a space or line break at the start / end) and whether the
    ///     translations differ.
    /// </summary>
    public string DuplicateTip => _duplicateTip;

    /// <summary>UA: Є схожі рядки (інший регістр чи пробіли). / EN: Has similar rows (different case or whitespace).</summary>
    public bool HasSimilar => _similarCount > 0;

    /// <summary>
    /// UA: Підпис для колонки: «×3» — точні дублікати, «≈2» — схожі рядки (інший
    ///     регістр чи пробіли); обидва можуть бути разом; порожньо — немає.
    /// EN: Column label: "×3" — exact duplicates, "≈2" — similar rows (different case
    ///     or whitespace); both can appear together; empty — none.
    /// </summary>
    public string DuplicateLabel =>
        (IsDuplicate ? $"×{_duplicateCount}" : "") +
        (IsDuplicate && HasSimilar ? " " : "") +
        (HasSimilar ? $"≈{_similarCount}" : "");

    /// <summary>
    /// UA: Задає відомості про групи дублікатів і схожих рядків; сповіщає лише про зміну.
    /// EN: Sets the details of the duplicate and similar groups; notifies only on change.
    /// </summary>
    public void SetDuplicateInfo(int count, bool differ, int similar, string tip)
    {
        if (_duplicateCount == count && _duplicateDiffer == differ &&
            _similarCount == similar && _duplicateTip == tip) return;
        _duplicateCount = count;
        _duplicateDiffer = differ;
        _similarCount = similar;
        _duplicateTip = tip;
        OnPropertyChanged(nameof(DuplicateTip));
        OnPropertyChanged(nameof(DuplicateCount));
        OnPropertyChanged(nameof(IsDuplicate));
        OnPropertyChanged(nameof(DuplicateDiffer));
        OnPropertyChanged(nameof(SimilarCount));
        OnPropertyChanged(nameof(HasSimilar));
        OnPropertyChanged(nameof(DuplicateLabel));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ВАЛІДАЦІЯ / VALIDATION
    // ══════════════════════════════════════════════════════════════════════════

    public bool HasValidationIssue
    {
        get => _hasValidationIssue;
        private set
        {
            if (_hasValidationIssue == value) return;
            _hasValidationIssue = value;
            OnPropertyChanged(nameof(HasValidationIssue));
        }
    }

    public string ValidationWarning
    {
        get => _validationWarning;
        private set
        {
            if (_validationWarning == value) return;
            _validationWarning = value;
            OnPropertyChanged(nameof(ValidationWarning));
        }
    }

    /// <summary>
    /// UA: Перезапускає валідацію. Технічні рядки та порожні переклади не валідуються.
    /// EN: Re-runs validation. Technical entries and empty translations are not validated.
    /// </summary>
    public void Revalidate()
    {
        if (IsTechnical || !IsTranslated)
        {
            HasValidationIssue = false;
            ValidationWarning = "";
            return;
        }
        var (hasIssue, desc) = ValidationService.Check(Original, Translated);
        HasValidationIssue = hasIssue;
        ValidationWarning = desc;
    }

    private void RunValidation() => Revalidate();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}