# Star Wars: Empire at War — Localization Toolset (by EMP_UA)

**UA:** Комплексний набір інструментів для локалізації ігор на рушії **Alamo** (Star Wars: Empire at War). Дозволяє автоматизувати процес від екстракції бінарних даних до інтелектуального перекладу за допомогою ШІ.  
**EN:** A comprehensive toolkit for localizing games built on the **Alamo** engine (Star Wars: Empire at War). It automates the entire pipeline, from binary data extraction to AI-powered translation.

---

## 🖥️ DAT Editor GUI / Редактор DAT файлів

**UA:** `EaWLocalizationTool.GUI` — WPF-редактор для ручного редагування та перевірки перекладів у `.dat` файлах рушія Alamo. Призначений для моддерів, яким потрібен повний контроль над локалізацією.

**EN:** `EaWLocalizationTool.GUI` — a WPF editor for manual editing and review of translations in Alamo engine `.dat` files. Designed for modders who need full control over localization.

### Можливості / Features
- **UA:** Завантаження оригінального DAT + джерела перекладу (TSV або інший DAT) / **EN:** Load original DAT + translation source (TSV or another DAT)
- **UA:** Фільтрація: Всі / Без перекладу / Перекладено / Змінено / Проблемні / Технічні / **EN:** Filters: All / Untranslated / Translated / Modified / Issues / Technical
- **UA:** Автоматична валідація: `\n`, `%s/%d`, `[теги]`, `<теги>` / **EN:** Auto-validation: `\n`, `%s/%d`, `[tags]`, `<tags>`
- **UA:** Розумне визначення технічних рядків (роздільники crawl-тексту, заглушки) / **EN:** Smart detection of technical entries (crawl-text separators, placeholders)
- **UA:** Безпечний запис: CRC32 та ключі — побайтова копія оригіналу / **EN:** Safe write: CRC32 and keys — byte-perfect copy from original
- **UA:** Темна / світла тема, масштабування шрифту / **EN:** Dark / light theme, font scaling
- **UA:** Експорт у TSV для роботи в Excel / Google Sheets / **EN:** Export to TSV for use in Excel / Google Sheets

### Завантаження / Download
Дивіться розділ [Releases](../../releases) → `EaWLocalizationTool.GUI_vX.X.zip`

