# Changelog

Історія змін релізів `EaWLocalizationTool.GUI` та `EaWTextureConverter`.
Release history of `EaWLocalizationTool.GUI` and `EaWTextureConverter`.

Формат базується на [Keep a Changelog](https://keepachangelog.com/uk/1.1.0/).
Based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.1.0] — 2026-10-03

### UA: Додано

**EaWLocalizationTool.GUI**
- Кнопка «⇄ Перенесення» в шапці, доступна одразу після запуску: вікно з чотирма файлами —
  оригінал і переклад основної гри (Empire at War), оригінал і переклад доповнення (Forces of
  Corruption); замість двох останніх можна взяти доповнення, відкрите в програмі. Переклад
  основної гри — DAT або TSV (TSV з колонкою `OriginalText` не потребує оригінального DAT)
- Індекс «ключ → англійський текст → переклад» будується в пам'яті й не перечитується, доки
  файли не змінилися
- Попередній аналіз у вікні перенесення: збіг за ключем, збіг лише за англійським текстом,
  конфлікти, англійський текст інший, без відповідника, ключі основної гри, яких немає в
  доповненні, і кількість збігів за ключем, де збіг за англійським текстом дав би інший переклад
- Кнопка «Звіт збігів»: зберігає `transfer\transfer_report {yyMMdd HHmm}.tsv` (усі нетехнічні
  рядки доповнення з видом збігу, перекладом за ключем, варіантами за англійським текстом і
  прапорцем розбіжності) і відкриває його у вікні лише для перегляду: фільтри з лічильниками,
  пошук, копіювання комірок і рядків (Ctrl+C, контекстне меню), «Відкрити теку»
- Зіставлення рядків: за ключем при збігу англійського тексту (без урахування регістру й
  пробілів), далі за англійським текстом — спершу за точним (регістр і пробіли значущі), потім
  за нормалізованим; ключ, що є в донорі, але має інший англійський
  текст у доповненні, не переноситься
- Вікно вибору варіанта для англійського тексту, який має кілька різних перекладів у
  донорі; рішення за замовчуванням — пропустити
- Захист від перезапису за позначкою вичитки: рядок із позначкою, відмінною від `-`, не
  змінюється; технічні рядки не змінюються ніколи; змінені рядки отримують статус «Змінено»
- Підсумок перенесення: збіги за ключем / за текстом / конфлікти, перенесено, без змін,
  пропущено вичитаних, англійський текст відрізняється, без відповідника
- Виділення кількох рядків таблиці (Ctrl/Shift + клік); ПКМ на вже виділеному рядку
  зберігає виділення
- Колонка «Вичитка»: редагований список із позначками `+` (вичитано), `+/-` (є сумнів),
  `-` (не вичитано) або довільний коментар; сортування за колонкою
- Позначка `-` ставиться автоматично всім нетехнічним рядкам без статусу; порожнє поле
  вичитки повертається до `-`
- Контекстне меню «Вичитка для виділених»: `+`, `+/-`, `-`, власний текст
  (`ReviewMarkPromptWindow`) — для всіх виділених нетехнічних рядків одразу
- Другий ряд фільтрів вичитки: Усі / `+` / `+/-` / `-` / Коментар, з кількістю рядків;
  поєднується з основним фільтром і пошуком
- Лічильник вичитаних (`+` та `+/-`) у шапці та смузі прогресу (кількість і відсоток від
  нетехнічних рядків); пошук охоплює й статус вичитки
- Колонка `ReviewStatus` у TSV-експорті; під час завантаження TSV як джерела перекладу
  статуси вичитки відновлюються
