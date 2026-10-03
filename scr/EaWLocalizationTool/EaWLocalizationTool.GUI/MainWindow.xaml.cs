using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using EaWLocalizationTool.Core;
using EaWLocalizationTool.Core.Models;
using EaWLocalizationTool.GUI.Models;
using EaWLocalizationTool.GUI.Services;

namespace EaWLocalizationTool.GUI;

public partial class MainWindow : Window
{
    // ── Стан / State ──────────────────────────────────────────────────────────
    private readonly ObservableCollection<TranslationEntry> _entries = new();
    private ICollectionView _view;
    private byte[] _origRaw = [];
    private string _origFileName = "";
    private string _origHash = "";
    private string _filterMode = "all";
    private string _reviewFilter = "all";
    private string _searchText = "";
    private int _rejectedReported;

    // UA: Рядок, на якому відкрито контекстне меню (ПКМ). Окремі пункти меню
    //     діють саме на нього, а не на SelectedItem: при мультивиділенні
    //     SelectedItem — не обов'язково рядок, на якому натиснуто ПКМ.
    // EN: The row the context menu was opened on (RMB). Single-row menu items
    //     act on it rather than on SelectedItem: with multi-selection,
    //     SelectedItem is not necessarily the clicked row.
    private TranslationEntry? _contextEntry;

    // UA: Гра, до якої належить відкритий оригінал (визначає підтеку original / translated / work).
    // EN: The game the open original belongs to (selects the original / translated / work subfolder).
    private GameKind _gameKind = GameKind.Other;

    // UA: Таймер автозбереження / EN: Autosave timer
    private readonly DispatcherTimer _autoSaveTimer = new();

    // ── Допоміжний метод для кольорів / Resource brush helper ─────────────────
    private static Brush Res(string key) =>
        (Brush)Application.Current.Resources[key];

    // ─────────────────────────────────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();

        // UA: Створює теки original / translated / work / transfer, якщо їх немає.
        // EN: Creates the original / translated / work / transfer folders when missing.
        AppFolders.EnsureCreated();

        _view = CollectionViewSource.GetDefaultView(_entries);
        _view.Filter = FilterEntry;
        MainGrid.ItemsSource = _view;
        UpdateThemeButton();
        UpdateFontLabel();
        SetActiveFilter("all");
        SetActiveReviewFilter("all");

