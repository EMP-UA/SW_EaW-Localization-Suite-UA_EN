# Перенесення перекладу / Translation transfer

[← README](../README.md) · [Вичитка й теки / Review and folders](REVIEW.md) · [Редактор / Editor](EDITOR.md)

Основна гра (Empire at War) → доповнення (Forces of Corruption).
Base game (Empire at War) → expansion (Forces of Corruption).

---

## UA

**UA:** Кнопка **«⇄ Перенесення»** у шапці доступна одразу після запуску. Вікно приймає чотири файли:

| Файл | Призначення |
|---|---|
| ① Основна гра: оригінал | англійський DAT Empire at War |
| ② Основна гра: переклад | перекладений DAT або TSV (TSV з колонкою `OriginalText` обходиться без ①) |
| ③ Доповнення: оригінал | англійський DAT Forces of Corruption |
| ④ Доповнення: переклад | поточний переклад і статуси вичитки (необов'язково), напр. робочий TSV з теки `work` |

Замість ③ і ④ можна взяти доповнення, уже відкрите в програмі. З ① і ② у пам'яті будується
індекс «ключ → англійський текст → переклад»; він зберігається, доки програма відкрита, і не
перечитується, поки файли не змінилися.

Ще до перенесення вікно показує аналіз:

- ключів із перекладом в основній грі та різних англійських текстів;
- збіг **за ключем** (англійський текст однаковий);
- збіг **лише за англійським текстом** (ключ інший або відсутній);
- конфлікти (кілька різних перекладів одного тексту);
- ключ є в основній грі, а англійський текст інший;
- без відповідника;
- ключі основної гри, яких немає в доповненні;
- серед збігів за ключем — рядки, для яких збіг за англійським текстом дав би **інший** переклад.

Кнопка **«Звіт збігів»** зберігає у `transfer\transfer_report {yyMMdd HHmm}.tsv` усі нетехнічні
рядки доповнення й відкриває їх у вікні лише для перегляду: фільтри з лічильниками та поясненням
під ними, пошук, копіювання комірок і рядків (Ctrl+C, контекстне меню), «Відкрити теку».
Колонки: ключ, вид збігу (`Key` / `KeyMinorDiff` / `TextOnly` / `Conflict` / `TextChanged` /
`NoMatch`), прапорець розбіжності, англійський текст основної гри й доповнення, переклад за
ключем та варіанти за англійською фразою. У примітці вгорі вікна показано, скільки рядків
підуть на підтвердження.

Зіставлення рядків:

1. **За ключем**, якщо англійський текст у донорі й у доповненні збігається (без урахування
   регістру та пробілів). Ключ задає контекст, тому це найнадійніший збіг.
2. **За англійським текстом** — для ключів, яких немає в донорі. Спершу шукається **точний**
   текст (з урахуванням регістру й пробілів), і лише за його відсутності — нормалізований.
   Тож «Fall out» і «Fall out » (з пробілом) отримують кожен свій переклад. Якщо донор має один
   переклад цього тексту, він переноситься; якщо кілька різних, відкривається вікно вибору
   варіанта.
3. **Дрібна відмінність.** Якщо ключ є в основній грі, а англійські тексти відрізняються лише
   дрібницею, що не змінює змісту (одрук, зайва чи відсутня літера або розділовий знак),
   переклад переноситься за ключем без підтвердження. Умови (усі разом): текст від 20 символів,
   змінений фрагмент не довший за 3 символи і не більший за 5% довжини (після відкидання
   спільного початку й кінця нормалізованих текстів), цифри однакові, різниця не лише в дефісі
   чи пробілі (інше написання назви, напр. «Abregado-rae» / «Abregado Rae»). Відмінності у словах і числах під це не підпадають.
4. Інакше рядки, у яких ключ збігається, а англійський текст у доповненні змінено, не
   переносяться автоматично й рахуються окремо.

Рядки, у яких ключ є в основній грі, але англійський текст за ним інший (пункт 4 і ті рядки
пункту 2, де фраза знайдена під іншим ключем), перед записом потрапляють у **вікно
підтвердження**. Для кожного видно англійський текст основної гри й доповнення, поточний
переклад доповнення, переклад основної гри за ключем і за англійською фразою. Колонка
«Результат» редагується, як переклад у головному вікні (2× клік — багаторядкове поле); типово
рядок зі знайденою фразою отримує переклад за фразою, а рядок без неї лишає поточний переклад.
Для виділених рядків є кнопки «зрівняти з основною грою за ключем» і «за фразою»; контекстне
меню комірок дозволяє копіювати значення, внести його в результат, додати до результату, внести
всі варіанти та застосувати результат до рядків з таким самим англійським текстом. Результат із кількома готовими
варіантами не записується.

**Результат.** Після підтвердження переклад записується в **новий** DAT із міткою часу в теці
`translated\{гра}\Data\Text` (`{ім'я} {yyMMdd HHmm}.dat`; за збігу назви додається ` (2)`),
парний TSV зі статусами — у `work\{гра}`. Наявні DAT не перезаписуються, попередня версія
робочого TSV лишається як `.bak`.

Захист і безпека:

- Рядок із позначкою вичитки, відмінною від `-`, **не перезаписується**. Наявність
  перекладу критерієм не є: після пакетного ШІ-перекладу переклад має майже кожен рядок.
- Технічні рядки не змінюються ніколи.
- Структура DAT береться лише з файлу доповнення; донор постачає тільки текст.
- Змінені рядки отримують статус «Змінено», тож їх легко переглянути фільтром.
- Після завершення показується підсумок: збіги за ключем / за текстом / конфлікти, перенесено,
  без змін, пропущено вичитаних, англійський текст відрізняється, без відповідника.

---

## EN

**EN:** The **"⇄ Transfer"** header button is available right after startup. The window takes four files:

| File | Purpose |
|---|---|
| ① Base game: original | the English Empire at War DAT |
| ② Base game: translation | a translated DAT or a TSV (a TSV with an `OriginalText` column does without ①) |
| ③ Expansion: original | the English Forces of Corruption DAT |
| ④ Expansion: translation | the current translation and review statuses (optional), e.g. a working TSV from the `work` folder |

Instead of ③ and ④ the expansion already open in the application can be used. From ① and ②
an in-memory "key → English text → translation" index is built; it is kept while the
application is open and is not reread until the files change.

Before the transfer the window shows an analysis:

- keys with a translation in the base game and distinct English texts;
- match **by key** (same English text);
- match **by English text only** (different or absent key);
- conflicts (several distinct translations of one text);
- the key exists in the base game but the English text differs;
- no match;
- base game keys absent from the expansion;
- among key matches, rows for which an English-text match would give a **different** translation.

The **"Match report"** button saves every non-technical expansion row to
`transfer\transfer_report {yyMMdd HHmm}.tsv` and opens it in a viewer-only window: filters with
counters and an explanation under them, search, copying of cells and rows (Ctrl+C, context
menu), "Open folder". Columns: key, match kind (`Key` / `KeyMinorDiff` / `TextOnly` /
`Conflict` / `TextChanged` / `NoMatch`), the discrepancy flag, the base game's and the
expansion's English text, the translation by key and the variants by English phrase. The note
at the top of the window shows how many rows will go to the review.

Row matching:

1. **By key**, when the English text in the donor and in the expansion is equal (ignoring
   case and whitespace). The key identifies the context, so this is the most reliable match.
2. **By English text** — for keys absent from the donor. The **exact** text (case and
   whitespace included) is looked up first, and the normalized text only when no exact one
   exists, so "Fall out" and "Fall out " (with a trailing space) each get their own
   translation. If the donor has one translation of that text, it is transferred; if it has
   several distinct ones, a choice window opens.
3. **Minor difference.** When the key exists in the base game and the English texts differ only
   by a trifle that does not change the meaning (a typo, an extra or missing letter or a
   punctuation mark), the translation is taken by key without a confirmation. Conditions (all
   together): a text of at least 20 characters, a changed fragment of at most 3 characters and
   at most 5% of the length (after the common beginning and ending of the normalized texts are
   discarded), the same digits, and the difference is not merely a hyphen or a space (another
   spelling of a name, e.g. "Abregado-rae" / "Abregado Rae"). Differences in words and numbers do not qualify.
4. Otherwise rows whose key matches but whose English text differs in the expansion are not
   transferred automatically and are counted separately.

Rows whose key exists in the base game but whose English text under it differs (item 4 and
the rows of item 2 whose phrase was found under another key) go to a **review window** before
writing. For each row it shows the base game's and the expansion's English text, the expansion's
current translation, and the base game's translation by key and by English phrase. The "Result"
column is edited like a translation in the main window (double-click opens a multi-line box); by
default a row with a found phrase gets the translation by phrase, a row without one keeps its
current translation. For the selected rows there are the buttons "match the base game by key"
and "by phrase"; the cell context menu copies the value, puts it into the result, appends it to
the result, inserts all variants and applies the result to rows with the same English text.
A result that still holds several ready-made variants is not written.

**Result.** After the confirmation the translation is written to a **new** time-stamped DAT in
`translated\{game}\Data\Text` (`{name} {yyMMdd HHmm}.dat`; a ` (2)` suffix on a name clash), and a
paired TSV with the statuses goes to `work\{game}`. Existing DATs are never overwritten, the
previous version of the working TSV stays as `.bak`.

Protection and safety:

- A row whose review mark is anything but `-` is **never overwritten**. Having a translation is
  not the criterion: after a batch AI translation almost every row already has one.
- Technical entries are never changed.
- The DAT structure comes only from the expansion file; the donor supplies text only.
- Changed rows become "Modified", so they are easy to inspect with the filter.
- A summary follows: matches by key / by text / conflicts, transferred, unchanged, reviewed rows
  skipped, English text differs, no match.

---

## Скріншоти / Screenshots

**UA:** вікно підтвердження перед записом.
**EN:** the confirmation window before writing.

![Підтвердження перенесення / Transfer review](../assets/screenshots/gui_light_transfer_change.png)
