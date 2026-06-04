using System.ComponentModel;
using EaWLocalizationTool.Core.Models;
using EaWLocalizationTool.GUI.Services;

namespace EaWLocalizationTool.GUI.Models;

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
    "NOT USED",         // альтернативний маркер / alternative marker
    "DO NOT TRANSLATE", // явна вказівка / explicit instruction
    "DO NOT USE",       // вирізані репліки / deprecated voice lines
    "TODO"              // незаповнений рядок / unfilled entry
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
            Original.Contains(p, StringComparison.OrdinalIgnoreCase));

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
    /// UA: Встановлює переклад без позначки Modified (для завантаження з файлу).
    ///     Також запускає валідацію.
    /// EN: Sets translation without Modified flag (for loading from file).
    ///     Also runs validation.
    /// </summary>
    public void SetTranslatedSilent(string value)
    {
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