- Теки `original\`, `translated\`, `work\`, `transfer\` створюються під час запуску.
  `original` і `translated` повторюють структуру гри (`GameData\Data\Text`,
  `corruption\Data\Text`), тож файли копіюються з гри й у гру без зміни шляхів; `work` має
  підтеки `GameData`, `corruption`; файли поза цими іграми йдуть у підтеку `other`. Діалоги
  відкриття й збереження починаються в цих теках, ручний вибір файлу зберігається; вікно
  перенесення бере найновіші DAT із них
- Мітка дати й часу `yyMMdd HHmm` в іменах збережених DAT, парних TSV і звітів
- Парний TSV зі статусами вичитки зберігається поруч із кожним збереженим DAT
- Робочий TSV `work\{гра}\{ім'я} [{хеш ключів}].tsv` поруч із програмою: створюється під
  час відкриття перекладу (попередня версія зберігається як `.bak`), оновлюється під час
  збереження DAT та автозбереження; хеш відрізняє основну гру від доповнення
- Під час відкриття перекладу статуси вичитки беруться з відкритого TSV, а за його відсутності
  — з наявного робочого файлу
- Автозбереження спрацьовує й тоді, коли змінено лише статуси вичитки
- Фільтри дублікатів: «Дублікати» (рядки з точно однаковим англійським текстом) і «Дубл.
  різні» (переклади в групі відрізняються або частина не перекладена); «Схожі» — рядки, що
  відрізняються лише регістром чи пробілами; колонка «Дубл.» з кількістю рядків у групі
  («×3») і схожих («≈2»); підказка до колонки пояснює, чим саме відрізняються схожі рядки
  (регістр, пробіл чи перенос на початку або в кінці) і чи різні переклади; «Дубл. різні»
  враховує й схожі рядки: у точних дублікатів різниця — будь-яка, крім пробілів і переносів
  на краях, між схожими рядками — лише більша за регістр і пробіли («Argh!» / «argh!» →
  «Аргх!» / «аргх!» різницею не є); бурштиновий
  колір — переклади відрізняються
- Вікно перенесення: відкритий у програмі файл стає ціллю за замовчуванням лише тоді, коли це
  доповнення; файл основної гри не може бути ціллю; перенесення блокується, якщо майже всі
  ключі цілі збігаються з основною грою
- Пункт контекстного меню «Застосувати до всіх дублікатів»: копіює переклад рядка в усі рядки
  з таким самим англійським текстом; вичитані рядки за замовчуванням пропускаються, за
  згодою змінюються, і їхні «+» та «+/-» скидаються на «-»
- Збіг за ключем із дрібною відмінністю тексту: якщо ключ є в основній грі, а англійські тексти
  відрізняються лише одруком, зайвою чи відсутньою літерою або розділовим знаком, переклад
  переноситься за ключем без підтвердження. Умови (усі разом): текст від 20 символів, змінений
  фрагмент не довший за 3 символи і не більший за 5% довжини (після відкидання спільного
  початку й кінця нормалізованих текстів), цифри однакові, різниця не лише в дефісі чи пробілі
  (інше написання назви, наприклад «Abregado-rae» / «Abregado Rae»). Відмінності у словах і числах під це не підпадають і йдуть
  на підтвердження
- Вікно підтвердження перенесення: рядки, у яких ключ є в основній грі, але англійський текст за
  ним інший, показуються перед записом: англійський текст основної гри й доповнення, поточний
  переклад доповнення, переклад основної гри за ключем і за англійською фразою. Колонка
  «Результат» відображається й редагується, як переклад у головному вікні (перенос рядків,
  2× клік — багаторядкове поле). Типово рядок зі знайденою фразою отримує переклад за фразою,
  рядок без неї лишає поточний переклад. Кнопки для виділених рядків: «зрівняти з основною грою
  за ключем» і «за фразою». Контекстне меню комірок: копіювати значення, внести в результат,
  додати до результату, внести всі варіанти, застосувати результат до рядків з таким самим
  англійським текстом. Результат із кількома готовими варіантами не записується;
  «Скасувати перенесення» нічого не змінює
- Результат перенесення записується в НОВИЙ DAT із міткою часу в теці перекладу доповнення
  (`translated\corruption\Data\Text\{ім'я} {yyMMdd HHmm}.dat`, за збігу назви — суфікс ` (2)`);
  поруч у `work` — парний TSV; наявні DAT не перезаписуються, попередня версія робочого TSV
  лишається як `.bak`
- Фільтри вікна звіту названо за суттю: «Усі рядки», «Збіг за ключем», «Дрібна відмінність
  тексту», «Та сама фраза в основній грі», «Новий ключ, кілька перекладів», «Ключ є, текст
  змінено, фрази немає», «Піде на підтвердження», «Лише в доповненні», «Переклад за ключем ≠ за
  фразою»; під фільтрами — пояснення активного фільтру; у верхній примітці — кількість рядків,
  які підуть на підтвердження
- Пункт контекстного меню «Застосувати до схожих з урахуванням регістру»: копіює переклад у рядки
  з тим самим англійським текстом, але іншим регістром (`cr` / `Cr` / `CR`); регістр перекладу
  підлаштовується під оригінал цілі (нижній, ВЕРХНІЙ, як у реченні, Кожне Слово); для оригіналів
  зі змішаним регістром (`Communications Array destroyed`) змінюються лише перші літери слів за
  позиціями, якщо кількість слів в оригіналі та перекладі однакова; краї беруться з оригіналу цілі;
  вичитані й технічні рядки пропускаються; перед застосуванням показується кількість і до 5 прикладів
- Кнопка «↔ Вирівняти краї»: робить пробіли й переноси на початку та в кінці перекладу такими,
  як в оригіналі, для всіх перекладених нетехнічних рядків; текст між краями не змінюється,
  позначки вичитки зберігаються
- Кнопка «⌂ На початок»: закриває відкриті файли й повертає стартовий екран; за незбережених
  змін пропонує зберегти DAT
- Картка «③ Вихід» клікабельна й виконує «Зберегти DAT»
- Вікно перенесення автоматично підхоплює найновіші DAT зі стандартних тек
- Фільтр «Проблемні» перевіряє також `\r` (повернення каретки), модифікатори `{…}`
  (вміст і кількість дужок) та пробіли й переноси на початку й у кінці рядка (мають збігатися
  з оригіналом)

**Збірка та поширення**
- Workflow GitHub Actions `.github/workflows/release.yml`: за тегом `vX.Y.Z` (або вручну для
  наявного тега) збирає `EaWLocalizationTool.GUI` та `EaWTextureConverter` у трьох варіантах —
  `win-x64`, `win-x86` (самодостатні) та `generic` (потребує .NET 10 Desktop Runtime) —
  і публікує GitHub Release
- Опис релізу береться з розділу `## [X.Y.Z]` цього файлу; до релізу додається
  `SHA256SUMS.txt`
- До релізу додається підтвердження походження збірки (GitHub build provenance attestation):
  `gh attestation verify <архів> --repo EMP-UA/SW_EaW-Localization-Suite-UA_EN`
- Завантаження архівів `generic` на Nexus Mods як нової версії наявних файлів (завдання `nexus`;
  вмикається змінними репозиторію `NEXUS_*_FILE_ID` та секретом `NEXUSMODS_API_KEY`)
- `.gitattributes`: нормалізація кінців рядків
- Тека `docs/`: описи редактора, перенесення перекладу, вичитки й релізів (UA + EN)

**EaWTextureConverter**
- Нові варіанти поставки: `win-x64`, `win-x86` (самодостатні) та `generic`; функціонал
  не змінено; версія збірки збігається з тегом релізу

### UA: Змінено
- Контекстне меню рядка діє на рядок, на якому натиснуто ПКМ
- Лічильники, смуга прогресу та фільтри оновлюються одразу після завершення редагування
  комірки
- Режим виділення таблиці — `Extended` (раніше `Single`)
- Текст на акцентних (фіолетових) кнопках світлий в обох темах

---

### EN: Added

**EaWLocalizationTool.GUI**
- "⇄ Transfer" header button, available right after startup: a window with four files —
  the base game's (Empire at War) original and translation, the expansion's (Forces of
  Corruption) original and translation; instead of the last two the expansion open in the
  application can be used. The base game's translation is a DAT or a TSV (a TSV with an
  `OriginalText` column does not need the original DAT)