        // UA: Налаштування таймера автозбереження / EN: Autosave timer setup
        _autoSaveTimer.Tick += AutoSaveTimer_Tick;
        ApplyAutoSaveSettings();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ТЕМА ТА ШРИФТ / THEME AND FONT
    // ══════════════════════════════════════════════════════════════════════════

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.Toggle();
        UpdateThemeButton();
        // UA: Оновлюємо кольори кнопок фільтрів після зміни теми
        // EN: Refresh filter button colors after theme change
        SetActiveFilter(_filterMode);
        SetActiveReviewFilter(_reviewFilter);
    }

    private void FontIncrease_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.IncreaseFontSize();
        UpdateFontLabel();
        RefreshDataGridRows();
    }

    private void FontDecrease_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.DecreaseFontSize();
        UpdateFontLabel();
        RefreshDataGridRows();
    }

    /// <summary>
    /// UA: Скидає ItemsSource щоб DataGrid перерахував висоту рядків.
    ///     DynamicResource оновлює FontSize, але DataGrid кешує висоту рядків —
    ///     скидання ItemsSource = null → _view очищає кеш.
    /// EN: Resets ItemsSource so DataGrid recalculates row heights.
    ///     DynamicResource updates FontSize, but DataGrid caches row heights —
    ///     resetting ItemsSource = null → _view clears the cache.
    /// </summary>
    private void RefreshDataGridRows()
    {
        if (_entries.Count == 0) return;
        MainGrid.ItemsSource = null;
        MainGrid.ItemsSource = _view;
    }

    private void UpdateThemeButton() =>
        ThemeToggleBtn.Content = ThemeManager.IsDark
            ? "☀ Світла / Light"
            : "🌙 Темна / Dark";

    private void UpdateFontLabel() =>
        FontSizeLabel.Text = ThemeManager.FontSize.ToString("F0");

    private void Window_Closing(object sender, CancelEventArgs e) =>
        ThemeManager.SaveSettings();

    // ══════════════════════════════════════════════════════════════════════════
    // ЗАВАНТАЖЕННЯ ФАЙЛІВ / FILE LOADING
    // ══════════════════════════════════════════════════════════════════════════

    // UA: Кнопка на порожньому екрані / EN: Empty state button
    private void EmptyStateOpenBtn_Click(object sender, RoutedEventArgs e) =>
        OpenOriginalDat();

    // UA: Клік на картку ① / EN: Click on card ①
    private void OrigCard_Click(object sender, MouseButtonEventArgs e) =>
        OpenOriginalDat();

    // UA: Клік на картку ② / EN: Click on card ②
    private void TransCard_Click(object sender, MouseButtonEventArgs e) =>
        OpenTranslationFile();

    /// <summary>
    /// UA: Відкриває та парсить оригінальний DAT.
    ///     Зберігає сирі байти для подальшого безпечного запису (WriteSafe).
    /// EN: Opens and parses the original DAT.
    ///     Stores raw bytes for subsequent safe writing (WriteSafe).
    /// </summary>
    private void OpenOriginalDat()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "DAT files (*.dat)|*.dat|All files (*.*)|*.*",
            Title = "① UA: Відкрити оригінальний DAT / EN: Open original DAT",
            InitialDirectory = DialogDir(AppFolders.Original(_gameKind == GameKind.Other
                ? GameKind.Base : _gameKind))
        };
        if (dlg.ShowDialog() != true) return;

        LoadOriginalFile(dlg.FileName);
    }

    /// <summary>
    /// UA: Завантажує оригінальний DAT із вказаного шляху. Ручні правки й статуси вичитки
    ///     зберігаються лише тоді, коли відкрито файл із тим самим набором ключів
    ///     (основна гра й доповнення мають однакові імена файлів, але різні ключі).
    ///     Усім нетехнічним рядкам без статусу вичитки ставиться «-».
    ///     Повертає false, якщо файл не вдалося прочитати.
    /// EN: Loads the original DAT from the given path. Manual edits and review statuses
    ///     are kept only when a file with the same key set is reopened (the base game
    ///     and the expansion share file names but not keys). Every non-technical row
    ///     without a review status gets "-". Returns false when the file cannot be read.
    /// </summary>
    private bool LoadOriginalFile(string path)
    {
        try
        {
            var (entries, rawBytes) = DatService.ParseOriginal(path);
            string hash = DatService.KeySetHash(entries.Select(e => e.Key));
            bool sameKeySet = _entries.Count > 0 && hash == _origHash;

            var existing = new Dictionary<string, string>(StringComparer.Ordinal);
            var existingReviews = new Dictionary<string, string>(StringComparer.Ordinal);
            if (sameKeySet)
            {
                foreach (var x in _entries.Where(x => x.IsModified))
                    existing[x.Key] = x.Translated;
                foreach (var x in _entries.Where(x => x.HasReviewMark))
                    existingReviews[x.Key] = x.ReviewStatus;
            }

            _origRaw = rawBytes;
            _origFileName = Path.GetFileName(path);
            _origHash = hash;
            _gameKind = AppFolders.Detect(path);

            _entries.Clear();
            _contextEntry = null;
            foreach (var e in entries)
            {
                if (existing.TryGetValue(e.Key, out var saved))
                    e.SetTranslatedSilent(saved);
                if (existingReviews.TryGetValue(e.Key, out var review))
                    e.ReviewStatus = review;
                _entries.Add(e);
            }
            EnsureReviewMarks();

            if (!sameKeySet) ResetTranslationCard();

            int techCount = entries.Count(e => e.IsTechnical);

            OrigFileText.Text = "📄 " + _origFileName;
            OrigFileText.FontStyle = FontStyles.Normal;
            OrigFileText.Foreground = Res("TextPrim");
            OrigMetaText.Text = $"{entries.Count} UA: записів / EN: records" +
                (techCount > 0
                    ? $" · {techCount} UA: технічних / EN: technical"
                    : "");

            OutputFileText.Text = SuggestedDatName();
            OutputFileText.FontStyle = FontStyles.Normal;

            ShowPanels();

            // UA: Скидаємо фільтри й пошук при новому файлі
            // EN: Reset filters and search for a new file
            _filterMode = "all";
            _reviewFilter = "all";
            _searchText = "";
            SearchBox.Text = "";
            SetActiveFilter("all");
            SetActiveReviewFilter("all");

            RefreshView();
            if (!sameKeySet) MarkClean();
            ShowStatus($"✓ {entries.Count} UA: записів / EN: records · «{_origFileName}»");

            SimpleLogger.Log($"UA: Відкрито оригінальний файл / EN: Opened original file: {_origFileName}");
            if (_autoSaveTimer.Interval.TotalMinutes > 0)
                _autoSaveTimer.Start();
            return true;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "LoadOriginalFile");
            MessageBox.Show(
                $"UA: Помилка читання DAT / EN: DAT read error:\n\n{ex.Message}",
                "Помилка / Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    /// <summary>
    /// UA: Повертає картку ② до початкового вигляду (новий оригінал — ще немає перекладу).
    /// EN: Returns card ② to its initial look (a new original — no translation yet).
    /// </summary>
    private void ResetTranslationCard()
    {
        TransFileText.Text = "Клацніть / Click to select...";
        TransFileText.FontStyle = FontStyles.Italic;
        TransFileText.Foreground = Res("TextDim");
        TransMetaText.Text = "UA: Зіставляється з ① по ключу / EN: Matched by key";
    }

    /// <summary>
    /// UA: Ставить «-» усім нетехнічним рядкам без статусу вичитки.
    /// EN: Sets "-" on every non-technical row without a review status.
    /// </summary>
    private void EnsureReviewMarks()
    {
        foreach (var e in _entries)
            e.EnsureReviewMark();
    }

    /// <summary>
    /// UA: Відкриває файл перекладу (TSV або DAT) і зіставляє з оригіналом.
    ///     Технічні рядки (пробільні роздільники) автоматично пропускаються —
    ///     їх переклад ламає crawl-текст та інші формати у грі.
    /// EN: Opens translation file (TSV or DAT) and merges with original.
    ///     Technical entries (whitespace separators) are skipped automatically —
    ///     translating them breaks crawl text and other formats in-game.
    /// </summary>
    private void OpenTranslationFile()
    {
        if (_entries.Count == 0)
        {
            MessageBox.Show(
                "UA: Спочатку завантажте оригінальний DAT (①)\n" +
                "EN: Load the original DAT first (①)",
                "Увага / Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new OpenFileDialog
        {
            Filter = "UA: Файли перекладу / EN: Translation files|*.tsv;*.txt;*.dat" +
                     "|TSV (*.tsv;*.txt)|*.tsv;*.txt|DAT (*.dat)|*.dat",
            Title = "② UA: Відкрити файл перекладу / EN: Open translation file",
            InitialDirectory = DialogDir(AppFolders.Translated(_gameKind))
        };
        if (dlg.ShowDialog() != true) return;

        LoadTranslationFile(dlg.FileName);
    }

    /// <summary>
    /// UA: Завантажує переклад (TSV або DAT) із вказаного шляху.
    ///     Статуси вичитки беруться: 1) з колонки ReviewStatus відкритого TSV;
    ///     2) з робочого файлу work\{гра}\{ім'я} [{хеш}].tsv цього оригіналу, якщо він є;
    ///     3) інакше рядок отримує «-». Після завантаження робочий файл створюється
    ///     (або оновлюється; попередню версію зберігає поряд із суфіксом .bak).
    /// EN: Loads a translation (TSV or DAT) from the given path.
    ///     Review statuses come from: 1) the ReviewStatus column of the opened TSV;
    ///     2) the original's working file work\{game}\{name} [{hash}].tsv, if it exists;
    ///     3) otherwise the row gets "-". After loading, the working file is created
    ///     (or updated; the previous version is kept next to it with a .bak suffix).
    /// </summary>
    private void LoadTranslationFile(string path)
    {
        try
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            int rejectedBefore = TranslationEntry.RejectedCount;

            var map = ext == ".dat"
                ? DatService.ParseDatAsTranslation(path)
                : DatService.ParseTsv(path);

            var fileReviews = ext == ".dat"
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : DatService.ParseTsvReviews(path);

            string working = WorkingFilePath();
            bool workingExisted = File.Exists(working);
            var workingReviews = workingExisted &&
                                 !string.Equals(Path.GetFullPath(working), Path.GetFullPath(path),
                                     StringComparison.OrdinalIgnoreCase)
                ? DatService.ParseTsvReviews(working)
                : new Dictionary<string, string>(StringComparer.Ordinal);

            int cnt = 0, skippedTech = 0, restoredReviews = 0;
            foreach (var entry in _entries)
            {
                // UA: Технічні рядки (пробіли/роздільники) — НІКОЛИ не перекладати!
                // EN: Technical entries (whitespace/separators) — NEVER translate!
                if (entry.IsTechnical) { skippedTech++; continue; }

                if (map.TryGetValue(entry.Key, out var t) &&
                    !string.IsNullOrWhiteSpace(t))
                {
                    entry.SetTranslatedSilent(t);
                    cnt++;
                }

                if (fileReviews.TryGetValue(entry.Key, out var review) ||
                    workingReviews.TryGetValue(entry.Key, out review))
                {
                    entry.ReviewStatus = review;
                    if (entry.WasReviewed) restoredReviews++;
                }
            }

            EnsureReviewMarks();
            WriteWorkingTsv(force: true, backup: workingExisted);

            int rejected = TranslationEntry.RejectedCount - rejectedBefore;
            _rejectedReported = TranslationEntry.RejectedCount;

            TransFileText.Text = (ext == ".dat" ? "📦 " : "📋 ") + Path.GetFileName(path);
            TransFileText.FontStyle = FontStyles.Normal;
            TransFileText.Foreground = Res("TextPrim");
            TransMetaText.Text = $"{cnt} UA: перекладів / EN: translations" +
                (restoredReviews > 0
                    ? $" · {restoredReviews} UA: статусів вичитки / EN: review statuses"
                    : "") +
                (skippedTech > 0
                    ? $" · {skippedTech} UA: технічних пропущено / EN: technical skipped"
                    : "") +
                (rejected > 0
                    ? $" · ⚠ {rejected} UA: відхилено / EN: rejected"
                    : "") +
                $" · UA: робочий файл / EN: working file: work\\{AppFolders.SubfolderName(_gameKind)}\\{Path.GetFileName(working)}" +
                (workingExisted ? "" : " (UA: створено / EN: created)");

            RefreshView();
            MarkClean();
            ShowStatus(
                $"✓ {cnt} UA: перекладів зіставлено / EN: matched " +
                $"· «{Path.GetFileName(path)}»" +
                (rejected > 0 ? $" · ⚠ {rejected} UA: відхилено / EN: rejected" : ""),
                isError: rejected > 0);

            SimpleLogger.Log($"UA: Завантажено переклад / EN: Loaded translation: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "LoadTranslationFile");
            MessageBox.Show(
                $"UA: Помилка читання перекладу / EN: Translation read error:\n\n{ex.Message}",
                "Помилка / Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ЗБЕРЕЖЕННЯ / SAVING
    // ══════════════════════════════════════════════════════════════════════════

    private void SaveButton_Click(object sender, RoutedEventArgs e) => SaveDat();

    /// <summary>
    /// UA: Зберігає новий DAT (діалог вибору файлу) і парний TSV. Повертає true лише
    ///     тоді, коли DAT записано; false — користувач скасував або сталася помилка.
    /// EN: Saves the new DAT (file dialog) and the paired TSV. Returns true only when
    ///     the DAT was written; false — the user cancelled or an error occurred.
    /// </summary>
    private bool SaveDat()
    {
        if (_origRaw.Length == 0 || _entries.Count == 0) return false;

        // UA: Фіксуємо відкрите редагування комірки перед збереженням
        // EN: Commit any open cell edit before saving
        MainGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

        // UA: Попередження якщо технічні рядки мають переклад
        // EN: Warning if technical entries have translations
        var techWithTrans = _entries.Where(x => x.IsTechnical && x.IsTranslated).ToList();
        if (techWithTrans.Count > 0)
        {
            var warn = MessageBox.Show(
                $"UA: {techWithTrans.Count} технічних рядків мають переклад — це може зламати відображення у грі!\n" +
                $"    Рекомендується натиснути «⚙ Очистити технічні».\n\n" +
                $"EN: {techWithTrans.Count} technical entries have translations — this may break in-game display!\n" +
                $"    Recommended to click «⚙ Clear Technical» first.\n\n" +
                $"UA: Продовжити все одно? / EN: Continue anyway?",
                "⚠ UA: Попередження / EN: Warning",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (warn != MessageBoxResult.Yes) return false;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "DAT files (*.dat)|*.dat",
            FileName = SuggestedDatName(),
            InitialDirectory = DialogDir(AppFolders.Translated(_gameKind)),
            Title = "③ UA: Зберегти новий DAT / EN: Save new DAT"
        };
        if (dlg.ShowDialog() != true) return false;

        return WriteDatFile(dlg.FileName);
    }

    /// <summary>
    /// UA: Записує DAT за шляхом path (структура — побайтово з завантаженого оригіналу),
    ///     оновлює робочий TSV і пише парний TSV з тим самим ім'ям. Існуючий файл за цим
    ///     шляхом перезаписується лише тоді, коли шлях обрав користувач у діалозі збереження.
    ///     Повертає true, якщо DAT записано.
    /// EN: Writes the DAT to path (structure byte-perfect from the loaded original),
    ///     updates the working TSV and writes a paired TSV with the same name. An existing
    ///     file at that path is overwritten only when the user chose the path in the save
    ///     dialog. Returns true when the DAT was written.
    /// </summary>
    private bool WriteDatFile(string path)
    {
        try
        {
            DatService.WriteSafe(path, _origRaw, _entries.ToList());
            WriteWorkingTsv(force: true);

            // UA: Парний TSV зі статусами з тим самим ім'ям і міткою часу, що й DAT.
            // EN: A paired TSV with statuses, named and time-stamped like the DAT.
            WriteWorkingSnapshot(path);

            OutputFileText.Text = Path.GetFileName(path);
            OutputFileText.FontStyle = FontStyles.Normal;
            ShowStatus(
                $"✓ UA: Збережено / EN: Saved «{Path.GetFileName(path)}» " +
                "· UA: структура збережена побайтово / EN: byte-perfect");

            SimpleLogger.Log($"UA: Файл збережено / EN: File saved: {path}");
            MarkClean();
            return true;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "WriteDatFile");
            MessageBox.Show(
                $"UA: Помилка збереження / EN: Save error:\n\n{ex.Message}",
                "Помилка / Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    /// <summary>
    /// UA: Шлях нового DAT у теці перекладу гри: {ім'я} {yyMMdd HHmm}.dat; якщо файл із
    ///     такою назвою вже є, додається « (2)», « (3)» тощо, тож наявний файл не чіпається.
    /// EN: Path of a new DAT in the game's translation folder: {name} {yyMMdd HHmm}.dat; when
    ///     a file with that name exists, " (2)", " (3)" and so on is appended, so an existing
    ///     file is never touched.
    /// </summary>
    private string NewDatPath()
    {
        string folder = DialogDir(AppFolders.Translated(_gameKind));
        string baseName = Path.GetFileNameWithoutExtension(SuggestedDatName());
        string path = Path.Combine(folder, baseName + ".dat");
        for (int n = 2; File.Exists(path); n++)
            path = Path.Combine(folder, $"{baseName} ({n}).dat");
        return path;
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_entries.Count == 0) return;
        var dlg = new SaveFileDialog
        {
            Filter = "TSV files (*.tsv)|*.tsv",
            FileName = $"{Path.GetFileNameWithoutExtension(_origFileName)} {AppFolders.Stamp()}.tsv",
            InitialDirectory = DialogDir(AppFolders.Work(_gameKind)),
            Title = "UA: Експортувати TSV / EN: Export TSV"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            DatService.ExportTsv(dlg.FileName, _entries);
            ShowStatus(
                $"✓ UA: Експортовано / EN: Exported {_entries.Count} " +
                $"UA: рядків / EN: rows · «{Path.GetFileName(dlg.FileName)}»");

            SimpleLogger.Log($"UA: Експортовано TSV / EN: Exported TSV: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ExportButton_Click");
            MessageBox.Show(
                $"UA: Помилка / EN: Error:\n\n{ex.Message}",
                "Помилка / Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // АВТОЗБЕРЕЖЕННЯ / AUTOSAVE
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// UA: Читає інтервал з конфігу. Якщо 0 — вимикає таймер.
    /// EN: Reads interval from config. If 0 — disables timer.
    /// </summary>
    private void ApplyAutoSaveSettings()
    {
        int interval = 5;
        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "eaw_localizer_config.json");

        try
        {
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                var cfg = JsonSerializer.Deserialize<ConfigData>(json);
                if (cfg != null) interval = cfg.AutoSaveIntervalMinutes;
            }
        }
        catch { }

        if (interval > 0)
        {
            _autoSaveTimer.Interval = TimeSpan.FromMinutes(interval);
            if (_origRaw.Length > 0 && !_autoSaveTimer.IsEnabled)
                _autoSaveTimer.Start();
        }
        else
        {
            _autoSaveTimer.Stop();
        }
    }

    private void AutoSaveTimer_Tick(object? sender, EventArgs e)
    {
        PerformAutoSave();
    }

    /// <summary>
    /// UA: Шлях робочого TSV поточного оригіналу: work\{гра}\{ім'я} [{хеш ключів}].tsv.
    ///     Хеш відрізняє основну гру від доповнення, у яких файл називається однаково.
    /// EN: Path of the current original's working TSV: work\{game}\{name} [{key hash}].tsv.
    ///     The hash tells the base game from the expansion, whose files share a name.
    /// </summary>
    private string WorkingFilePath() =>
        Path.Combine(AppFolders.Work(_gameKind),
            $"{Path.GetFileNameWithoutExtension(_origFileName)} [{_origHash}].tsv");

    /// <summary>
    /// UA: Запропоноване ім'я збереженого DAT: {ім'я} {yyMMdd HHmm}.dat.
    /// EN: Suggested name of the saved DAT: {name} {yyMMdd HHmm}.dat.
    /// </summary>
    private string SuggestedDatName() =>
        $"{Path.GetFileNameWithoutExtension(_origFileName)} {AppFolders.Stamp()}.dat";

    /// <summary>
    /// UA: Повертає теку для діалогу (створює, якщо її немає); порожній рядок —
    ///     діалог відкриється там, де система вважає за потрібне.
    /// EN: Returns a folder for a dialog (creates it when missing); an empty string
    ///     lets the system choose the starting folder.
    /// </summary>
    private static string DialogDir(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            return folder;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "DialogDir");
            return string.Empty;
        }
    }

    /// <summary>
    /// UA: Записує парний TSV поруч із робочими файлами: ім'я збереженого DAT без
    ///     розширення + «.tsv» (мітка часу береться з імені DAT).
    /// EN: Writes a paired TSV among the working files: the saved DAT's name without
    ///     extension + ".tsv" (the timestamp comes from the DAT's name).
    /// </summary>
    private void WriteWorkingSnapshot(string datPath)
    {
        try
        {
            string folder = DialogDir(AppFolders.Work(_gameKind));
            if (folder.Length == 0) return;
            string path = Path.Combine(folder, Path.GetFileNameWithoutExtension(datPath) + ".tsv");
            DatService.ExportTsv(path, _entries);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "WriteWorkingSnapshot");
        }
    }

    /// <summary>
    /// UA: Записує робочий TSV (Key, OriginalText, TranslatedText, ReviewStatus) у теку
    ///     «work\{гра}» поруч із програмою. DAT не містить статусів вичитки, тому цей файл —
    ///     єдине місце, де вони зберігаються між сеансами. Завантажується назад як
    ///     джерело перекладу (②). Файл не потрапляє в теку гри чи моду.
    ///     force — записати навіть без жодного перекладу чи позначки; backup — перед
    ///     записом зберегти попередню версію як .bak.
    /// EN: Writes the working TSV (Key, OriginalText, TranslatedText, ReviewStatus) into the
    ///     "work\{game}" folder next to the executable. A DAT cannot carry review statuses, so
    ///     this file is the only place they persist between sessions. It loads back as a
    ///     translation source (②). The file never lands in the game or mod folder.
    ///     force — write even without any translation or mark; backup — keep the previous
    ///     version as .bak before writing.
    /// </summary>
    private void WriteWorkingTsv(bool force = false, bool backup = false)
    {
        try
        {
            if (_entries.Count == 0 || string.IsNullOrEmpty(_origFileName)) return;
            if (!force && !_entries.Any(x => x.IsTranslated || x.WasReviewed)) return;

            Directory.CreateDirectory(AppFolders.Work(_gameKind));
            string path = WorkingFilePath();
            if (backup && File.Exists(path))
                File.Copy(path, path + ".bak", overwrite: true);

            DatService.ExportTsv(path, _entries);
        }
        catch (Exception ex)
        {
            // UA: Збій запису робочого TSV не повинен переривати збереження DAT
            // EN: A working-TSV write failure must not interrupt saving the DAT
            SimpleLogger.LogError(ex, "WriteWorkingTsv");
        }
    }

    /// <summary>
    /// UA: Виконує тихе збереження у файл _AUTOSAVE.dat у теці work\{гра}.
    /// EN: Performs silent save to _AUTOSAVE.dat in work\{game}.
    /// </summary>
    private void PerformAutoSave()
    {
        if (_origRaw.Length == 0 || _entries.Count == 0) return;
        if (!_entries.Any(x => x.IsModified || x.WasReviewed)) return;

        try
        {
            MainGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

            string autoSaveName = Path.GetFileNameWithoutExtension(_origFileName) + "_AUTOSAVE.dat";
            string autoSavePath = Path.Combine(DialogDir(AppFolders.Work(_gameKind)), autoSaveName);

            DatService.WriteSafe(autoSavePath, _origRaw, _entries.ToList());
            WriteWorkingTsv();

            SimpleLogger.Log($"UA: Автозбереження успішне / EN: Autosave successful: {autoSaveName}");
            ShowStatus($"💾 UA: Автозбереження виконано / EN: Autosaved о {DateTime.Now:HH:mm}");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "PerformAutoSave");
            ShowStatus("⚠ UA: Помилка автозбереження / EN: Autosave failed", isError: true);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ФІЛЬТРАЦІЯ ТА ПОШУК / FILTERING AND SEARCH
    // ══════════════════════════════════════════════════════════════════════════

    private bool FilterEntry(object obj)
    {
        if (obj is not TranslationEntry e) return false;

        bool passes = _filterMode switch
        {
            // UA: "un" виключає технічні — для них є окремий фільтр "tech"
            // EN: "un" excludes technical — they have separate "tech" filter
            "un" => !e.IsTranslated && !e.IsTechnical,
            "tr" => e.IsTranslated && !e.IsTechnical,
            "mod" => e.IsModified,
            "issues" => e.HasValidationIssue,
            "dup" => e.IsDuplicate,
            "dupdiff" => (e.IsDuplicate || e.HasSimilar) && e.DuplicateDiffer,
            "similar" => e.HasSimilar,
            "tech" => e.IsTechnical,
            _ => true
        };
        if (!passes) return false;

        // UA: Другий, незалежний фільтр — за позначкою вичитки (технічні рядки не вичитуються)
        // EN: A second, independent filter — by review mark (technical rows are not reviewed)
        if (_reviewFilter != "all")
        {
            if (e.IsTechnical) return false;

            var kind = e.ReviewMarkKind;
            bool reviewPasses = _reviewFilter switch
            {
                "plus" => kind == ReviewKind.Plus,
                "unsure" => kind == ReviewKind.Unsure,
                "none" => kind == ReviewKind.None,
                "comment" => kind == ReviewKind.Comment,
                _ => true
            };
            if (!reviewPasses) return false;
        }

        if (string.IsNullOrEmpty(_searchText)) return true;

        // UA: Пошук за ключем, оригіналом, перекладом і статусом вичитки без урахування регістру
        // EN: Case-insensitive search across key, original, translation and review status
        string q = _searchText.ToLower();
        return e.Key.ToLower().Contains(q)
            || e.Original.ToLower().Contains(q)
            || e.Translated.ToLower().Contains(q)
            || e.ReviewStatus.ToLower().Contains(q);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text;
        RefreshView();
    }

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            _filterMode = tag;
            SetActiveFilter(tag);
            RefreshView();
        }
    }

    private void ReviewFilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            _reviewFilter = tag;
            SetActiveReviewFilter(tag);
            RefreshView();
        }
    }

    /// <summary>
    /// UA: Скидає сортування DataGrid до початкового (порядок оригінального файлу).
    /// EN: Resets DataGrid sorting to initial state (original file order).
    /// </summary>
    private void ClearSort_Click(object sender, RoutedEventArgs e)
    {
        MainGrid.Items.SortDescriptions.Clear();
        foreach (var col in MainGrid.Columns)
            col.SortDirection = null;
        _view.Refresh();
    }

    /// <summary>
    /// UA: Перераховує групи дублікатів. Дублікат — нетехнічні рядки з ТОЧНО однаковим
    ///     англійським текстом. Схожі — рядки, текст яких збігається лише після
    ///     нормалізації (регістр, пробіли): це різні рядки гри. «Різні» — переклади в групі
    ///     (дублікати разом зі схожими) відрізняються за змістом: у точних дублікатів —
    ///     будь-яка різниця (крім пробілів і переносів на краях), між схожими рядками —
    ///     різниця, більша за регістр і пробіли. Порожній переклад теж вважається відмінним.
    /// EN: Recomputes the duplicate groups. A duplicate is a set of non-technical rows
    ///     with EXACTLY the same English text. Similar rows are rows whose text matches
    ///     only after normalization (case, whitespace): they are different game strings.
    ///     "Differ" — the translations within the group (duplicates together with similar
    ///     rows) differ in meaning: any difference among exact duplicates (except edge
    ///     whitespace and line breaks), a difference beyond case and whitespace among
    ///     similar rows. An empty translation counts as different too.
    /// </summary>
    private void RecomputeDuplicates()
    {
        var exact = new Dictionary<string, List<TranslationEntry>>(StringComparer.Ordinal);
        foreach (var e in _entries)
        {
            if (e.IsTechnical || string.IsNullOrWhiteSpace(e.Original)) continue;

            if (!exact.TryGetValue(e.Original, out var list))
                exact[e.Original] = list = [];
            list.Add(e);
        }

        // UA: Нормалізований текст → точні групи, які до нього належать.
        // EN: Normalized text → the exact groups that belong to it.
        var byNorm = new Dictionary<string, List<List<TranslationEntry>>>(StringComparer.Ordinal);
        foreach (var list in exact.Values)
        {
            string norm = DatTranslationTransfer.NormalizeText(list[0].Original);
            if (!byNorm.TryGetValue(norm, out var groups))
                byNorm[norm] = groups = [];
            groups.Add(list);
        }

        // UA: Кожен рядок отримує значення рівно один раз — без проміжного скидання.
        // EN: Every row is assigned exactly once — no intermediate reset.
        var assigned = new HashSet<TranslationEntry>();
        foreach (var groups in byNorm.Values)
        {
            int total = groups.Sum(g => g.Count);
            // UA: Усередині групи з ТОЧНО однаковим оригіналом переклади мають збігатися
            //     (пробіли на краях не враховуються). Між схожими групами оригінали
            //     різняться регістром чи пробілами, тож і переклади можуть різнитися лише
            //     регістром та пробілами; відмінність за змістом — це різниця.
            // EN: Within a group with an EXACTLY identical original the translations must
            //     match (edge whitespace is ignored). Between similar groups the originals
            //     differ in case or whitespace, so the translations may differ in case and
            //     whitespace alone too; a difference in meaning is a real difference.
            bool differ = total > 1 &&
                          (groups.Any(g => g.Select(x => x.Translated.Trim())
                                            .Distinct(StringComparer.Ordinal).Count() > 1) ||
                           groups.SelectMany(g => g)
                                 .Select(x => DatTranslationTransfer.NormalizeText(x.Translated))
                                 .Distinct(StringComparer.Ordinal).Count() > 1);

            foreach (var list in groups)
            {
                int similar = total - list.Count;
                int count = list.Count >= 2 ? list.Count : 0;
                string tip = (count == 0 && similar == 0)
                    ? ""
                    : BuildDuplicateTip(list, groups, count, similar, differ);

                foreach (var e in list)
                {
                    e.SetDuplicateInfo(count, differ && (count > 0 || similar > 0), similar, tip);
                    assigned.Add(e);
                }
            }
        }

        foreach (var e in _entries)
            if (!assigned.Contains(e))
                e.SetDuplicateInfo(0, false, 0, "");
    }

    /// <summary>
    /// UA: Підказка до колонки «Дубл.»: скільки точних дублікатів і схожих рядків, у чому
    ///     саме схожі відрізняються від цього рядка та чи різні переклади.
    /// EN: The tooltip for the "Dup" column: how many exact duplicates and similar rows
    ///     there are, exactly how the similar ones differ from this row, and whether
    ///     the translations differ.
    /// </summary>
    private static string BuildDuplicateTip(
        List<TranslationEntry> own, List<List<TranslationEntry>> groups,
        int count, int similar, bool differ)
    {
        var sb = new System.Text.StringBuilder();
        if (count > 0)
            sb.AppendLine($"×{count} — UA: точні дублікати (однаковий англійський текст) / EN: exact duplicates (identical English text)");

        if (similar > 0)
        {
            var parts = new SortedSet<string>(StringComparer.Ordinal);
            string? example = null;
            foreach (var other in groups)
            {
                if (ReferenceEquals(other, own)) continue;
                example ??= other[0].Key;
                foreach (var p in DescribeTextDifference(own[0].Original, other[0].Original))
                    parts.Add(p);
            }

            sb.AppendLine($"≈{similar} — UA: схожі рядки / EN: similar rows");
            sb.AppendLine($"    UA: відрізняються / EN: differ: {string.Join("; ", parts)}");
            if (example is not null)
                sb.AppendLine($"    UA: приклад / EN: example: {example}");
        }

        sb.Append(differ
            ? "UA: Переклади різні за змістом (різниця лише в регістрі чи пробілах між схожими рядками не враховується) / EN: Translations differ in meaning (a difference in case or whitespace alone between similar rows is ignored)"
            : "UA: Переклади однакові / EN: Translations are equal");
        return sb.ToString();
    }

    /// <summary>
    /// UA: Описує, чим відрізняються два схожі оригінали: регістр, пробіл чи перенос на
    ///     початку або в кінці, пробіли всередині.
    /// EN: Describes how two similar originals differ: case, a space or line break at the
    ///     start or end, whitespace inside.
    /// </summary>
    private static IEnumerable<string> DescribeTextDifference(string a, string b)
    {
        static string Lead(string t) => t[..(t.Length - t.TrimStart().Length)];
        static string Tail(string t) => t[t.TrimEnd().Length..];
        static string Kind(string x, string y) =>
            (x + y).Any(c => c is '\n' or '\r') ? "UA: перенос / EN: line break" : "UA: пробіл / EN: space";
        static string Collapse(string t) =>
            System.Text.RegularExpressions.Regex.Replace(t.Trim(), @"\s+", " ");

        var result = new List<string>();
        string ca = Collapse(a), cb = Collapse(b);

        if (!string.Equals(ca, cb, StringComparison.Ordinal))
            result.Add("UA: регістр / EN: case");
        else if (!string.Equals(a.Trim(), b.Trim(), StringComparison.Ordinal))
            result.Add("UA: пробіли всередині / EN: whitespace inside");

        if (!string.Equals(Lead(a), Lead(b), StringComparison.Ordinal))
            result.Add($"UA: початок / EN: start ({Kind(Lead(a), Lead(b))})");
        if (!string.Equals(Tail(a), Tail(b), StringComparison.Ordinal))
            result.Add($"UA: кінець / EN: end ({Kind(Tail(a), Tail(b))})");

        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // СТАРТОВИЙ ЕКРАН / START SCREEN
    // ══════════════════════════════════════════════════════════════════════════

    // UA: Відбиток стану (переклади + статуси вичитки) на момент останнього збереження
    //     DAT або завантаження файлів; різниця означає незбережені зміни.
    // EN: A fingerprint of the state (translations + review statuses) at the last DAT
    //     save or file load; a difference means unsaved changes.
    private int _cleanHash;

    private int StateHash()
    {
        var h = new HashCode();
        foreach (var e in _entries)
        {
            h.Add(e.Key, StringComparer.Ordinal);
            h.Add(e.Translated, StringComparer.Ordinal);
            h.Add(e.ReviewStatus, StringComparer.Ordinal);
        }
        return h.ToHashCode();
    }

    private void MarkClean() => _cleanHash = StateHash();

    /// <summary>
    /// UA: Кнопка «На початок»: за незбережених змін пропонує зберегти DAT, далі
    ///     закриває відкриті файли й повертає стартовий екран.
    /// EN: The "Start" button: with unsaved changes it offers to save the DAT, then
    ///     closes the open files and returns the start screen.
    /// </summary>
    private void HomeButton_Click(object sender, RoutedEventArgs e)
    {
        MainGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

        if (_entries.Count > 0 && StateHash() != _cleanHash)
        {
            var answer = MessageBox.Show(
                "UA: Є незбережені зміни. Зберегти DAT перед поверненням на початок?\n" +
                "    Так — зберегти DAT; Ні — повернутися без збереження DAT " +
                "(переклади й статуси лишаються в робочому файлі теки work); Скасувати — залишитися.\n\n" +
                "EN: There are unsaved changes. Save the DAT before returning to the start?\n" +
                "    Yes — save the DAT; No — return without saving the DAT " +
                "(translations and statuses stay in the working file in the work folder); Cancel — stay.",
                "UA: На початок / EN: Start",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (answer == MessageBoxResult.Cancel) return;
            if (answer == MessageBoxResult.Yes)
            {
                if (!SaveDat()) return;
            }
            else
            {
                WriteWorkingTsv();
            }
        }

        ResetToStart();
    }

    /// <summary>
    /// UA: Повертає програму до стану після запуску: файли закрито, панелі сховано.
    /// EN: Returns the application to its post-startup state: files closed, panels hidden.
    /// </summary>
    private void ResetToStart()
    {
        _autoSaveTimer.Stop();

        _entries.Clear();
        _contextEntry = null;
        _origRaw = [];
        _origFileName = "";
        _origHash = "";
        _gameKind = GameKind.Other;
        _cleanHash = 0;

        OrigFileText.Text = "Клацніть / Click to select...";
        OrigFileText.FontStyle = FontStyles.Italic;
        OrigFileText.Foreground = Res("TextDim");
        OrigMetaText.Text = "UA: Джерело CRC32 · ключів · структури\nEN: Source of CRC32 · keys · structure";
        ResetTranslationCard();
        OutputFileText.Text = "Після «Зберегти» / After save";
        OutputFileText.FontStyle = FontStyles.Italic;

        _filterMode = "all";
        _reviewFilter = "all";
        _searchText = "";
        SearchBox.Text = "";
        SetActiveFilter("all");
        SetActiveReviewFilter("all");

        HidePanels();
        RefreshView();
        ShowStatus("⌂ UA: Файли закрито / EN: Files closed");
    }

    /// <summary>
    /// UA: Клік на картці ③ зберігає DAT — те саме, що кнопка «Зберегти DAT».
    /// EN: A click on card ③ saves the DAT — the same as the "Save DAT" button.
    /// </summary>
    private void OutCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SaveButton.IsEnabled) SaveDat();
    }

    private void RefreshView()
    {
        // UA: CollectionView не дозволяє Refresh під час відкритої транзакції редагування
        // EN: CollectionView does not allow Refresh while an edit transaction is open
        MainGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
        RecomputeDuplicates();
        _view.Refresh();
        UpdateStats();
        int visible = _entries.Count(FilterEntry);
        FilterCountText.Text = $"{visible} / {_entries.Count}";
    }

    private void UpdateStats()
    {
        int total = _entries.Count;
        int tech = _entries.Count(e => e.IsTechnical);
        int normal = total - tech;
        int done = _entries.Count(e => e.IsTranslated && !e.IsTechnical);
        int mod = _entries.Count(e => e.IsModified);
        int issues = _entries.Count(e => e.HasValidationIssue);
        int dup = _entries.Count(e => e.IsDuplicate);
        int dupDiff = _entries.Count(e => (e.IsDuplicate || e.HasSimilar) && e.DuplicateDiffer);
        int similar = _entries.Count(e => e.HasSimilar);

        int plus = 0, unsure = 0, notReviewed = 0, comment = 0;
        foreach (var e in _entries)
        {
            if (e.IsTechnical) continue;
            switch (e.ReviewMarkKind)
            {
                case ReviewKind.Plus: plus++; break;
                case ReviewKind.Unsure: unsure++; break;
                case ReviewKind.Comment: comment++; break;
                default: notReviewed++; break;
            }
        }

        int reviewed = plus + unsure;
        int un = normal - done;
        int pct = normal > 0 ? (int)(done * 100.0 / normal) : 0;
        int reviewPct = normal > 0 ? (int)(reviewed * 100.0 / normal) : 0;

        HeaderStatText.Text = total > 0
            ? $"{done}/{normal} · {pct}% · ✓ {reviewed} · {mod} UA: правок / EN: edits · {issues} ⚠"
            : "";

        if (total > 0)
        {
            ProgressBar.Value = pct;
            MergeStatText.Text = $"{done}/{normal} ({pct}%)";
            MergeWarnText.Text = un > 0
                ? $"⚠ {un} UA: без перекладу / EN: untranslated"
                : "✔ UA: Всі перекладено / EN: All translated";
            MergeWarnText.Foreground = un > 0 ? Res("StatusAmber") : Res("StatusGreen");
            ReviewStatText.Text =
                $"✓ UA: вичитано / EN: reviewed {reviewed}/{normal} ({reviewPct}%)";
        }

        FilterAll.Content = $"UA: Всі / EN: All · {total}";
        FilterUn.Content = $"UA: Без / EN: Untranslated · {un}";
        FilterTr.Content = $"UA: Перекл. / EN: Translated · {done}";
        FilterMod.Content = $"UA: Змінено / EN: Modified · {mod}";
        FilterIssues.Content = $"⚠ UA: Проблемні / EN: Issues · {issues}";
        FilterDup.Content = $"⧉ UA: Дублікати / EN: Duplicates · {dup}";
        FilterDupDiff.Content = $"≠ UA: Дубл. різні / EN: Dup. differ · {dupDiff}";
        FilterSimilar.Content = $"≈ UA: Схожі / EN: Similar · {similar}";
        FilterTech.Content = $"⚙ UA: Технічні / EN: Technical · {tech}";

        ReviewAll.Content = $"UA: Усі / EN: All · {normal}";
        ReviewPlus.Content = $"+ · {plus}";
        ReviewUnsure.Content = $"+/- · {unsure}";
        ReviewNone.Content = $"- · {notReviewed}";
        ReviewComment.Content = $"💬 UA: Коментар / EN: Comment · {comment}";
    }

    private void SetActiveFilter(string active)
    {
        var buttons = new[] { FilterAll, FilterUn, FilterTr, FilterMod, FilterIssues, FilterDup, FilterDupDiff, FilterSimilar, FilterTech };
        var tags = new[] { "all", "un", "tr", "mod", "issues", "dup", "dupdiff", "similar", "tech" };
        HighlightFilter(buttons, tags, active);
    }

    private void SetActiveReviewFilter(string active)
    {
        var buttons = new[] { ReviewAll, ReviewPlus, ReviewUnsure, ReviewNone, ReviewComment };
        var tags = new[] { "all", "plus", "unsure", "none", "comment" };
        HighlightFilter(buttons, tags, active);
    }

    private static void HighlightFilter(Button[] buttons, string[] tags, string active)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            bool on = tags[i] == active;
            buttons[i].Background = on ? Res("AccentDark") : Res("BgCard");
            // UA: Світлий текст на темному фоні активної кнопки в обох темах.
            // EN: Light text on the active button's dark background in both themes.
            buttons[i].Foreground = on ? Brushes.White : Res("TextPrim");
            buttons[i].BorderBrush = on ? Res("Accent") : Res("BdAcc");
            buttons[i].FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ТЕХНІЧНІ РЯДКИ / TECHNICAL ENTRIES
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// UA: Кнопка «Вирівняти краї»: для всіх перекладених нетехнічних рядків робить пробіли
    ///     й переноси на початку та в кінці такими, як в оригіналі. Текст між краями не
    ///     змінюється; позначки вичитки лишаються, бо зміст рядка той самий.
    /// EN: The "Align edges" button: for every translated non-technical row, makes the
    ///     whitespace and line breaks at the start and end match the original. The text
    ///     between the edges is not changed; review marks stay, because the row's content
    ///     is the same.
    /// </summary>
    private void AlignEdges_Click(object sender, RoutedEventArgs e)
    {
        MainGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

        var fixes = new List<(TranslationEntry Entry, string Aligned)>();
        foreach (var x in _entries)
        {
            if (x.IsTechnical || !x.IsTranslated) continue;

            string aligned = ValidationService.AlignEdgeWhitespace(x.Original, x.Translated);
            if (!string.Equals(aligned, x.Translated, StringComparison.Ordinal))
                fixes.Add((x, aligned));
        }

        if (fixes.Count == 0)
        {
            ShowStatus("ℹ UA: Краї всіх перекладів збігаються з оригіналом / EN: The edges of all translations match the original");
            return;
        }

        var answer = MessageBox.Show(
            $"UA: Буде вирівняно краї в {fixes.Count} рядках: пробіли й переноси на початку та в кінці " +
            "стануть такими, як в оригіналі. Текст між краями не змінюється.\n\n" +
            "UA: Продовжити?\n\n" +
            $"EN: The edges of {fixes.Count} rows will be aligned: the whitespace and line breaks at the " +
            "start and end will match the original. The text between the edges is not changed.\n\n" +
            "EN: Continue?",
            "UA: Вирівняти краї / EN: Align edges",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        foreach (var (entry, aligned) in fixes)
            entry.Translated = aligned;

        RefreshView();
        ShowStatus($"↔ UA: Вирівняно країв: {fixes.Count} / EN: Edges aligned: {fixes.Count}");
        SimpleLogger.Log($"UA: Вирівняно краї / EN: Edges aligned: {fixes.Count}");
    }

    /// <summary>
    /// UA: Очищає переклади для ВСІХ технічних рядків.
    ///     Критично важливо для виправлення crawl-тексту!
    ///     Технічні рядки (пробільні роздільники) завжди мають зберігатись як оригінал:
    ///     WriteSafe при порожньому Translated копіює оригінальні байти без змін.
    /// EN: Clears translations for ALL technical entries.
    ///     Critical for fixing crawl text display!
    ///     Technical entries (whitespace separators) must always use original bytes:
    ///     WriteSafe with empty Translated copies original bytes unchanged.
    /// </summary>
    private void ClearTechnical_Click(object sender, RoutedEventArgs e)
    {
        var techWithTrans = _entries
            .Where(x => x.IsTechnical && x.IsTranslated)
            .ToList();

        if (techWithTrans.Count == 0)
        {
            ShowStatus(
                "ℹ UA: Технічних рядків з перекладом не знайдено " +
                "/ EN: No translated technical entries found");
            return;
        }

        var result = MessageBox.Show(
            $"UA: Очистити переклад для {techWithTrans.Count} технічних рядків?\n" +
            $"    WriteSafe збереже оригінальні байти при наступному записі.\n\n" +
            $"EN: Clear translation for {techWithTrans.Count} technical entries?\n" +
            $"    WriteSafe will preserve original bytes on next save.",
            "UA: Очистити технічні / EN: Clear Technical",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        foreach (var entry in techWithTrans)
            entry.ClearTranslation();

        RefreshView();
        ShowStatus(
            $"✓ UA: Очищено / EN: Cleared {techWithTrans.Count} " +
            "UA: технічних рядків / EN: technical entries");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // КОНТЕКСТНЕ МЕНЮ (ПКМ ПО РЯДКУ) / CONTEXT MENU (RMB ON ROW)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// UA: Обробляє натискання ПКМ на рядку. Якщо рядок ще не виділений —
    ///     виділення замінюється цим рядком; якщо вже входить до виділення —
    ///     мультивиділення зберігається, щоб пункти «Вичитка для виділених»
    ///     діяли на всі виділені рядки. Клікнутий рядок запам'ятовується
    ///     для пунктів меню, що працюють з одним рядком.
    /// EN: Handles an RMB press on a row. If the row is not yet selected, the
    ///     selection is replaced by it; if it is already part of the selection,
    ///     the multi-selection is kept so the "Review for selected" items act
    ///     on all selected rows. The clicked row is remembered for the menu
    ///     items that work on a single row.
    /// </summary>
    private void Row_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow row || row.Item is not TranslationEntry entry) return;

        if (!row.IsSelected)
        {
            MainGrid.SelectedItems.Clear();
            row.IsSelected = true;
        }

        _contextEntry = entry;
        MainGrid.Focus();
    }

    /// <summary>
    /// UA: Рядок, до якого застосовуються пункти меню для одного рядка:
    ///     клікнутий ПКМ, а за його відсутності — поточний SelectedItem.
    /// EN: The row that single-row menu items act on: the one clicked with RMB,
    ///     or the current SelectedItem when there is none.
    /// </summary>
    private TranslationEntry? GetContextEntry() =>
        _contextEntry ?? MainGrid.SelectedItem as TranslationEntry;

    /// <summary>
    /// UA: Копіює оригінальний текст поточного рядка у буфер обміну.
    /// EN: Copies original text of current row to clipboard.
    /// </summary>
    private void CtxCopyOriginal_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextEntry() is { } entry &&
            !string.IsNullOrEmpty(entry.Original))
        {
            Clipboard.SetText(entry.Original);
            ShowStatus($"📋 UA: Оригінал скопійовано / EN: Original copied · «{entry.Key}»");
        }
    }

    /// <summary>
    /// UA: Вставляє оригінальний текст у поле перекладу.
    ///     Корисно коли потрібні мінімальні правки від оригіналу.
    ///     Для технічних рядків заборонено — показує попередження.
    /// EN: Pastes original text into translation field.
    ///     Useful when only minor edits from original are needed.
    ///     Blocked for technical entries — shows warning.
    /// </summary>
    private void CtxPasteOriginalAsTranslation_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextEntry() is not { } entry) return;

        if (entry.IsTechnical)
        {
            MessageBox.Show(
                "UA: Це технічний рядок (пробільний роздільник) — його не можна перекладати!\n" +
                "EN: This is a technical entry (whitespace separator) — do not translate it!",
                "⚠ UA: Увага / EN: Warning",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        entry.Translated = entry.Original;
        ShowStatus(
            $"📝 UA: Оригінал вставлено як переклад / EN: Original pasted as translation " +
            $"· «{entry.Key}»");
    }

    /// <summary>
    /// UA: Очищає переклад поточного рядка.
    ///     WriteSafe автоматично збереже оригінальні байти для порожнього поля.
    /// EN: Clears translation of current row.
    ///     WriteSafe automatically saves original bytes for empty field.
    /// </summary>
    private void CtxClearTranslation_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextEntry() is { } entry)
        {
            entry.ClearTranslation();
            RefreshView();
            ShowStatus(
                $"🗑 UA: Переклад очищено / EN: Translation cleared · «{entry.Key}»");
        }
    }

    /// <summary>
    /// UA: Копіює переклад поточного рядка в усі рядки з таким самим англійським
    ///     текстом. Вичитані рядки (позначка, відмінна від «-») за замовчуванням не
    ///     змінюються; користувач може дозволити й їх, тоді їхні «+» та «+/-» скидаються
    ///     на «-», бо текст уже не той, що вичитувався. Коментарі лишаються.
    /// EN: Copies the current row's translation into every row with the same English
    ///     text. Reviewed rows (a mark other than "-") are left alone by default; the
    ///     user can allow them too, in which case their "+" and "+/-" reset to "-",
    ///     because the text is no longer the one that was reviewed. Comments stay.
    /// </summary>
    private void ApplyTranslationToDuplicates(TranslationEntry source)
    {
        if (!source.IsDuplicate)
        {
            ShowStatus("ℹ UA: Дублікатів немає / EN: No duplicates");
            return;
        }
        if (!source.IsTranslated)
        {
            ShowStatus("ℹ UA: Рядок без перекладу — нічого застосовувати / EN: The row has no translation — nothing to apply");
            return;
        }

        var targets = _entries
            .Where(x => !ReferenceEquals(x, source) && !x.IsTechnical &&
                        string.Equals(x.Original, source.Original, StringComparison.Ordinal) &&
                        !string.Equals(x.Translated, source.Translated, StringComparison.Ordinal))
            .ToList();

        if (targets.Count == 0)
        {
            ShowStatus("ℹ UA: Усі дублікати вже мають такий самий переклад / EN: All duplicates already have the same translation");
            return;
        }

        int overwritten = targets.Count(x => x.IsTranslated);
        int reviewed = targets.Count(x => x.WasReviewed);
        bool includeReviewed = false;

        string summary =
            $"UA: Буде змінено {targets.Count} рядків (із них {overwritten} уже мають інший переклад).\n" +
            $"EN: {targets.Count} rows will change ({overwritten} of them already have a different translation).";

        if (reviewed > 0)
        {
            var answer = MessageBox.Show(
                summary + "\n\n" +
                $"UA: Вичитаних рядків: {reviewed}.\n" +
                "    Так — змінити лише невичитані; Ні — змінити й вичитані (їхні «+» та «+/-» стануть «-»).\n" +
                $"EN: Reviewed rows: {reviewed}.\n" +
                "    Yes — change unreviewed rows only; No — change reviewed rows too (their \"+\" and \"+/-\" become \"-\").",
                "UA: Застосувати до дублікатів / EN: Apply to duplicates",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel) return;
            includeReviewed = answer == MessageBoxResult.No;
        }
        else if (overwritten > 0)
        {
            var answer = MessageBox.Show(
                summary + "\n\nUA: Продовжити? / EN: Continue?",
                "UA: Застосувати до дублікатів / EN: Apply to duplicates",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
        }

        int changed = 0;
        foreach (var t in targets)
        {
            if (t.WasReviewed && !includeReviewed) continue;

            t.Translated = source.Translated;
            if (t.ReviewMarkKind is ReviewKind.Plus or ReviewKind.Unsure)
                t.ReviewStatus = TranslationEntry.NotReviewedMark;
            changed++;
        }

        RefreshView();
        ShowStatus(
            $"⧉ UA: Застосовано до {changed} дублікатів / EN: Applied to {changed} duplicates" +
            (changed < targets.Count
                ? $" · {targets.Count - changed} UA: вичитаних пропущено / EN: reviewed skipped"
                : ""));
    }

    private void CtxApplyToDuplicates_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextEntry() is { } entry)
            ApplyTranslationToDuplicates(entry);
    }

    /// <summary>
    /// UA: Копіює переклад поточного рядка в «схожі» рядки (той самий нормалізований
    ///     оригінал, інший точний запис, тобто інший регістр або пробіли). Регістр
    ///     перекладу підлаштовується під регістр оригіналу цілі (CaseAdapter), краї
    ///     (пробіли, переноси) беруться з оригіналу цілі. Рядки, регістр яких
    ///     не визначається, вичитані та технічні рядки пропускаються.
    /// EN: Copies the current row's translation into "similar" rows (same normalized
    ///     original, different exact text, i.e. other letter case or whitespace). The
    ///     translation's case is adapted to the target original's case (CaseAdapter),
    ///     edge whitespace follows the target original. Rows whose case cannot be
    ///     resolved, reviewed rows and technical rows are skipped.
    /// </summary>
    private void CopyTranslationToSimilar(TranslationEntry source)
    {
        if (!source.IsTranslated)
        {
            ShowStatus("ℹ UA: Рядок без перекладу / EN: The row has no translation");
            return;
        }

        string norm = DatTranslationTransfer.NormalizeText(source.Original);

        var plan = new List<(TranslationEntry Row, string NewText)>();
        int skippedMixed = 0, skippedReviewed = 0, similarTotal = 0;

        foreach (var t in _entries)
        {
            if (ReferenceEquals(t, source) || t.IsTechnical) continue;
            if (string.Equals(t.Original, source.Original, StringComparison.Ordinal)) continue;
            if (DatTranslationTransfer.NormalizeText(t.Original) != norm) continue;
            similarTotal++;

            if (t.WasReviewed) { skippedReviewed++; continue; }

            string? adapted = CaseAdapter.AdaptFor(source.Original, source.Translated, t.Original);
            if (adapted is null) { skippedMixed++; continue; }

            string result = ValidationService.AlignEdgeWhitespace(t.Original, adapted);
            if (string.Equals(result, t.Translated, StringComparison.Ordinal)) continue;

            plan.Add((t, result));
        }

        if (similarTotal == 0)
        {
            ShowStatus("ℹ UA: Схожих рядків немає / EN: No similar rows");
            return;
        }
        if (plan.Count == 0)
        {
            ShowStatus(
                "ℹ UA: Нема що змінювати (однакові, вичитані або регістр не визначається) / " +
                $"EN: Nothing to change (identical, reviewed or case not resolvable) · {skippedReviewed}/{skippedMixed}");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"UA: Буде змінено {plan.Count} рядків; пропущено: вичитаних {skippedReviewed}, регістр не визначається {skippedMixed}.");
        sb.AppendLine($"EN: {plan.Count} rows will change; skipped: reviewed {skippedReviewed}, case not resolvable {skippedMixed}.");
        sb.AppendLine();
        foreach (var (row, text) in plan.Take(5))
            sb.AppendLine($"{row.Key}:  «{row.Translated}» → «{text}»");
        if (plan.Count > 5) sb.AppendLine($"… +{plan.Count - 5}");
        sb.AppendLine();
        sb.Append("UA: Продовжити? / EN: Continue?");

        if (MessageBox.Show(sb.ToString(),
                "UA: Копіювати у схожі / EN: Copy to similar rows",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        foreach (var (row, text) in plan)
            row.Translated = text;

        RefreshView();
        ShowStatus($"≈ UA: Змінено {plan.Count} схожих рядків / EN: Changed {plan.Count} similar rows");
    }

    private void CtxCopyToSimilar_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextEntry() is { } entry)
            CopyTranslationToSimilar(entry);
    }

    /// <summary>
    /// UA: Копіює ключ запису у буфер обміну.
    /// EN: Copies entry key to clipboard.
    /// </summary>
    private void CtxCopyKey_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextEntry() is { } entry)
        {
            Clipboard.SetText(entry.Key);
            ShowStatus($"🔑 UA: Ключ скопійовано / EN: Key copied · «{entry.Key}»");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ВИЧИТКА / REVIEW
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// UA: Завершення редагування комірки (переклад або вичитка). Оновлення
    ///     лічильників і фільтрів відкладається, доки DataGrid не закриє
    ///     транзакцію редагування рядка.
    /// EN: End of a cell edit (translation or review). Counter and filter
    ///     updates are deferred until the DataGrid closes the row's edit
    ///     transaction.
    /// </summary>
    private void MainGrid_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            // UA: Порожню вичитку після редагування замінює «-»
            // EN: An emptied review field becomes "-" after editing
            EnsureReviewMarks();
            RefreshView();
            ReportRejectedText();
        }));
    }

    /// <summary>
    /// UA: Повідомляє в рядку стану, якщо після редагування текст відхилено перевіркою символів.
    /// EN: Reports in the status line when a text was rejected by the character check after editing.
    /// </summary>
    private void ReportRejectedText()
    {
        int now = TranslationEntry.RejectedCount;
        if (now == _rejectedReported) return;

        _rejectedReported = now;
        ShowStatus("⚠ " + TextGuard.RejectMessage, isError: true);
    }

    /// <summary>
    /// UA: Пункт «Вичитка для виділених» із готовою позначкою (+, +/-, -).
    ///     Значення позначки передає Tag пункту меню.
    /// EN: A "Review for selected" item with a ready-made mark (+, +/-, -).
    ///     The mark value comes from the menu item's Tag.
    /// </summary>
    private void CtxReviewSet_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item)
            ApplyReviewToSelection(item.Tag as string ?? "");
    }

    /// <summary>
    /// UA: Пункт «Вичитка для виділених → Власний текст…»: довільний коментар.
    ///     Якщо всі виділені рядки мають однакову позначку, вона підставляється
    ///     в поле як початкове значення.
    /// EN: The "Review for selected → Custom text…" item: a free-form comment.
    ///     If all selected rows share one mark, it is pre-filled in the field.
    /// </summary>
    private void CtxReviewCustom_Click(object sender, RoutedEventArgs e)
    {
        var rows = GetSelectedReviewable();
        if (rows.Count == 0)
        {
            ShowStatus("ℹ UA: Немає виділених рядків / EN: No rows selected");
            return;
        }

        var distinct = rows.Select(r => r.ReviewStatus).Distinct().ToList();
        string initial = distinct.Count == 1 ? distinct[0] : "";

        var dialog = new ReviewMarkPromptWindow(rows.Count, initial) { Owner = this };
        if (dialog.ShowDialog() == true)
            ApplyReviewToSelection(dialog.ReviewText);
    }

    /// <summary>
    /// UA: Виділені рядки, яким можна ставити вичитку. Технічні рядки пропускаються:
    ///     вони не перекладаються й не входять у лічильник вичитки.
    /// EN: Selected rows that can carry a review mark. Technical rows are skipped:
    ///     they are not translated and are not part of the review counter.
    /// </summary>
    private List<TranslationEntry> GetSelectedReviewable() =>
        MainGrid.SelectedItems.OfType<TranslationEntry>().Where(r => !r.IsTechnical).ToList();

    /// <summary>
    /// UA: Ставить позначку вичитки всім виділеним (не технічним) рядкам через той самий
    ///     сеттер ReviewStatus, що й редагування комірки. Лічильники й фільтри
    ///     оновлюються один раз на всю пачку.
    /// EN: Sets the review mark on all selected (non-technical) rows through the same
    ///     ReviewStatus setter that cell editing uses. Counters and filters are
    ///     refreshed once for the whole batch.
    /// </summary>
    private void ApplyReviewToSelection(string status)
    {
        var rows = GetSelectedReviewable();
        if (rows.Count == 0)
        {
            ShowStatus("ℹ UA: Немає виділених рядків / EN: No rows selected");
            return;
        }

        // UA: Порожній текст означає «не вичитано»
        // EN: Empty text means "not reviewed"
        if (string.IsNullOrWhiteSpace(status))
            status = TranslationEntry.NotReviewedMark;

        foreach (var row in rows)
            row.ReviewStatus = status;

        RefreshView();

        string label = status;
        ShowStatus(
            $"✓ UA: Позначку вичитки «{label}» встановлено для {rows.Count} рядків / " +
            $"EN: Review mark «{label}» set for {rows.Count} rows");
        SimpleLogger.Log($"Review mark '{label}' set for {rows.Count} rows");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ВІДОБРАЖЕННЯ ПАНЕЛЕЙ / PANEL VISIBILITY
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// UA: Перемикає з порожнього стану на DataGrid після завантаження файлу.
    ///     Visibility.Collapsed у Grid Auto-рядках = рівно 0px висоти.
    /// EN: Switches from empty state to DataGrid after file load.
    ///     Visibility.Collapsed in Grid Auto rows = exactly 0px height.
    /// </summary>
    private void ShowPanels()
    {
        EmptyState.Visibility = Visibility.Collapsed;
        MainGrid.Visibility = Visibility.Visible;
        SafeBadge.Visibility = Visibility.Visible;
        SafetyStrip.Visibility = Visibility.Visible;
        MergeBar.Visibility = Visibility.Visible;
        ToolbarPanel.Visibility = Visibility.Visible;
        LegendBar.Visibility = Visibility.Visible;
        SaveButton.IsEnabled = true;
        ExportButton.IsEnabled = true;
        HomeButton.Visibility = Visibility.Visible;
        OutCard.Opacity = 1.0;
    }

    /// <summary>
    /// UA: Зворотна дія до ShowPanels: показує порожній стан.
    /// EN: The reverse of ShowPanels: shows the empty state.
    /// </summary>
    private void HidePanels()
    {
        EmptyState.Visibility = Visibility.Visible;
        MainGrid.Visibility = Visibility.Collapsed;
        SafeBadge.Visibility = Visibility.Collapsed;
        SafetyStrip.Visibility = Visibility.Collapsed;
        MergeBar.Visibility = Visibility.Collapsed;
        ToolbarPanel.Visibility = Visibility.Collapsed;
        LegendBar.Visibility = Visibility.Collapsed;
        SaveButton.IsEnabled = false;
        ExportButton.IsEnabled = false;
        HomeButton.Visibility = Visibility.Collapsed;
        OutCard.Opacity = 0.65;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // КОНФІГ / CONFIG
    // ══════════════════════════════════════════════════════════════════════════

    // ══════════════════════════════════════════════════════════════════════════
    // ПЕРЕНЕСЕННЯ ПЕРЕКЛАДУ ОСНОВНА ГРА → ДОПОВНЕННЯ / BASE GAME → EXPANSION TRANSFER
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// UA: Відкриває вікно перенесення перекладу з основної гри (напр. Empire at War) у
    ///     доповнення (напр. Forces of Corruption). Кнопка доступна одразу після запуску:
    ///     вікно саме збирає чотири файли — оригінал і переклад основної гри (донор),
    ///     оригінал і переклад доповнення (цільовий файл) — або бере доповнення, відкрите в програмі.
    ///     Ще до перенесення вікно показує аналіз збігів за ключем і за англійським текстом.
    ///     Виконання — RunTransfer.
    /// EN: Opens the window for transferring a translation from the base game (e.g.
    ///     Empire at War) into the expansion (e.g. Forces of Corruption). The button is
    ///     available right after startup: the window collects four files — the base game's
    ///     original and translation (the donor), the expansion's original and translation
    ///     (the target) — or takes the expansion open in the application.
    ///     Before the transfer the window shows the match analysis by key and by English
    ///     text. Execution — RunTransfer.
    /// </summary>
    private void TransferButton_Click(object sender, RoutedEventArgs e)
    {
        MainGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

        var openRows = _entries.Count > 0
            ? _entries.Select(x => (x.Key, x.Original, x.IsTechnical)).ToList()
            : null;

        var window = new TransferWindow(
            openRows, _entries.Count > 0 ? _origFileName : null, _gameKind)
        {
            Owner = this
        };

        if (window.ShowDialog() == true && window.Index is not null)
            RunTransfer(window);
    }

    /// <summary>
    /// UA: Виконує перенесення за вибором із вікна.
    ///
    ///     Цільовий файл — доповнення, відкрите в програмі, або пара файлів із диска: тоді оригінал
    ///     доповнення завантажується як ① (за наявності — і його переклад як ②; без нього
    ///     підхоплюється робочий TSV цього оригіналу, якщо він є).
    ///
    ///     Захист — за позначкою вичитки: рядок із позначкою, відмінною від «-», не
    ///     перезаписується. Наявність перекладу критерієм не є: після пакетного ШІ-перекладу
    ///     переклад має майже кожен рядок. Технічні рядки не змінюються ніколи. Структура
    ///     DAT береться лише з файлу доповнення — донор постачає тільки текст.
    ///
    ///     Для англійського тексту з кількома різними перекладами відкривається вікно вибору.
    ///
    ///     Результат записується в НОВИЙ DAT із міткою часу в теці перекладу доповнення
    ///     (наявні файли не перезаписуються), поруч — парний TSV; попередня версія робочого
    ///     TSV лишається як .bak.
    ///
    /// EN: Runs the transfer according to the window's selection.
    ///
    ///     The target is the expansion open in the application, or a pair of files from
    ///     disk: then the expansion's original is loaded as ① (and its translation, if
    ///     given, as ②; without one the original's working TSV is picked up if it exists).
    ///
    ///     Protection is based on the review mark: a row whose mark is anything but "-" is
    ///     never overwritten. Having a translation is not the criterion: after a batch AI
    ///     translation almost every row already has one. Technical rows are never changed.
    ///     The DAT structure comes only from the expansion file — the donor supplies text only.
    ///
    ///     English text with several distinct translations opens a choice window.
    ///
    ///     The result is written to a NEW time-stamped DAT in the expansion's translation
    ///     folder (existing files are never overwritten), with a paired TSV beside it; the
    ///     previous version of the working TSV stays as .bak.
    /// </summary>
    private void RunTransfer(TransferWindow window)
    {
        try
        {
            // UA: Остання перевірка: файл основної гри не може бути ціллю перенесення.
            // EN: A final check: the base game's file cannot be a transfer target.
            if (window.UseOpenTarget && _gameKind == GameKind.Base)
            {
                MessageBox.Show(
                    "UA: Відкритий файл — основна гра, він не може бути ціллю перенесення.\n" +
                    "EN: The open file is the base game; it cannot be a transfer target.",
                    "UA: Перенесення / EN: Transfer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!window.UseOpenTarget)
            {
                if (_entries.Any(x => x.IsModified || x.WasReviewed))
                {
                    var answer = MessageBox.Show(
                        "UA: Відкритий файл має незбережені правки або статуси вичитки. " +
                        "Завантаження доповнення з диска замінить його. Продовжити?\n\n" +
                        "EN: The open file has unsaved edits or review statuses. " +
                        "Loading the expansion from disk will replace it. Continue?",
                        "UA: Перенесення / EN: Transfer",
                        MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (answer != MessageBoxResult.Yes) return;
                }

                if (!LoadOriginalFile(window.AddonOriginalPath!)) return;

                if (window.AddonTranslationPath is { } translationPath)
                    LoadTranslationFile(translationPath);
                else if (File.Exists(WorkingFilePath()))
                    LoadTranslationFile(WorkingFilePath());
            }

            var index = window.Index!;
            SimpleLogger.Log($"Transfer donor index: {index.KeyCount} translated keys, {index.TextCount} distinct texts");

            // UA: Вичитані рядки не змінюються — конфліктів для них не збираємо
            // EN: Reviewed rows are never changed — no conflicts are collected for them
            var candidates = _entries
                .Where(x => !x.IsTechnical && !x.WasReviewed)
                .Select(x => (x.Key, x.Original))
                .ToList();

            var conflicts = DatTranslationTransfer.CollectConflicts(index, candidates);

            IReadOnlyDictionary<string, string> resolutions = new Dictionary<string, string>();
            if (conflicts.Count > 0)
            {
                var originals = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var (_, original) in candidates)
                    originals.TryAdd(original, original);

                var conflictWindow = new TranslationConflictWindow(conflicts, originals) { Owner = this };
                if (conflictWindow.ShowDialog() != true)
                {
                    ShowStatus("ℹ UA: Перенесення скасовано / EN: Transfer cancelled");
                    return;
                }
                resolutions = conflictWindow.Resolutions;
            }

            // UA: Рядки, де ключ є в основній грі, але англійський текст інший, — на окреме підтвердження.
            // EN: Rows whose key exists in the base game but whose English text differs go to a separate review.
            var reviewItems = new List<TransferReviewWindow.Item>();
            foreach (var entry in _entries)
            {
                if (entry.IsTechnical || entry.WasReviewed) continue;

                var match = DatTranslationTransfer.Match(index, entry.Key, entry.Original);
                var info = DatTranslationTransfer.Inspect(index, entry.Key, entry.Original);
                if (!DatTranslationTransfer.NeedsReview(match, info)) continue;

                reviewItems.Add(new TransferReviewWindow.Item(
                    entry.Key, entry.Original, info.DonorOriginal ?? string.Empty, entry.Translated,
                    info.TranslationByKey, info.ByText, match.Kind == TransferMatchKind.Text));
            }

            IReadOnlyDictionary<string, string> reviewed = new Dictionary<string, string>();
            if (reviewItems.Count > 0)
            {
                var reviewWindow = new TransferReviewWindow(reviewItems) { Owner = this };
                if (reviewWindow.ShowDialog() != true)
                {
                    ShowStatus("ℹ UA: Перенесення скасовано / EN: Transfer cancelled");
                    return;
                }
                reviewed = reviewWindow.Results;
            }

            // UA: Стан до перенесення фіксується в робочому TSV, а після перенесення його
            //     попередня версія лишається як .bak.
            // EN: The pre-transfer state is persisted to the working TSV, and after the
            //     transfer its previous version stays as .bak.
            WriteWorkingTsv(force: true);

            var summary = ApplyTransfer(index, resolutions, reviewed);

            RefreshView();
            WriteWorkingTsv(force: true, backup: true);

            // UA: Результат завжди йде в НОВИЙ DAT із міткою часу в теці перекладу; жоден
            //     наявний DAT не перезаписується.
            // EN: The result always goes to a NEW time-stamped DAT in the translation folder;
            //     no existing DAT is overwritten.
            string newDat = NewDatPath();
            bool datSaved = WriteDatFile(newDat);

            ShowStatus(
                $"⇄ UA: Перенесено / EN: Transferred {summary.Changed} " +
                $"(UA: за ключем / EN: by key {summary.ByKey}, UA: за текстом / EN: by text {summary.ByText}, UA: з підтвердження / EN: from review {summary.FromReview}) " +
                $"· UA: вичитаних пропущено / EN: reviewed skipped {summary.ProtectedSkipped}");
            SimpleLogger.Log(
                $"Transfer done: keyMatched={summary.KeyMatched}, textMatched={summary.TextMatched}, " +
                $"conflictRows={summary.ConflictRows}, byKey={summary.ByKey}, byText={summary.ByText}, " +
                $"fromConflicts={summary.FromConflicts}, fromReview={summary.FromReview}, alreadyEqual={summary.AlreadyEqual}, " +
                $"protected={summary.ProtectedSkipped}, textChanged={summary.TextChanged}, " +
                $"unmatched={summary.Unmatched}");

            MessageBox.Show(
                "UA: Перенесення перекладу завершено.\n" +
                $"   Збіги: за ключем {summary.KeyMatched} · лише за англійським текстом {summary.TextMatched} · конфлікти {summary.ConflictRows}\n" +
                $"   Перенесено: {summary.Changed} (за ключем {summary.ByKey}, за англійським текстом {summary.ByText}, з конфліктів {summary.FromConflicts}, з підтвердження {summary.FromReview})\n" +
                $"   Без змін: уже однаково {summary.AlreadyEqual} · пропущено вичитаних {summary.ProtectedSkipped}\n" +
                $"   Ключ є в донорі, англійський текст інший: {summary.TextChanged}\n" +
                $"   Без відповідника (або конфлікт пропущено): {summary.Unmatched}\n" +
                (datSaved ? $"   Новий DAT: {newDat}\n\n" : "   DAT не збережено — збережіть вручну.\n\n") +
                "EN: Translation transfer finished.\n" +
                $"   Matches: by key {summary.KeyMatched} · by English text only {summary.TextMatched} · conflicts {summary.ConflictRows}\n" +
                $"   Transferred: {summary.Changed} (by key {summary.ByKey}, by English text {summary.ByText}, from conflicts {summary.FromConflicts}, from review {summary.FromReview})\n" +
                $"   Unchanged: already equal {summary.AlreadyEqual} · reviewed skipped {summary.ProtectedSkipped}\n" +
                $"   Key in the donor, English text differs: {summary.TextChanged}\n" +
                $"   No match (or conflict skipped): {summary.Unmatched}\n" +
                (datSaved ? $"   New DAT: {newDat}" : "   The DAT was not saved — save it manually."),
                "UA: Перенесення перекладу / EN: Translation transfer",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "RunTransfer");
            MessageBox.Show(
                $"UA: Помилка перенесення перекладу / EN: Translation transfer error:\n\n{ex.Message}",
                "Помилка / Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// UA: Записує підібрані переклади в рядки цілі й повертає підсумок.
    ///     Зміна йде через сеттер Translated, тож рядок отримує статус «Змінено»,
    ///     проходить валідацію й потрапляє в автозбереження.
    /// EN: Writes the matched translations into the target rows and returns the summary.
    ///     The change goes through the Translated setter, so the row becomes "Modified",
    ///     is validated and is picked up by autosave.
    /// </summary>
    private TransferSummary ApplyTransfer(
        DonorIndex index,
        IReadOnlyDictionary<string, string> resolutions,
        IReadOnlyDictionary<string, string> reviewed)
    {
        int byKey = 0, byText = 0, fromConflicts = 0, fromReview = 0;
        int alreadyEqual = 0, protectedSkipped = 0, textChanged = 0, unmatched = 0;
        int keyMatched = 0, textMatched = 0, conflictRows = 0;

        foreach (var entry in _entries)
        {
            if (entry.IsTechnical) continue;

            var match = DatTranslationTransfer.Match(index, entry.Key, entry.Original);

            // UA: Рядок із вікна підтвердження: записується обраний користувачем текст
            // EN: A row from the review window: the text chosen by the user is written
            if (!entry.WasReviewed && reviewed.TryGetValue(entry.Key, out var chosen))
            {
                if (match.Kind == TransferMatchKind.Text) textMatched++;
                else if (match.Kind == TransferMatchKind.TextChanged) textChanged++;

                if (string.Equals(entry.Translated, chosen, StringComparison.Ordinal)) alreadyEqual++;
                else { entry.Translated = chosen; fromReview++; }
                continue;
            }

            if (match.Kind == TransferMatchKind.None) { unmatched++; continue; }
            if (match.Kind == TransferMatchKind.TextChanged) { textChanged++; continue; }

            if (match.Kind == TransferMatchKind.Key) keyMatched++;
            else if (match.Kind == TransferMatchKind.Text) textMatched++;
            else conflictRows++;

            // UA: Вичитаний рядок захищений від перезапису
            // EN: A reviewed row is protected from being overwritten
            if (entry.WasReviewed) { protectedSkipped++; continue; }

            string? text = match.Translation;
            bool fromConflict = false;

            if (match.Kind == TransferMatchKind.Conflict)
            {
                if (!resolutions.TryGetValue(entry.Original, out text))
                {
                    unmatched++;
                    continue;
                }
                fromConflict = true;
            }

            if (string.IsNullOrEmpty(text)) { unmatched++; continue; }

            if (string.Equals(entry.Translated, text, StringComparison.Ordinal))
            {
                alreadyEqual++;
                continue;
            }

            entry.Translated = text;

            if (fromConflict) fromConflicts++;
            else if (match.Kind == TransferMatchKind.Key) byKey++;
            else byText++;
        }

        return new TransferSummary(
            byKey, byText, fromConflicts, alreadyEqual, protectedSkipped, textChanged, unmatched,
            keyMatched, textMatched, conflictRows, fromReview);
    }

    private void ConfigButton_Click(object sender, RoutedEventArgs e)
    {
        new ConfigWindow { Owner = this }.ShowDialog();
        // UA: Оновлюємо налаштування таймера після закриття вікна
        // EN: Update timer settings after closing the window
        ApplyAutoSaveSettings();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // СТАТУС / STATUS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// UA: Виводить повідомлення у рядку стану (зелене — успіх, червоне — помилка).
    /// EN: Shows message in status bar (green — success, red — error).
    /// </summary>
    private void ShowStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? Res("StatusRed") : Res("StatusGreen");
    }
}