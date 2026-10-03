# Редактор DAT (EaWLocalizationTool.GUI) / DAT editor

[← README](../README.md) · [Перенесення / Transfer](TRANSFER.md) · [Вичитка й теки / Review and folders](REVIEW.md)

---

## UA

**Можливості:**

- Завантаження оригінального DAT і джерела перекладу: TSV, перекладений DAT або робочий TSV зі статусами вичитки
- ⇄ **Перенесення перекладу «основна гра → доповнення»**: кнопка в шапці доступна одразу
  після запуску; вікно показує, скільки рядків збігається за ключем і за англійським текстом
  ([докладно](TRANSFER.md))
- ✓ **Вичитка**: виділення кількох рядків (Ctrl/Shift + клік), масова позначка `+`, `+/-`, `-`
  або власний текст; окремі фільтри за кожною позначкою та коментарем; лічильники в шапці
  й смузі прогресу
- Фільтри: Всі / Без перекладу / Перекладено / Змінено / Проблемні / Дублікати / Дубл. різні / Схожі / Технічні
  + другий ряд фільтрів вичитки: Усі / `+` / `+/-` / `-` / Коментар
- Автоматична валідація: `\n`, `\r`, `%s/%d`, `[теги]`, `<теги>`, `{модифікатори}`, пробіли й переноси на початку та в кінці рядка
- Кнопка «↔ Вирівняти краї» узгоджує пробіли й переноси на початку та в кінці перекладу з оригіналом, не змінюючи текст між краями
- Дублікати: колонка «Дубл.» (`×3`) показує кількість рядків з **точно** однаковим англійським текстом, бурштиновий колір — переклади відрізняються; `≈2` — схожі рядки (інший регістр чи пробіли), які є різними рядками гри, а не дублікатами (фільтр «Схожі»); пункт меню «Застосувати до всіх дублікатів» копіює переклад у всю групу (вичитані рядки — лише за згодою); пункт «Застосувати до схожих з урахуванням регістру» переносить переклад у схожі рядки, підлаштовуючи регістр під оригінал цілі (вичитані рядки пропускаються)
- Розумне визначення технічних рядків: заглушки, роздільники crawl-тексту, фрази
  `DO NOT DISPLAY`, маркер `TEXT_END_OF_DATA`
- Контекстне меню (ПКМ): копіювати оригінал, вставити оригінал як переклад, очистити
  переклад, копіювати ключ, вичитка для виділених
- Безпечний запис: CRC32 та ключі — побайтова копія оригіналу
- Автозбереження: фоновий запис резервної копії (інтервал налаштовується, `0` = вимкнено)
- Логування помилок і подій у файл для діагностики
- Темна / світла тема, масштабування шрифту
- Експорт у TSV (Key, OriginalText, TranslatedText, ReviewStatus) для Excel / Google Sheets

Посилання на докладні описи: [перенесення перекладу](TRANSFER.md), [вичитка, робочі файли й
теки](REVIEW.md).

---

## EN

**Features:**

- Load the original DAT and a translation source: TSV, a translated DAT, or a working TSV with review statuses
- ⇄ **"Base game → expansion" translation transfer**: the header button is available right
  after startup; the window shows how many rows match by key and by English text ([details](TRANSFER.md))
- ✓ **Review**: multi-row selection (Ctrl/Shift + click), bulk `+`, `+/-`, `-` marks or a
  custom text; separate filters per mark and for comments; counters in the header and the
  progress bar
- Filters: All / Untranslated / Translated / Modified / Issues / Duplicates / Dup. differ / Similar / Technical
  + a second row of review filters: All / `+` / `+/-` / `-` / Comment
- Auto-validation: `\n`, `\r`, `%s/%d`, `[tags]`, `<tags>`, `{modifiers}`, whitespace and line breaks at the start and end of the string
- The "↔ Align edges" button matches the whitespace and line breaks at the start and end of the translation to the original without changing the text between the edges
- Duplicates: the "Dup" column (`×3`) shows how many rows share **exactly** the same English text, amber means the translations differ; `≈2` — similar rows (different case or whitespace), which are different game strings rather than duplicates (the "Similar" filter); the "Apply to all duplicates" menu item copies the translation to the whole group (reviewed rows only on consent)
- Smart detection of technical entries: placeholders, crawl-text separators,
  `DO NOT DISPLAY` phrases, the `TEXT_END_OF_DATA` marker
- Context menu (RMB): copy original, paste original as translation, clear translation,
  copy key, review for selected rows
- Safe write: CRC32 and keys are a byte-perfect copy of the original
- Autosave: background backup writing (configurable interval, `0` = disabled)
- Logging of errors and events to a file for diagnostics
- Dark / light theme, font scaling
- TSV export (Key, OriginalText, TranslatedText, ReviewStatus) for Excel / Google Sheets

Detailed descriptions: [translation transfer](TRANSFER.md), [review, working files and
folders](REVIEW.md).
