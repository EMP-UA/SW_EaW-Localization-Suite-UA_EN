# Вичитка, робочі файли та теки / Review, working files and folders

[← README](../README.md) · [Перенесення / Transfer](TRANSFER.md) · [Редактор / Editor](EDITOR.md)

---

## UA

**UA:** Колонка **«Вичитка»** — вільний текст. Значення:

| Позначка | Значення |
|---|---|
| `+` | вичитано |
| `+/-` | вичитано, є сумнів |
| `-` | ще не вичитано (ставиться автоматично) |
| інший текст | довільний коментар |

Лічильник «вичитано» враховує `+` та `+/-`. Для кожного виду є окремий фільтр у другому ряду
панелі фільтрів; він поєднується з основним фільтром і пошуком. Кілька рядків позначаються
одразу: виділити (Ctrl/Shift + клік) → ПКМ → **Вичитка для виділених**. Технічні рядки
пропускаються.

DAT не може зберігати статуси вичитки, тому вони записуються в робочий TSV
`work\{гра}\{ім'я} [{хеш ключів}].tsv` поруч із програмою. Хеш відрізняє основну гру від
доповнення, бо їхні файли називаються однаково. Під час збереження DAT поруч зберігається
парний TSV з тим самим іменем і міткою часу. Під час відкриття перекладу (②) робочий файл
створюється або оновлюється (попередню версію зберігає поряд із суфіксом `.bak`), статуси
беруться з відкритого TSV, а за його відсутності — з наявного робочого файлу. Усім рядкам без
статусу ставиться `-`. Файл також записується під час збереження DAT й автозбереження і не
потрапляє в теку гри чи мода.

### Теки програми

**UA:** Під час запуску програма створює теки поруч із виконуваним файлом, якщо їх немає. Вибір
файлу вручну лишається доступним; теки — це початкове розташування діалогів.

| Тека | Вміст |
|---|---|
| `original\GameData\Data\Text\`, `original\corruption\Data\Text\` | оригінальні англійські DAT; структура збігається з грою (`Star Wars Empire at War\GameData\Data\Text\mastertextfile_english.dat` — основна гра, `Star Wars Empire at War\corruption\Data\Text\mastertextfile_english.dat` — доповнення) |
| `translated\GameData\Data\Text\`, `translated\corruption\Data\Text\` | збережені перекладені DAT: `{ім'я} {yyMMdd HHmm}.dat` |
| `work\GameData\`, `work\corruption\` | робочі TSV зі статусами вичитки: поточний `{ім'я} [{хеш}].tsv`, парні `{ім'я} {yyMMdd HHmm}.tsv` та `{ім'я}_AUTOSAVE.dat` |
| `transfer\` | `transfer_report {yyMMdd HHmm}.tsv` |

Гра визначається за шляхом до відкритого оригіналу: сегмент `corruption` — доповнення, `GameData` — основна гра, інакше файли пишуться в підтеку `other`. Вікно перенесення за
замовчуванням бере найновіші DAT із цих тек.

---

## EN

**EN:** The **"Review"** column is free text. Values:

| Mark | Meaning |
|---|---|
| `+` | reviewed |
| `+/-` | reviewed, in doubt |
| `-` | not reviewed yet (set automatically) |
| any other text | a free-form comment |

The "reviewed" counter counts `+` and `+/-`. Each kind has its own filter in the second row of
the filter bar; it combines with the main filter and the search. Several rows are marked at
once: select (Ctrl/Shift + click) → RMB → **Review for selected**. Technical rows are skipped.

A DAT cannot store review statuses, so they are written to a working TSV
`work\{game}\{name} [{key hash}].tsv` next to the executable. The hash tells the base game from
the expansion, whose files share a name. When a DAT is saved, a paired TSV with the same name
and timestamp is saved alongside it. When a translation (②) is opened, the working file is
created or updated (the previous version is kept next to it with a `.bak` suffix); statuses come
from the opened TSV, or from an existing working file when the TSV has none. Every row without a
status gets `-`. The file is also written on DAT save and autosave and never lands in the game or
mod folder.

### Application folders

**EN:** At startup the application creates the folders next to the executable when missing.
Picking a file manually remains available; the folders are the dialogs' starting locations.

| Folder | Contents |
|---|---|
| `original\GameData\Data\Text\`, `original\corruption\Data\Text\` | original English DATs; the structure matches the game (`Star Wars Empire at War\GameData\Data\Text\mastertextfile_english.dat` — base game, `Star Wars Empire at War\corruption\Data\Text\mastertextfile_english.dat` — expansion) |
| `translated\GameData\Data\Text\`, `translated\corruption\Data\Text\` | saved translated DATs: `{name} {yyMMdd HHmm}.dat` |
| `work\GameData\`, `work\corruption\` | working TSVs with review statuses: the current `{name} [{hash}].tsv`, paired `{name} {yyMMdd HHmm}.tsv` and `{name}_AUTOSAVE.dat` |
| `transfer\` | `transfer_report {yyMMdd HHmm}.tsv` |

The game is detected from the open original's path: a `corruption` segment means the expansion,
`GameData` means the base game, otherwise files go to the `other` subfolder. The
transfer window takes the newest DATs from these folders by default.
