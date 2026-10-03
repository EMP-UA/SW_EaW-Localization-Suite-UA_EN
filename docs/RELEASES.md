# Релізи та перевірка збірок / Releases and build verification

[← README](../README.md) · [CHANGELOG](../CHANGELOG.md)

---

## UA

Архіви в [Releases](https://github.com/EMP-UA/SW_EaW-Localization-Suite-UA_EN/releases) збирає
GitHub Actions ([`release.yml`](../.github/workflows/release.yml)) з вихідного коду тега;
готові файли вручну не завантажуються.

Тег `vX.Y.Z` (напр. `v1.1.0`) збирає обидві програми — `EaWLocalizationTool.GUI` і
`EaWTextureConverter` — по три варіанти (6 архівів) в одному релізі. Опис релізу береться з розділу
`## [X.Y.Z]` файлу `CHANGELOG.md`.

Варіанти: `win-x64` і `win-x86` (самодостатні), `generic` (потребує .NET 10 Desktop Runtime).
Імена архівів: `{Програма}_vX.Y.Z_{варіант}.zip`.

Запуск: пуш тега або вручну — Actions → Release → Run workflow із наявним тегом. Якщо
відповідного розділу `CHANGELOG.md` немає, збірка зупиняється до компіляції.

**Перевірка архіву.** До кожного релізу додано `SHA256SUMS.txt` і підтвердження походження
збірки (GitHub build provenance attestation). Воно показує, що архів зібрано цим workflow з
конкретного коміту репозиторію:

```
gh attestation verify EaWLocalizationTool.GUI_vX.Y.Z_win-x64.zip --repo EMP-UA/SW_EaW-Localization-Suite-UA_EN
```

Контрольні суми: `sha256sum -c SHA256SUMS.txt` (Linux/macOS) або
`Get-FileHash <архів> -Algorithm SHA256` (PowerShell).

**Nexus Mods.** Після публікації релізу завдання `nexus` завантажує архіви `generic` як нову
версію наявних файлів на Nexus Mods (попередня версія архівується). Налаштування в репозиторії
(Settings → Secrets and variables → Actions):

| Назва | Тип | Значення |
|---|---|---|
| `NEXUSMODS_API_KEY` | секрет | особистий API-ключ ([Personal API Key](https://www.nexusmods.com/settings/api-keys)) |
| `NEXUS_GUI_FILE_ID` | змінна | «API File ID» у властивостях файлу на сторінці керування файлами моду (`…/mods/<номер>/edit/files`) |
| `NEXUS_GUI_MOD_ID` | змінна, необов'язково | «API Unique Mod ID» там само (це не номер моду з адреси сторінки); з ним у журнал змін Nexus додається посилання на реліз GitHub |
| `NEXUS_TEXTURE_FILE_ID`, `NEXUS_TEXTURE_MOD_ID` | змінні | те саме для конвертера текстур |

Особистий ключ призначений для власної автоматизації; його зберігають лише як секрет репозиторію, не в коді й не в журналах. «Short Unique File ID» у властивостях файлу для завантаження не використовується. Журнал змін на Nexus лише доповнюється: повторний запуск для того самого релізу додасть запис ще раз.

Без змінних `NEXUS_*_FILE_ID` завдання пропускається. Сторінка моду та перший файл мають уже
існувати. Реліз GitHub публікується раніше, тож збій завантаження на Nexus його не скасовує.

---

## EN

The archives on the [Releases](https://github.com/EMP-UA/SW_EaW-Localization-Suite-UA_EN/releases)
page are built by GitHub Actions ([`release.yml`](../.github/workflows/release.yml)) from the tag's
source code; no prebuilt files are uploaded by hand.

A `vX.Y.Z` tag (e.g. `v1.1.0`) builds both applications — `EaWLocalizationTool.GUI` and
`EaWTextureConverter` — in three variants each (6 archives) in one release. The release notes come
from the `## [X.Y.Z]` section of `CHANGELOG.md`.

Variants: `win-x64` and `win-x86` (self-contained), `generic` (requires the .NET 10 Desktop
Runtime). Archive names: `{Application}_vX.Y.Z_{variant}.zip`.

Trigger: a tag push, or manually — Actions → Release → Run workflow with an existing tag. If
the matching `CHANGELOG.md` section is missing, the run stops before compiling.

**Verifying an archive.** Every release carries `SHA256SUMS.txt` and a build provenance
attestation (GitHub build provenance attestation). It shows that the archive was built by this
workflow from a specific commit of the repository:

```
gh attestation verify EaWLocalizationTool.GUI_vX.Y.Z_win-x64.zip --repo EMP-UA/SW_EaW-Localization-Suite-UA_EN
```

Checksums: `sha256sum -c SHA256SUMS.txt` (Linux/macOS) or
`Get-FileHash <archive> -Algorithm SHA256` (PowerShell).

**Nexus Mods.** After the release is published, the `nexus` job uploads the `generic` archives as
a new version of the existing files on Nexus Mods (the previous version is archived). Repository
settings (Settings → Secrets and variables → Actions):

| Name | Type | Value |
|---|---|---|
| `NEXUSMODS_API_KEY` | secret | a personal API key ([Personal API Key](https://www.nexusmods.com/settings/api-keys)) |
| `NEXUS_GUI_FILE_ID` | variable | "API File ID" in the file's properties on the mod's file management page (`…/mods/<number>/edit/files`) |
| `NEXUS_GUI_MOD_ID` | variable, optional | "API Unique Mod ID" on the same page (not the mod number from the page address); with it a link to the GitHub release is added to the Nexus changelog |
| `NEXUS_TEXTURE_FILE_ID`, `NEXUS_TEXTURE_MOD_ID` | variables | the same for the texture converter |

A personal key is meant for your own automation; keep it only as a repository secret, never in code or logs. The "Short Unique File ID" in the file's properties is not used for uploading. The Nexus changelog is append-only: re-running the job for the same release adds the entry again.

Without the `NEXUS_*_FILE_ID` variables the job is skipped. The mod page and its first file must
already exist. The GitHub release is published first, so a failed Nexus upload does not undo it.
