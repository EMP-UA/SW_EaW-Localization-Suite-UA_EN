# Star Wars: Empire at War — Localization Toolset (by EMP_UA)

**UA:** Комплексний набір інструментів для локалізації ігор на рушії **Alamo** (Star Wars: Empire at War). Дозволяє автоматизувати процес від екстракції бінарних даних до інтелектуального перекладу за допомогою ШІ.  
**EN:** A comprehensive toolkit for localizing games built on the **Alamo** engine (Star Wars: Empire at War). It automates the entire pipeline, from binary data extraction to AI-powered translation.

---

## 🛡️ Technical Transparency / Технічна прозорість

**UA:** Для забезпечення безпеки та прозорості я надаю вихідний код усіх інструментів та логіку інсталятора:
* **Безпека:** Основні інструменти портативні та працюють без спеціальних прав. Скрипт `SetupFonts.bat` автоматично визначає версію ОС: на сучасних Windows (10 версії 1809+ та 11) він дозволяє встановлення шрифтів локально **без прав адміністратора**, а для старіших систем або глобального встановлення безпечно запитує підвищення прав через UAC.
* **Реєстр:** 
    * Інсталятор використовує **HKEY_CURRENT_USER** виключно для роботи деінсталятора та відстеження версії 0.10 (запобігання дублюванню).
    * Скрипт `SetupFonts.bat` вносить зміни до гілки Fonts (у **HKEY_CURRENT_USER** або **HKEY_LOCAL_MACHINE** залежно від вибору) для офіційної реєстрації шрифтів у системі, що необхідно для рушія гри.
* **Приватність:** API-ключі Gemini вводяться користувачем вручну та не зберігаються в репозиторії.
* **Очищення:** Деінсталятор повністю видаляє всі файли локалізації, власні записи в реєстрі, а також **інтерактивно запитує** користувача перед автоматичним видаленням встановлених шрифтів із системи.

**EN:** To ensure safety and transparency, I am providing the source code for all tools and the installer logic:
* **Security:** Core tools are portable and run without special privileges. The `SetupFonts.bat` script automatically detects the OS version: on modern Windows (10 build 1809+ and 11), it allows local font installation **without administrator rights**. For older systems or global installation, it safely requests elevation via UAC.
* **Registry Use:** 
    * The installer uses **HKEY_CURRENT_USER** solely for uninstaller support and version 0.10 tracking (to prevent duplication).
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
3. **Font Engineering:**: Модифікація оригінальних шрифтів гри шляхом "підсадки" українських символів (на базі відкритого шрифту **Exo 2**) та налаштування метрик для коректного відображення в інтерфейсі.

**EN:** This project is the result of a complex technical workflow:
1. **Extraction & Packaging:** Using `EaWLocalizationTool` to unpack binary `.dat` files, process `XML`, and reconstruct archives while preserving CRC32.
2. **AI-Enhanced Translation:** Using `StarWarsLocalizer` for automated translation via Gemini API (3.1 Flash Lite / 2.5 Flash) with two-tier hallucination validation.
3. **Font Engineering:**: Modifying original game fonts by blending in Ukrainian characters (based on the open-source **Exo 2** font) and adjusting metrics for correct UI display.

---

## 📂 Repository Structure / Структура репозиторію

* **/assets/fonts** 
  * **UA:** Містить модифіковані шрифти (`.ttf`) та `SetupFonts.bat` для автоматизації їх підготовки та встановлення у систему.
  * **EN:** Contains modified fonts (`.ttf`) and `SetupFonts.bat` for automated preparation and system installation.
* **/scr**
  * `/EaWLocalizationTool` — **UA:** Код для екстракції та пакування архівів. **EN:** Source for archive extraction and repacking.
  * `/MEGExtractor` — **UA:** Інструмент для роботи з `.meg` архівами гри. **EN:** Tool for handling game `.meg` archives.
  * `/StarWarsLocalizer` — **UA:** ШІ-перекладач на базі Gemini API. **EN:** AI-translator based on Gemini API.
* **/setup**
  * `packexe.iss` — **UA:** Текст скрипта для Inno Setup. Надає повну прозорість того, як файли розгортаються та видаляються з папки гри.
  * **EN:** Inno Setup script text. Provides full transparency on how localization is deployed and uninstalled from the game directory.
* `.gitignore` — **UA:** Виключає конфіденційні файли (ключі) та тимчасові дані компіляції. **EN:** Excludes sensitive files (keys) and temporary build data.
* `LICENSE` — **UA:** Ліцензія проєкту. **EN:** Project license.

---

### ⚖️ Copyright Note / Примітка щодо авторських прав
**UA:** Усі модифіковані активи у папці `/assets` надаються виключно для некомерційного використання фанатами. Усі права на оригінальні активи належать розробникам гри.  
**EN:** All modified assets in the `/assets` folder are provided solely for non-commercial fan use. All rights to the original assets belong to the game developers.