**UA:** Вимоги: Windows 10/11 x64, [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)  
**EN:** Requirements: Windows 10/11 x64, [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

---

## 🛡️ Technical Transparency / Технічна прозорість

**UA:** Для забезпечення безпеки та прозорості я надаю вихідний код усіх інструментів та логіку інсталятора:
* **Безпека:** Основні інструменти портативні та працюють без спеціальних прав. Скрипт `SetupFonts.bat` автоматично визначає версію ОС: на сучасних Windows (10 версії 1809+ та 11) він дозволяє встановлення шрифтів локально **без прав адміністратора**, а для старіших систем або глобального встановлення безпечно запитує підвищення прав через UAC.
* **Реєстр:** 
    * Інсталятор використовує **HKEY_CURRENT_USER** виключно для роботи деінсталятора та відстеження версії (запобігання дублюванню).
    * Скрипт `SetupFonts.bat` вносить зміни до гілки Fonts (у **HKEY_CURRENT_USER** або **HKEY_LOCAL_MACHINE** залежно від вибору) для офіційної реєстрації шрифтів у системі, що необхідно для рушія гри.
* **Приватність:** API-ключі Gemini вводяться користувачем вручну та не зберігаються в репозиторії.
* **Очищення:** Деінсталятор повністю видаляє всі файли локалізації, власні записи в реєстрі, а також **інтерактивно запитує** користувача перед автоматичним видаленням встановлених шрифтів із системи.

**EN:** To ensure safety and transparency, I am providing the source code for all tools and the installer logic:
* **Security:** Core tools are portable and run without special privileges. The `SetupFonts.bat` script automatically detects the OS version: on modern Windows (10 build 1809+ and 11), it allows local font installation **without administrator rights**. For older systems or global installation, it safely requests elevation via UAC.
* **Registry Use:** 
    * The installer uses **HKEY_CURRENT_USER** solely for uninstaller support and version tracking (to prevent duplication).
    * The `SetupFonts.bat` script modifies the Fonts branch (in **HKEY_CURRENT_USER** or **HKEY_LOCAL_MACHINE** depending on the user's choice) to officially register fonts, which is required by the game engine.
* **Privacy:** Gemini API keys are entered manually by the user and are not stored within the repository.
* **Cleanup:** The uninstaller completely removes all localization files, its own registry entries, and **interactively prompts** the user before automatically removing the installed fonts from the system.
  
---

## 🧰 Third-party Tools & Credits / Подяки

* **[Gemini API](https://ai.google.dev/):** **UA:** Основний лінгвістичний рушій для перекладу. **EN:** The primary linguistic engine used for translation.
* **[CsvHelper](https://joshclose.github.io/CsvHelper/):** **UA:** Для надійної обробки проміжних TSV-таблиць. **EN:** For robust processing of intermediate TSV tables.
* **[Inno Setup](https://jrsoftware.org/isinfo.php):** **UA:** Використовується для створення професійного пакета встановлення з підтримкою версійності. **EN:** Used to create a professional installation package with version detection support.
* **[Exo 2 Font](https://fonts.google.com/specimen/Exo+2):** **UA:** Використано як гармонійну базу для інтеграції українських символів в оригінальні шрифти гри. **EN:** Used as a matching base for integrating Ukrainian characters into the original game fonts.

---

## ⚙️ Development Workflow / Робочий процес

**UA:** Цей проєкт є результатом складного технічного циклу:
1. **Extraction & Packaging:** Використання `EaWLocalizationTool` для розпакування бінарних `.dat`, обробки `XML` та реконструкції архівів із збереженням CRC32.
2. **AI-Enhanced Translation:** Використання `StarWarsLocalizer` для автоматизованого перекладу через Gemini API (3.1 Flash Lite / 2.5 Flash) з дворівневою перевіркою галюцинацій.
3. **Manual Review & Edit:** Використання `EaWLocalizationTool.GUI` для ручної перевірки, виправлення та збереження фінального DAT.
4. **Font Engineering:** Модифікація оригінальних шрифтів гри шляхом "підсадки" українських символів (на базі відкритого шрифту **Exo 2**) та налаштування метрик для коректного відображення в інтерфейсі.

**EN:** This project is the result of a complex technical workflow:
1. **Extraction & Packaging:** Using `EaWLocalizationTool` to unpack binary `.dat` files, process `XML`, and reconstruct archives while preserving CRC32.
2. **AI-Enhanced Translation:** Using `StarWarsLocalizer` for automated translation via Gemini API (3.1 Flash Lite / 2.5 Flash) with two-tier hallucination validation.
3. **Manual Review & Edit:** Using `EaWLocalizationTool.GUI` for manual review, correction, and saving the final DAT.
4. **Font Engineering:** Modifying original game fonts by blending in Ukrainian characters (based on the open-source **Exo 2** font) and adjusting metrics for correct UI display.

---

## 📂 Repository Structure / Структура репозиторію

* **/assets/fonts** 
  * **UA:** Містить модифіковані шрифти (`.ttf`) та `SetupFonts.bat` для автоматизації їх підготовки та встановлення у систему.
  * **EN:** Contains modified fonts (`.ttf`) and `SetupFonts.bat` for automated preparation and system installation.
* **/scr**
  * `/EaWLocalizationTool` — **UA:** Solution-папка, що містить три проєкти: / **EN:** Solution folder containing three projects:
    * `/EaWLocalizationTool` — **UA:** Консольний інструмент: екстракція XML/DAT/TXT у TSV-таблиці, зіставлення з перекладом, пакування назад в ігрові формати. **EN:** Console tool: extracts XML/DAT/TXT into TSV tables, merges with translation, and repacks into game formats.
    * `/EaWLocalizationTool.Core` — **UA:** Спільна бібліотека: парсинг/запис DAT, валідація, моделі. Використовується консоллю та GUI. **EN:** Shared library: DAT parsing/writing, validation, and models. Used by both the console and GUI.
    * `/EaWLocalizationTool.GUI` — **UA:** WPF-редактор DAT-файлів для ручного перекладу та перевірки. **EN:** WPF DAT file editor for manual translation and review.
  * `/MEGExtractor` — **UA:** Інструмент для роботи з `.meg` архівами гри. **EN:** Tool for handling game `.meg` archives.
  * `/StarWarsLocalizer` — **UA:** ШІ-перекладач на базі Gemini API. **EN:** AI translator based on the Gemini API.
* **/setup**
  * `packexe.iss` — **UA:** Текст скрипта для Inno Setup. Надає повну прозорість того, як файли розгортаються та видаляються з папки гри. **EN:** Inno Setup script text. Provides full transparency on how localization is deployed and uninstalled from the game directory.
* `.gitignore` — **UA:** Виключає конфіденційні файли (ключі) та тимчасові дані компіляції. **EN:** Excludes sensitive files (keys) and temporary build data.
* `LICENSE` — **UA:** Ліцензія проєкту. **EN:** Project license.

---

### ⚖️ Copyright Note / Примітка щодо авторських прав
**UA:** Усі модифіковані активи у папці `/assets` надаються виключно для некомерційного використання фанатами. Усі права на оригінальні активи належать розробникам гри.  
**EN:** All modified assets in the `/assets` folder are provided solely for non-commercial fan use. All rights to the original assets belong to the game developers.
