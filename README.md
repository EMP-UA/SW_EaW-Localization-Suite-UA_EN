# Star Wars: Empire at War — Localization Toolset (by EMP_UA)

**UA:** Комплексний набір інструментів для локалізації ігор на рушії **Alamo** (Star Wars: Empire at War). Дозволяє автоматизувати процес від екстракції бінарних даних до інтелектуального перекладу за допомогою ШІ.  
**EN:** A comprehensive toolkit for localizing games built on the **Alamo** engine (Star Wars: Empire at War). It automates the entire pipeline, from binary data extraction to AI-powered translation.

---

## 🛡️ Technical Transparency / Технічна прозорість

**UA:** Для забезпечення безпеки та прозорості я надаю вихідний код усіх інструментів та логіку інсталятора:
* **Безпека:** Основні інструменти портативні та працюють без спеціальних прав, проте запуск `SetupFonts.bat` для реєстрації шрифтів у Windows може потребувати прав адміністратора.
* **Реєстр:** 
    * Інсталятор використовує **HKEY_CURRENT_USER** виключно для роботи деінсталятора та відстеження версії 1.1, щоб запобігти дублюванню файлів.
    * Скрипт `SetupFonts.bat` вносить зміни до реєстру (гілка Fonts) для офіційної реєстрації модифікованих шрифтів у системі, що необхідно для їх розпізнавання грою.
* **Приватність:** API-ключі Gemini вводяться користувачем вручну та не зберігаються в репозиторії.
* **Очищення:** Деінсталятор повністю видаляє всі внесені зміни, файли перекладу та власні записи в реєстрі.

**EN:** To ensure safety and transparency, I am providing the source code for all tools and the installer logic:
* **Security:** Core tools are portable, though running `SetupFonts.bat` for font registration in Windows may require administrator privileges.
* **Registry Use:** 
    * The installer uses **HKEY_CURRENT_USER** solely for uninstaller support and version 1.1 tracking.
    * The `SetupFonts.bat` script modifies the registry (Fonts branch) to register modified fonts within the system, which is required for the game engine to recognize them.
* **Privacy:** Gemini API keys are entered manually by the user and are not stored within the repository.
* **Cleanup:** The uninstaller completely removes all changes, localization files, and its own registry entries.

---

## 🧰 Third-party Tools & Credits / Подяки

* **[Gemini API](https://ai.google.dev/):** **UA:** Основний лінгвістичний рушій для перекладу. **EN:** The primary linguistic engine used for translation.
* **[CsvHelper](https://joshclose.github.io/CsvHelper/):** **UA:** Для надійної обробки проміжних TSV-таблиць. **EN:** For robust processing of intermediate TSV tables.
* **[Inno Setup](https://jrsoftware.org/isinfo.php):** **UA:** Використовується для створення професійного пакета встановлення з підтримкою версійності. **EN:** Used to create a professional installation package with version detection support.

---

## ⚙️ Development Workflow / Робочий процес

**UA:** Цей проєкт є результатом складного технічного циклу:
1. **Extraction & Packaging:** Використання `EaWLocalizationTool` для розпакування бінарних `.dat`, обробки `XML` та реконструкції архівів із збереженням CRC32.
2. **AI-Enhanced Translation:** Використання `StarWarsLocalizer` для автоматизованого перекладу через Gemini API (3.1 Flash Lite / 2.5 Flash) з дворівневою перевіркою галюцинацій.
3. **Font Engineering:** Модифікація оригінальних шрифтів гри шляхом додавання українських символів та налаштування метрик для коректного відображення в інтерфейсі.

**EN:** This project is the result of a complex technical workflow:
1. **Extraction & Packaging:** Using `EaWLocalizationTool` to unpack binary `.dat` files, process `XML`, and reconstruct archives while preserving CRC32.
2. **AI-Enhanced Translation:** Using `StarWarsLocalizer` for automated translation via Gemini API (3.1 Flash Lite / 2.5 Flash) with two-tier hallucination validation.
3. **Font Engineering:** Modifying original game fonts by adding Ukrainian characters and adjusting metrics for correct UI display.

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
  * `InnoSetup.txt` — **UA:** Текст скрипта для Inno Setup. Надає повну прозорість того, як файли розгортаються та видаляються з папки гри.
  * **EN:** Inno Setup script text. Provides full transparency on how localization is deployed and uninstalled from the game directory.
* `.gitignore` — **UA:** Виключає конфіденційні файли (ключі) та тимчасові дані компіляції. **EN:** Excludes sensitive files (keys) and temporary build data.
* `LICENSE` — **UA:** Ліцензія проєкту. **EN:** Project license.

---

### ⚖️ Copyright Note / Примітка щодо авторських прав
**UA:** Усі модифіковані активи у папці `/assets` надаються виключно для некомерційного використання фанатами. Усі права на оригінальні активи належать розробникам гри.  
**EN:** All modified assets in the `/assets` folder are provided solely for non-commercial fan use. All rights to the original assets belong to the game developers.