- The "key → English text → translation" index is built in memory and is not reread until the
  files change
- A dry-run analysis in the transfer window: match by key, match by English text only,
  conflicts, English text differs, no match, base game keys absent from the expansion, and the
  number of key matches where an English-text match would give a different translation
- A "Match report" button: saves `transfer\transfer_report {yyMMdd HHmm}.tsv` (every
  non-technical expansion row with the match kind, the translation by key, the variants by
  English text and the discrepancy flag) and opens it in a viewer-only window: filters with
  counters, search, copying of cells and rows (Ctrl+C, context menu), "Open folder"
- Row matching: by key when the English text is equal (ignoring case and whitespace), then
  by English text — the exact text first (case and whitespace significant), then the
  normalized one; a key that exists in the donor but carries a different English text in the
  expansion is not transferred
- A choice window for English text that has several distinct translations in the donor;
  the default decision is to skip
- Overwrite protection based on the review mark: a row whose mark is anything but `-` is not
  changed; technical rows are never changed; changed rows become "Modified"
- Transfer summary: matches by key / by text / conflicts, transferred, unchanged, reviewed rows
  skipped, English text differs, no match
- Multi-row selection in the table (Ctrl/Shift + click); a right click on an already
  selected row keeps the selection
- "Review" column: an editable list with the marks `+` (reviewed), `+/-` (in doubt),
  `-` (not reviewed) or a free-form comment; sortable
