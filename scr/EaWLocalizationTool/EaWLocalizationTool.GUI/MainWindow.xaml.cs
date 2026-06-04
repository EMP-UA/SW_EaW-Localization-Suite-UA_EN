using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
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
    private string _filterMode = "all";
    private string _searchText = "";

    // ── Допоміжний метод для кольорів / Resource brush helper ─────────────────
    private static Brush Res(string key) =>
        (Brush)Application.Current.Resources[key];

    // ─────────────────────────────────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();
        _view = CollectionViewSource.GetDefaultView(_entries);
        _view.Filter = FilterEntry;
        MainGrid.ItemsSource = _view;
        UpdateThemeButton();
        UpdateFontLabel();
        SetActiveFilter("all");
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
            Title = "① UA: Відкрити оригінальний DAT / EN: Open original DAT"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var (entries, rawBytes) = DatService.ParseOriginal(dlg.FileName);
            _origRaw = rawBytes;
            _origFileName = Path.GetFileName(dlg.FileName);

            // UA: Зберігаємо ручні правки якщо перезавантажуємо той самий файл
            // EN: Preserve manual edits if reloading the same file
            var existing = _entries
                .Where(x => x.IsModified)
                .ToDictionary(x => x.Key, x => x.Translated, StringComparer.Ordinal);

            _entries.Clear();
            foreach (var e in entries)
            {
                if (existing.TryGetValue(e.Key, out var saved))
                    e.SetTranslatedSilent(saved);
                _entries.Add(e);
            }

            int techCount = entries.Count(e => e.IsTechnical);

            OrigFileText.Text = "📄 " + _origFileName;
            OrigFileText.FontStyle = FontStyles.Normal;
            OrigFileText.Foreground = Res("TextPrim");
            OrigMetaText.Text = $"{entries.Count} UA: записів / EN: records" +
                (techCount > 0
                    ? $" · {techCount} UA: технічних / EN: technical"
                    : "");

            OutputFileText.Text = Path.GetFileNameWithoutExtension(dlg.FileName) + "_UA.dat";
            OutputFileText.FontStyle = FontStyles.Normal;

            ShowPanels();

            // UA: Скидаємо фільтр і пошук при новому файлі
            // EN: Reset filter and search for new file
            _filterMode = "all";
            _searchText = "";
            SearchBox.Text = "";
            SetActiveFilter("all");

            RefreshView();
            ShowStatus($"✓ {entries.Count} UA: записів / EN: records · «{_origFileName}»");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"UA: Помилка читання DAT / EN: DAT read error:\n\n{ex.Message}",
                "Помилка / Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
            Title = "② UA: Відкрити файл перекладу / EN: Open translation file"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            string ext = Path.GetExtension(dlg.FileName).ToLower();
            var map = ext == ".dat"
                ? DatService.ParseDatAsTranslation(dlg.FileName)
                : DatService.ParseTsv(dlg.FileName);

            int cnt = 0, skippedTech = 0;
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
            }

            TransFileText.Text = (ext == ".dat" ? "📦 " : "📋 ") +
                                       Path.GetFileName(dlg.FileName);
            TransFileText.FontStyle = FontStyles.Normal;
            TransFileText.Foreground = Res("TextPrim");
            TransMetaText.Text = $"{cnt} UA: перекладів / EN: translations" +
                (skippedTech > 0
                    ? $" · {skippedTech} UA: технічних пропущено / EN: technical skipped"
                    : "");

            _view.Refresh();
            RefreshView();
            ShowStatus(
                $"✓ {cnt} UA: перекладів зіставлено / EN: matched " +
                $"· «{Path.GetFileName(dlg.FileName)}»");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"UA: Помилка читання перекладу / EN: Translation read error:\n\n{ex.Message}",
                "Помилка / Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ЗБЕРЕЖЕННЯ / SAVING
    // ══════════════════════════════════════════════════════════════════════════

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_origRaw.Length == 0 || _entries.Count == 0) return;

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
            if (warn != MessageBoxResult.Yes) return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "DAT files (*.dat)|*.dat",
            FileName = Path.GetFileNameWithoutExtension(_origFileName) + "_UA.dat",
            Title = "③ UA: Зберегти новий DAT / EN: Save new DAT"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            DatService.WriteSafe(dlg.FileName, _origRaw, _entries.ToList());
            OutputFileText.Text = Path.GetFileName(dlg.FileName);
            OutputFileText.FontStyle = FontStyles.Normal;
            ShowStatus(
                $"✓ UA: Збережено / EN: Saved «{Path.GetFileName(dlg.FileName)}» " +
                "· UA: структура збережена побайтово / EN: byte-perfect");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"UA: Помилка збереження / EN: Save error:\n\n{ex.Message}",
                "Помилка / Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_entries.Count == 0) return;
        var dlg = new SaveFileDialog
        {
            Filter = "TSV files (*.tsv)|*.tsv",
            FileName = Path.GetFileNameWithoutExtension(_origFileName) + "_review.tsv",
            Title = "UA: Експортувати TSV / EN: Export TSV"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            DatService.ExportTsv(dlg.FileName, _entries);
            ShowStatus(
                $"✓ UA: Експортовано / EN: Exported {_entries.Count} " +
                $"UA: рядків / EN: rows · «{Path.GetFileName(dlg.FileName)}»");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"UA: Помилка / EN: Error:\n\n{ex.Message}",
                "Помилка / Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            "tech" => e.IsTechnical,
            _ => true
        };
        if (!passes) return false;
        if (string.IsNullOrEmpty(_searchText)) return true;

        // UA: Пошук по всіх трьох полях без урахування регістру
        // EN: Case-insensitive search across all three fields
        string q = _searchText.ToLower();
        return e.Key.ToLower().Contains(q)
            || e.Original.ToLower().Contains(q)
            || e.Translated.ToLower().Contains(q);
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

    private void RefreshView()
    {
        _view.Refresh();
        UpdateStats();
        int visible = _entries.Count(FilterEntry);
        FilterCountText.Text = $"{visible} UA: з / EN: of {_entries.Count}";
    }

    private void UpdateStats()
    {
        int total = _entries.Count;
        int tech = _entries.Count(e => e.IsTechnical);
        int normal = total - tech;
        int done = _entries.Count(e => e.IsTranslated && !e.IsTechnical);
        int mod = _entries.Count(e => e.IsModified);
        int issues = _entries.Count(e => e.HasValidationIssue);
        int un = normal - done;
        int pct = normal > 0 ? (int)(done * 100.0 / normal) : 0;

        HeaderStatText.Text = total > 0
            ? $"{done}/{normal} · {pct}% · {mod} UA: правок / EN: edits · {issues} ⚠"
            : "";

        if (total > 0)
        {
            ProgressBar.Value = pct;
            MergeStatText.Text = $"{done}/{normal} ({pct}%)";
            MergeWarnText.Text = un > 0
                ? $"⚠ {un} UA: без перекладу / EN: untranslated"
                : "✔ UA: Всі перекладено / EN: All translated";
            MergeWarnText.Foreground = un > 0 ? Res("StatusAmber") : Res("StatusGreen");
        }

        FilterAll.Content = $"UA: Всі / EN: All · {total}";
        FilterUn.Content = $"UA: Без / EN: Untranslated · {un}";
        FilterTr.Content = $"UA: Перекл. / EN: Translated · {done}";
        FilterMod.Content = $"UA: Змінено / EN: Modified · {mod}";
        FilterIssues.Content = $"⚠ UA: Проблемні / EN: Issues · {issues}";
        FilterTech.Content = $"⚙ UA: Технічні / EN: Technical · {tech}";
    }

    private void SetActiveFilter(string active)
    {
        var buttons = new[] { FilterAll, FilterUn, FilterTr, FilterMod, FilterIssues, FilterTech };
        var tags = new[] { "all", "un", "tr", "mod", "issues", "tech" };

        for (int i = 0; i < buttons.Length; i++)
        {
            bool on = tags[i] == active;
            buttons[i].Background = on ? Res("AccentDark") : Res("BgCard");
            buttons[i].BorderBrush = on ? Res("Accent") : Res("BdAcc");
            buttons[i].FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ТЕХНІЧНІ РЯДКИ / TECHNICAL ENTRIES
    // ══════════════════════════════════════════════════════════════════════════

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
    /// UA: Виділяє рядок при натисканні ПКМ — щоб SelectedItem оновився
    ///     ДО відкриття контекстного меню. Без цього меню відкривається,
    ///     але SelectedItem може вказувати на попередній рядок.
    /// EN: Selects row on RMB press — so SelectedItem updates
    ///     BEFORE the context menu opens. Without this the menu opens,
    ///     but SelectedItem may point to the previous row.
    /// </summary>
    private void Row_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row)
        {
            row.IsSelected = true;
            MainGrid.Focus();
        }
    }

    /// <summary>
    /// UA: Копіює оригінальний текст поточного рядка у буфер обміну.
    /// EN: Copies original text of current row to clipboard.
    /// </summary>
    private void CtxCopyOriginal_Click(object sender, RoutedEventArgs e)
    {
        if (MainGrid.SelectedItem is TranslationEntry entry &&
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
        if (MainGrid.SelectedItem is not TranslationEntry entry) return;

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
        if (MainGrid.SelectedItem is TranslationEntry entry)
        {
            entry.ClearTranslation();
            RefreshView();
            ShowStatus(
                $"🗑 UA: Переклад очищено / EN: Translation cleared · «{entry.Key}»");
        }
    }

    /// <summary>
    /// UA: Копіює ключ запису у буфер обміну.
    /// EN: Copies entry key to clipboard.
    /// </summary>
    private void CtxCopyKey_Click(object sender, RoutedEventArgs e)
    {
        if (MainGrid.SelectedItem is TranslationEntry entry)
        {
            Clipboard.SetText(entry.Key);
            ShowStatus($"🔑 UA: Ключ скопійовано / EN: Key copied · «{entry.Key}»");
        }
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
    }

    // ══════════════════════════════════════════════════════════════════════════
    // КОНФІГ / CONFIG
    // ══════════════════════════════════════════════════════════════════════════

    private void ConfigButton_Click(object sender, RoutedEventArgs e) =>
        new ConfigWindow { Owner = this }.ShowDialog();

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