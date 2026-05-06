# Star Wars: Empire at War — Localization Toolset (by EMP_UA)

UA: Комплексний набір інструментів для локалізації ігор на рушії **Alamo** (Star Wars: Empire at War). Дозволяє автоматизувати процес від екстракції бінарних даних до інтелектуального перекладу за допомогою ШІ.

EN: A comprehensive toolkit for localizing games built on the **Alamo** engine (Star Wars: Empire at War). It automates the entire pipeline, from binary data extraction to AI-powered translation.

---

## 🛡️ Technical Transparency / Технічна прозорість

UA: Цей репозиторій містить вихідний код інструментів, що використовуються для створення мовних пакетів. Я публікую цей код для забезпечення прозорості перед спільнотою Nexus Mods та Steam Workshop.
* **Безпека:** Інструменти працюють виключно у межах робочих папок, не модифікують системні реєстри та не потребують прав адміністратора[cite: 11, 15].
* **Приватність:** API-ключі Gemini не зберігаються у коді та вводяться користувачем під час сесії або через локальний файл `api_key.txt`[cite: 15].

EN: This repository contains the source code for the tools used to create language packs. I am publishing this code to ensure transparency for the Nexus Mods and Steam Workshop communities.
* **Security:** The tools operate strictly within working directories, do not modify system registries, and do not require administrator privileges[cite: 11, 15].
* **Privacy:** Gemini API keys are not hardcoded and must be provided by the user during the session or via a local `api_key.txt` file[cite: 15].

---

## 🧰 Third-party Tools & Credits / Подяки

* **[Gemini API](https://ai.google.dev/):** Використовується як основний лінгвістичний рушій для перекладу[cite: 15, 18].
* **[CsvHelper](https://joshclose.github.io/CsvHelper/):** Для надійної обробки проміжних TSV-таблиць[cite: 13].
* **[Inno Setup](https://jrsoftware.org/isinfo.php):** Використовується для створення інсталятора з підтримкою версійності та деінсталяції.

---

## ⚙️ Development Workflow / Робочий процес

UA: Проєкт складається з трьох основних модулів:

1. **EaWLocalizationTool (Extractor & Repacker):**
   * Парсинг бінарних файлів `MASTERTEXTFILE_*.DAT`[cite: 9, 13].
   * Обробка ігрових `XML` та технічних `TXT` файлів[cite: 8, 10].
   * Реконструкція ігрових архівів із збереженням оригінального кодування та CRC32-хешів[cite: 7, 13].

2. **StarWarsLocalizer (AI Translation Engine):**
   * **Dual-Tier Logic:** Підтримка Gemini 3.1 Flash Lite (Free) та Gemini 2.5 Flash (Paid)[cite: 15].
   * **Rate Limiting:** Контроль RPM/RPD для запобігання блокуванням[cite: 14, 16].
   * **Smart Caching:** Глобальний кеш перекладів для економії запитів до API[cite: 16].

3. **MEGExtractor:**
   * Спеціалізований інструмент для розпакування та маніпуляції з `.meg` архівами гри.

EN: The project consists of three main modules:

1. **EaWLocalizationTool (Extractor & Repacker):**
   * Parsing of binary `MASTERTEXTFILE_*.DAT` files[cite: 9, 13].
   * Processing of game `XML` and technical `TXT` files[cite: 8, 10].
   * Reconstruction of game archives while preserving original encoding and CRC32 hashes[cite: 7, 13].

2. **StarWarsLocalizer (AI Translation Engine):**
   * **Dual-Tier Logic:** Support for Gemini 3.1 Flash Lite (Free) and Gemini 2.5 Flash (Paid)[cite: 15].
   * **Rate Limiting:** Intelligent RPM/RPD control to prevent API bans[cite: 14, 16].
   * **Smart Caching:** Global translation cache to save API requests[cite: 16].

---

## 📂 Repository Structure / Структура репозиторію

* **/assets/fonts** — UA: Оригінальні та модифіковані шрифти гри / EN: Original and modified game fonts.
* **/scr** — UA: Вихідний код інструментів / EN: Tools source code:
    * `/EaWLocalizationTool` — Extractor & Repacker.
    * `/MEGExtractor` — MEG archive tool.
    * `/StarWarsLocalizer` — Gemini AI Translator.
* **/setup** — UA: Конфігураційні файли для Inno Setup / EN: Inno Setup configuration files.
* `.gitignore` — UA: Список файлів, що ігноруються Git (включаючи кеш та ключі) / EN: Git ignore list (including cache and keys).