- The `-` mark is set automatically on every non-technical row without a status; an emptied
  review field returns to `-`
- "Review for selected" context menu: `+`, `+/-`, `-`, custom text
  (`ReviewMarkPromptWindow`) — applied to all selected non-technical rows at once
- A second row of review filters: All / `+` / `+/-` / `-` / Comment, with row counts;
  combined with the main filter and the search
- A counter of reviewed rows (`+` and `+/-`) in the header and the progress bar (count and
  percentage of non-technical rows); search also covers the review status
- A `ReviewStatus` column in the TSV export; review statuses are restored when a TSV is
  loaded as the translation source
- `original\`, `translated\`, `work\` and `transfer\` folders are created at startup.
  `original` and `translated` mirror the game's structure (`GameData\Data\Text`,
  `corruption\Data\Text`), so files are copied from and into the game without changing paths;
  `work` has the `GameData` and `corruption` subfolders; files outside these games go to the
  `other` subfolder. Open and save dialogs start in these folders, manual file picking is
  kept; the transfer window takes the newest DATs from them
- A `yyMMdd HHmm` date-time stamp in the names of saved DATs, paired TSVs and reports
- A paired TSV with review statuses is saved next to every saved DAT
- A working TSV `work\{game}\{name} [{key hash}].tsv` next to the executable: created when a
  translation is opened (the previous version is kept as `.bak`), updated on DAT save and
  autosave; the hash tells the base game from the expansion
- When a translation is opened, review statuses come from the opened TSV, or from an existing
  working file when the TSV has none
- Autosave also triggers when only review statuses changed
- Duplicate filters: "Duplicates" (rows with exactly the same English text) and "Dup. differ"
  (translations within the group differ or some are untranslated); "Similar" — rows that
  differ only in case or whitespace; a "Dup" column with the group size ("×3") and the number
  of similar rows ("≈2"); the column's tooltip explains exactly how similar rows differ (case,
  a space or line break at the start or end) and whether the translations differ; "Dup.
  differ" also covers similar rows: among exact duplicates any difference except edge
  whitespace and line breaks counts, among similar rows only a difference beyond case and
  whitespace ("Argh!" / "argh!" → "Аргх!" / "аргх!" is not one); amber when the translations differ
- Transfer window: the file open in the application is the target by default only when it is
  the expansion; the base game's file cannot be the target; the transfer is blocked when almost
  all of the target's keys match the base game
- A context-menu item "Apply to all duplicates": copies the row's translation into every row
  with the same English text; reviewed rows are skipped by default, changed on consent, and
  their "+" and "+/-" reset to "-"
- Key match with a minor text difference: when the key exists in the base game and the English
  texts differ only by a typo, an extra or missing letter or a punctuation mark, the translation
  is taken by key without a confirmation. Conditions (all together): a text of at least 20
  characters, a changed fragment of at most 3 characters and at most 5% of the length (after the
  common beginning and ending of the normalized texts are discarded), the same digits, and the
  difference is not merely a hyphen or a space (another spelling of a name, e.g. "Abregado-rae" / "Abregado Rae"). Differences in words and
  numbers do not qualify and go to the confirmation
- Transfer review window: rows whose key exists in the base game but whose English text under
  that key differs are shown before writing: the base game's and the expansion's English text,
  the expansion's current translation, and the base game's translation by key and by English
  phrase. The "Result" column is shown and edited like a translation in the main window (wrapped
  text, a double-click opens a multi-line box). By default a row with a found phrase gets the
  translation by phrase, a row without one keeps its current translation. Buttons for the
  selected rows: "match the base game by key" and "by phrase". The cell context menu: copy the
  value, put it into the result, append it to the result, insert all variants, apply the result
  to rows with the same English text. A result that still holds several ready-made variants is
  not written; "Cancel transfer" changes
  nothing
- The transfer result is written to a NEW time-stamped DAT in the expansion's translation folder
  (`translated\corruption\Data\Text\{name} {yyMMdd HHmm}.dat`, a ` (2)` suffix on a name clash);
  a paired TSV goes to `work` beside it; existing DATs are never overwritten, the previous
  version of the working TSV stays as `.bak`
- The match report's filters are named by their meaning: "All rows", "Matched by key", "Minor
  text difference", "Same phrase in base game", "New key, several translations", "Key found,
  text changed, no phrase", "Goes to review", "Expansion only", "Key vs phrase translation
  differ"; an explanation of the active filter sits under the filters; the note at the top
  gives the number of rows that will go to the review
- A context-menu item "Apply to similar rows, case-aware": copies the translation into rows with
  the same English text in a different letter case (`cr` / `Cr` / `CR`); the translation's case
  is adapted to the target original (lower, UPPER, sentence, Title Case); for mixed-case
  originals (`Communications Array destroyed`) only the first letters of words change by
  position, provided the original and the translation have the same number of words; the edges
  follow the target original; reviewed and technical rows are skipped; the count and up to 5
  examples are shown before applying
- An "↔ Align edges" button: makes the whitespace and line breaks at the start and end of the
  translation match the original, for all translated non-technical rows; the text between the
  edges is not changed, review marks are kept
- A "⌂ Start" button: closes the open files and returns the start screen; with unsaved
  changes it offers to save the DAT
- Card "③ Output" is clickable and performs "Save DAT"
- The transfer window automatically picks up the newest DATs from the standard folders
- The "Issues" filter also checks `\r` (carriage return), `{…}` modifiers (content and
  brace count) and whitespace and line breaks at the start and end of the string (must match
  the original)

**Build and distribution**
- GitHub Actions workflow `.github/workflows/release.yml`: on a `vX.Y.Z` tag (or manually
  for an existing tag) it builds `EaWLocalizationTool.GUI` and `EaWTextureConverter` in three
  variants — `win-x64`, `win-x86` (self-contained) and `generic` (requires the .NET 10
  Desktop Runtime) — and publishes a GitHub Release
- Release notes come from the `## [X.Y.Z]` section of this file; `SHA256SUMS.txt` is
  attached to the release
- A build provenance attestation (GitHub build provenance attestation) is attached to the release:
  `gh attestation verify <archive> --repo EMP-UA/SW_EaW-Localization-Suite-UA_EN`
- Uploading the `generic` archives to Nexus Mods as a new version of existing files (the `nexus`
  job; enabled by the `NEXUS_*_FILE_ID` repository variables and the `NEXUSMODS_API_KEY` secret)
- `.gitattributes`: line-ending normalization
- A `docs/` folder: descriptions of the editor, the translation transfer, review and releases
  (UA + EN)

**EaWTextureConverter**
- New distribution variants: `win-x64`, `win-x86` (self-contained) and `generic`;
  functionality unchanged; the build version follows the release tag

### EN: Changed
- The row context menu acts on the row that was right-clicked
- Counters, the progress bar and filters refresh immediately after a cell edit ends
- The table's selection mode is `Extended` (was `Single`)
- Text on accent (purple) buttons is light in both themes

## [EaWTextureConverter 1.0.0] — 2026-06-07

### UA: Додано
- `EaWTextureConverter` — WPF-інструмент пакетної конвертації DDS-текстур для модифікацій
  Star Wars: Empire at War
- DDS → PNG: декодування DXT1, DXT3 та DXT5 через Magick.NET зі збереженням альфа-каналу
- PNG → DDS: запис нестиснутих даних BGRA (32 bpp), сумісних із DirectX 9 та рушієм Alamo
- Автоматичне визначення кореня модифікації за структурою `Data\Art`
- Відтворення відносної структури підтек у вихідній теці
- Стандартні шляхи текстур гри: `Data\Art\Textures` та `Data\patch2\DATA\ART\TEXTURES`
- Темна й світла теми, двомовний інтерфейс (UA/EN)
- Без сторонніх консольних утиліт: лише вбудовані NuGet-залежності
- Вимоги: Windows 10/11 x64, .NET 10 Desktop Runtime

---

### EN: Added
- `EaWTextureConverter` — a WPF tool for batch DDS texture conversion in Star Wars:
  Empire at War mods
- DDS → PNG: decoding of DXT1, DXT3 and DXT5 through Magick.NET, alpha channel preserved
- PNG → DDS: writing uncompressed BGRA (32 bpp) data compatible with DirectX 9 and the Alamo
  engine
- Automatic detection of the mod root by the `Data\Art` structure
- The relative subfolder structure is recreated in the output folder
- Standard game texture paths: `Data\Art\Textures` and `Data\patch2\DATA\ART\TEXTURES`
- Dark and light themes, bilingual UI (UA/EN)
- No external command-line tools: only integrated NuGet dependencies
- Requirements: Windows 10/11 x64, .NET 10 Desktop Runtime

## [1.0.2] — 2026-06-06

### UA: Змінено
- `EaWLocalizationTool.GUI`: визначення технічних рядків враховує фразу `DO NOT DISPLAY`
- `EaWLocalizationTool.GUI`: маркер `TEXT_END_OF_DATA` завжди технічний і не підлягає
  випадковому перекладу

---

### EN: Changed
- `EaWLocalizationTool.GUI`: technical entry detection covers the `DO NOT DISPLAY` phrase
- `EaWLocalizationTool.GUI`: the `TEXT_END_OF_DATA` marker is always technical and is protected
  from accidental translation

## [1.0.1] — 2026-06-04

### UA: Додано
- `EaWLocalizationTool.GUI`: автозбереження у `<ім'я>_AUTOSAVE.dat` поруч із програмою (у версії 1.1.0 — у теці `work\{гра}`)
  (інтервал задається у вікні конфігурації, типово 5 хвилин, `0` — вимкнено)
- `EaWLocalizationTool.GUI`: журнал помилок і подій у `logs\app.log` (глобальний обробник винятків)

---

### EN: Added
- `EaWLocalizationTool.GUI`: autosave to `<name>_AUTOSAVE.dat` next to the executable (in 1.1.0 — in `work\{game}`)
  (interval set in the configuration window, 5 minutes by default, `0` disables it)
- `EaWLocalizationTool.GUI`: error and event log in `logs\app.log` (global exception handler)

## [1.0.0] — 2026-06-04

### UA: Додано
- `EaWLocalizationTool.GUI` — WPF-редактор DAT-файлів рушія Alamo на спільній бібліотеці
  `EaWLocalizationTool.Core`
- Завантаження оригінального DAT і джерела перекладу (TSV або DAT)
- Фільтри: Всі / Без перекладу / Перекладено / Змінено / Проблемні / Технічні
- Валідація: `\n`, `%s/%d`, `[теги]`, `<теги>`
- Визначення технічних рядків (роздільники crawl-тексту, заглушки)
- Безпечний запис: CRC32 і ключі копіюються з оригіналу побайтово
- Темна й світла тема, масштабування шрифту
- Експорт у TSV
- Пошук, контекстне меню рядка, скидання сортування до порядку оригінального файлу
- Кнопка «Очистити технічні»: очищає переклад усіх технічних рядків
- Вікно конфігурації для шляхів до файлів
- Виправлено: обробка нульових байтів, збереження технічних роздільників, дублікати ключів

---

### EN: Added
- `EaWLocalizationTool.GUI` — a WPF editor for Alamo engine DAT files, built on the shared
  `EaWLocalizationTool.Core` library
- Loading the original DAT and a translation source (TSV or DAT)
- Filters: All / Untranslated / Translated / Modified / Issues / Technical
- Validation: `\n`, `%s/%d`, `[tags]`, `<tags>`
- Technical entry detection (crawl-text separators, placeholders)
- Safe write: CRC32 and keys are copied from the original byte for byte
- Dark and light theme, font scaling
- TSV export
- Search, a row context menu, resetting the sort to the original file order
- A "Clear Technical" button: clears the translation of all technical rows
- A configuration window for file paths
- Fixed: null-byte handling, preservation of technical separators, duplicate keys
