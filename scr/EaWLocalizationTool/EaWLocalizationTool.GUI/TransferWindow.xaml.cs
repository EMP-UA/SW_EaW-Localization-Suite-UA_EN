// =============================================================================
// EaWLocalizationTool.GUI — TransferWindow.xaml.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Вікно вибору файлів для перенесення перекладу з основної гри в доповнення.
//     Донор — оригінал і переклад основної гри (DAT або TSV): з них у пам'яті
//     будується індекс «ключ → англійський текст → переклад». Цільовий файл — доповнення:
//     відкритий у програмі файл або пара оригінал + переклад із диска. Ще до
//     виконання перенесення вікно показує аналіз збігів (DatTranslationTransfer.Analyze).
//
//     Саме перенесення виконує MainWindow: вікно лише повертає індекс донора й
//     вибір цілі. Індекс і шляхи зберігаються в статичних полях, тож повторне
//     відкриття вікна не перечитує файли, якщо вони не змінилися.
// EN: File picker for transferring a translation from the base game into an
//     expansion. The donor is the base game's original and translation (DAT or
//     TSV): an in-memory "key → English text → translation" index is built from
//     them. The target is the expansion: the file open in the application or an
//     original + translation pair from disk. Before the transfer runs, the window
//     shows the match analysis (DatTranslationTransfer.Analyze).
//
//     MainWindow performs the transfer itself: the window only returns the donor
//     index and the target choice. The index and paths are kept in static fields,
//     so reopening the window does not reread files that have not changed.
// =============================================================================

using System.IO;
using System.Windows;
using Microsoft.Win32;
using EaWLocalizationTool.Core;
using EaWLocalizationTool.GUI.Services;

namespace EaWLocalizationTool.GUI;

public partial class TransferWindow : Window
{
    private sealed record DonorCache(string Signature, DonorIndex Index);

    // UA: Пам'ять між відкриттями вікна (на час роботи програми).
    // EN: Memory between window openings (for the lifetime of the application).
    private static DonorCache? _donorCache;
    private static string? _lastDonorOriginal, _lastDonorTranslation;
    private static string? _lastAddonOriginal, _lastAddonTranslation;

    private readonly IReadOnlyList<(string Key, string Original, bool Technical)>? _openRows;

    private string? _donorOriginalPath, _donorTranslationPath;
    private string? _addonOriginalPath, _addonTranslationPath;
    private List<(string Key, string Original, bool Technical)>? _fileRows;
    private IReadOnlyList<(string Key, string Original, bool Technical)>? _currentRows;

    /// <summary>UA: Індекс донора (після успішного вибору). / EN: The donor index (after a successful pick).</summary>
    public DonorIndex? Index { get; private set; }

    /// <summary>UA: Результат аналізу для поточного вибору. / EN: Analysis for the current selection.</summary>
    public TransferAnalysis? Analysis { get; private set; }

    /// <summary>UA: True, якщо цільовий файл — відкритий, відкритий у програмі. / EN: True when the target is the file open in the application.</summary>
    public bool UseOpenTarget => _openRows is not null && UseOpenCheck.IsChecked == true;

    /// <summary>UA: Оригінал доповнення з диска (коли цільовий файл — не відкритий). / EN: Expansion original from disk (when the target is not the open file).</summary>
    public string? AddonOriginalPath => UseOpenTarget ? null : _addonOriginalPath;

    /// <summary>UA: Переклад доповнення з диска (необов'язково). / EN: Expansion translation from disk (optional).</summary>
    public string? AddonTranslationPath => UseOpenTarget ? null : _addonTranslationPath;

    /// <summary>
    /// UA: openRows — рядки файлу, відкритого в програмі (null, якщо нічого не відкрито);
    ///     openName — його ім'я для підпису прапорця.
    /// EN: openRows — the rows of the file open in the application (null when nothing is open);
    ///     openName — its name for the checkbox label.
    /// </summary>
    public TransferWindow(
        IReadOnlyList<(string Key, string Original, bool Technical)>? openRows,
        string? openName,
        GameKind openGame = GameKind.Other)
    {
        InitializeComponent();
        _openRows = openRows;

        if (_openRows is not null)
        {
            UseOpenCheck.Visibility = Visibility.Visible;
            UseOpenCheck.Content =
                $"UA: Цільовий файл — відкритий у програмі: «{openName}» ({_openRows.Count} записів) / " +
                $"EN: Target — the file open in the application: \"{openName}\" ({_openRows.Count} entries)";

            // UA: Відкритий файл стає ціллю за замовчуванням лише коли це доповнення.
            //     Файл основної гри не може бути ціллю перенесення з основної гри;
            //     файл невизначеної гри користувач вмикає свідомо.
            // EN: The open file is the target by default only when it is the expansion.
            //     The base game's file cannot be the target of a transfer from the base
            //     game; a file of an undetermined game is enabled deliberately.
            switch (openGame)
            {
                case GameKind.Corruption:
                    UseOpenCheck.IsChecked = true;
                    break;
                case GameKind.Base:
                    UseOpenCheck.IsChecked = false;
                    UseOpenCheck.IsEnabled = false;
                    UseOpenCheck.Content +=
                        "\nUA: Це файл основної гри — він не може бути ціллю. / " +
                        "EN: This is the base game's file — it cannot be the target.";
                    break;
                default:
                    UseOpenCheck.IsChecked = false;
                    break;
            }
        }

        // UA: Останні шляхи; якщо їх немає — найновіші файли зі стандартних тек програми.
        // EN: The last paths; when there are none — the newest files in the application's standard folders.
        _donorOriginalPath = Preferred(_lastDonorOriginal, AppFolders.Original(GameKind.Base));
        _donorTranslationPath = Preferred(_lastDonorTranslation, AppFolders.Translated(GameKind.Base));
        _addonOriginalPath = Preferred(_lastAddonOriginal, AppFolders.Original(GameKind.Corruption));
        _addonTranslationPath = Preferred(_lastAddonTranslation, AppFolders.Translated(GameKind.Corruption));

        ShowPath(DonorOriginalText, _donorOriginalPath);
        ShowPath(DonorTranslationText, _donorTranslationPath);
        ShowPath(AddonOriginalText, _addonOriginalPath);
        ShowPath(AddonTranslationText, _addonTranslationPath);

        if (_addonOriginalPath is not null) LoadAddonOriginal();
        BuildDonor();
        ApplyTargetMode();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ВИБІР ФАЙЛІВ / FILE PICKING
    // ══════════════════════════════════════════════════════════════════════════

    private const string DatFilter = "DAT files (*.dat)|*.dat|All files (*.*)|*.*";
    private const string TranslationFilter =
        "UA: Переклад / EN: Translation|*.dat;*.tsv;*.txt|DAT (*.dat)|*.dat|TSV (*.tsv;*.txt)|*.tsv;*.txt";

    private void BrowseDonorOriginal_Click(object sender, RoutedEventArgs e)
    {
        var path = Pick(DatFilter,
            "UA: Основна гра — оригінальний (англійський) DAT / EN: Base game — original (English) DAT",
            AppFolders.Original(GameKind.Base));
        if (path is null) return;

        _donorOriginalPath = path;
        ShowPath(DonorOriginalText, path);
        BuildDonor();
    }

    private void BrowseDonorTranslation_Click(object sender, RoutedEventArgs e)
    {
        var path = Pick(TranslationFilter,
            "UA: Основна гра — перекладений DAT або TSV / EN: Base game — translated DAT or TSV",
            AppFolders.Translated(GameKind.Base));
        if (path is null) return;

        _donorTranslationPath = path;
        ShowPath(DonorTranslationText, path);
        BuildDonor();
    }

    private void BrowseAddonOriginal_Click(object sender, RoutedEventArgs e)
    {
        var path = Pick(DatFilter,
            "UA: Доповнення — оригінальний (англійський) DAT / EN: Expansion — original (English) DAT",
            AppFolders.Original(GameKind.Corruption));
        if (path is null) return;

        _addonOriginalPath = path;
        ShowPath(AddonOriginalText, path);
        LoadAddonOriginal();
        Recompute();
    }

    private void BrowseAddonTranslation_Click(object sender, RoutedEventArgs e)
    {
        var path = Pick(TranslationFilter,
            "UA: Доповнення — поточний переклад (DAT або TSV) / EN: Expansion — current translation (DAT or TSV)",
            AppFolders.Translated(GameKind.Corruption));
        if (path is null) return;

        _addonTranslationPath = path;
        ShowPath(AddonTranslationText, path);
    }

    private void UseOpenCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded && AddonOriginalCard is null) return;
        ApplyTargetMode();
    }

    private string? Pick(string filter, string title, string? initialDir = null)
    {
        var dlg = new OpenFileDialog { Filter = filter, Title = title };
        if (initialDir is not null)
        {
            try
            {
                Directory.CreateDirectory(initialDir);
                dlg.InitialDirectory = initialDir;
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, "TransferWindow.Pick");
            }
        }
        return dlg.ShowDialog(this) == true ? dlg.FileName : null;
    }

    /// <summary>
    /// UA: Файл для слота: останній вибраний шлях, якщо в стандартній теці немає
    ///     новішого DAT; інакше — найновіший DAT теки (щойно збережені файли
    ///     підхоплюються без ручного вибору).
    /// EN: The file for a slot: the last picked path, unless the standard folder holds
    ///     a newer DAT; otherwise the folder's newest DAT (freshly saved files are
    ///     picked up without manual selection).
    /// </summary>
    private static string? Preferred(string? last, string folder)
    {
        string? existing = ExistingOrNull(last);
        string? latest = AppFolders.LatestFile(folder, "*.dat");
        if (existing is null) return latest;
        if (latest is null) return existing;

        return File.GetLastWriteTimeUtc(latest) > File.GetLastWriteTimeUtc(existing)
            ? latest
            : existing;
    }

    private static string? ExistingOrNull(string? path) =>
        !string.IsNullOrEmpty(path) && File.Exists(path) ? path : null;

    private static void ShowPath(System.Windows.Controls.TextBlock block, string? path)
    {
        if (path is null)
        {
            block.Text = "UA: не вибрано / EN: not selected";
            block.FontStyle = FontStyles.Italic;
            block.ClearValue(System.Windows.Controls.TextBlock.ForegroundProperty);
            block.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextDim");
            return;
        }

        block.Text = path;
        block.FontStyle = FontStyles.Normal;
        block.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextPrim");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ДОНОР / DONOR
    // ══════════════════════════════════════════════════════════════════════════

    private static string Signature(string? path)
    {
        if (path is null) return "-";
        var info = new FileInfo(path);
        return $"{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
    }

    /// <summary>
    /// UA: Будує індекс донора з вибраних файлів. Переклад у DAT вимагає окремого
    ///     оригіналу; TSV з колонкою OriginalText обходиться одним файлом; TSV без
    ///     колонки оригіналу вимагає оригінального DAT.
    /// EN: Builds the donor index from the selected files. A translation in a DAT
    ///     needs a separate original; a TSV with an OriginalText column needs just
    ///     one file; a TSV without an original column needs the original DAT.
    /// </summary>
    private void BuildDonor()
    {
        Index = null;
        DonorOriginalMeta.Text = "";
        DonorTranslationMeta.Text = "";
        DonorTranslationMeta.SetResourceReference(
            System.Windows.Controls.TextBlock.ForegroundProperty, "TextDim");

        if (_donorTranslationPath is null)
        {
            DonorTranslationMeta.Text =
                "UA: Оберіть переклад основної гри / EN: Pick the base game's translation";
            Recompute();
            return;
        }

        try
        {
            string signature = Signature(_donorOriginalPath) + "||" + Signature(_donorTranslationPath);

            if (_donorCache is { } cache && cache.Signature == signature)
            {
                Index = cache.Index;
            }
            else
            {
                var pairs = ReadDonorPairs();
                if (pairs is null)
                {
                    DonorTranslationMeta.Text =
                        "UA: Для цього файлу потрібен ще й оригінальний DAT основної гри (①) / " +
                        "EN: This file also needs the base game's original DAT (①)";
                    DonorTranslationMeta.SetResourceReference(
                        System.Windows.Controls.TextBlock.ForegroundProperty, "StatusAmber");
                    Recompute();
                    return;
                }

                Index = DatTranslationTransfer.BuildIndex(pairs);
                _donorCache = new DonorCache(signature, Index);
            }

            if (Index.KeyCount == 0)
            {
                DonorTranslationMeta.Text =
                    "UA: Перекладених рядків не знайдено: перевірте, що ① — англійський DAT, а ② — переклад / " +
                    "EN: No translated rows found: check that ① is the English DAT and ② is the translation";
                DonorTranslationMeta.SetResourceReference(
                    System.Windows.Controls.TextBlock.ForegroundProperty, "StatusAmber");
                Index = null;
            }
            else
            {
                DonorTranslationMeta.Text =
                    $"{Index.KeyCount:N0} UA: ключів із перекладом / EN: keys with a translation" +
                    $" · {Index.TextCount:N0} UA: різних англ. текстів / EN: distinct English texts";
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "TransferWindow.BuildDonor");
            Index = null;
            DonorTranslationMeta.Text = $"UA: Помилка читання / EN: Read error: {ex.Message}";
            DonorTranslationMeta.SetResourceReference(
                System.Windows.Controls.TextBlock.ForegroundProperty, "StatusRed");
        }

        Recompute();
    }

    /// <summary>
    /// UA: Збирає пари «ключ / оригінал / переклад» донора; null — потрібен оригінальний DAT.
    /// EN: Collects the donor's "key / original / translation" pairs; null — the original DAT is needed.
    /// </summary>
    private List<(string Key, string? Original, string Translation)>? ReadDonorPairs()
    {
        bool translationIsDat = Path.GetExtension(_donorTranslationPath!)
            .Equals(".dat", StringComparison.OrdinalIgnoreCase);

        if (translationIsDat)
        {
            if (_donorOriginalPath is null) return null;

            var translations = DatService.ParseDatAsTranslation(_donorTranslationPath!);
            var originals = DatService.ReadDonorOriginals(_donorOriginalPath);
            DonorOriginalMeta.Text = $"{originals.Count:N0} UA: записів / EN: entries";
            return DatTranslationTransfer.PairWithOriginals(originals, translations);
        }

        var pairs = DatService.ParseTsvPairs(_donorTranslationPath!);
        if (pairs.Count > 0 && pairs.All(p => p.Original is null))
        {
            if (_donorOriginalPath is null) return null;

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var p in pairs) map.TryAdd(p.Key, p.Translation);

            var originals = DatService.ReadDonorOriginals(_donorOriginalPath);
            DonorOriginalMeta.Text = $"{originals.Count:N0} UA: записів / EN: entries";
            return DatTranslationTransfer.PairWithOriginals(originals, map);
        }

        return pairs;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ЦІЛЬ / TARGET
    // ══════════════════════════════════════════════════════════════════════════

    private void ApplyTargetMode()
    {
        bool fromFiles = !UseOpenTarget;
        AddonOriginalCard.IsEnabled = fromFiles;
        AddonTranslationCard.IsEnabled = fromFiles;
        AddonOriginalCard.Opacity = fromFiles ? 1.0 : 0.5;
        AddonTranslationCard.Opacity = fromFiles ? 1.0 : 0.5;
        Recompute();
    }

    private void LoadAddonOriginal()
    {
        _fileRows = null;
        AddonOriginalMeta.Text = "";
        AddonOriginalMeta.SetResourceReference(
            System.Windows.Controls.TextBlock.ForegroundProperty, "TextDim");

        try
        {
            var (entries, _) = DatService.ParseOriginal(_addonOriginalPath!);
            _fileRows = entries.Select(x => (x.Key, x.Original, x.IsTechnical)).ToList();

            int tech = _fileRows.Count(r => r.Technical);
            AddonOriginalMeta.Text =
                $"{_fileRows.Count:N0} UA: записів / EN: entries" +
                (tech > 0 ? $" · {tech:N0} UA: технічних / EN: technical" : "");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "TransferWindow.LoadAddonOriginal");
            AddonOriginalMeta.Text = $"UA: Помилка читання / EN: Read error: {ex.Message}";
            AddonOriginalMeta.SetResourceReference(
                System.Windows.Controls.TextBlock.ForegroundProperty, "StatusRed");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // АНАЛІЗ / ANALYSIS
    // ══════════════════════════════════════════════════════════════════════════

    private void Recompute()
    {
        // UA: Під час InitializeComponent події прапорця можуть прийти до створення решти елементів
        // EN: During InitializeComponent the checkbox events can arrive before the other elements exist
        if (TransferRunButton is null || StatsGrid is null) return;

        IReadOnlyList<(string Key, string Original, bool Technical)>? rows =
            UseOpenTarget ? _openRows : _fileRows;

        Analysis = null;
        _currentRows = null;
        ReportButton.IsEnabled = false;
        HintText.Text = "";
        ReadyText.Text = "";

        if (Index is null || rows is null)
        {
            ClearStats();
            TransferRunButton.IsEnabled = false;
            if (Index is not null && rows is null)
                HintText.Text = "UA: Оберіть оригінальний DAT доповнення (③) / " +
                                "EN: Pick the expansion's original DAT (③)";
            return;
        }

        var a = DatTranslationTransfer.Analyze(Index, rows);
        Analysis = a;
        _currentRows = rows;
        ReportButton.IsEnabled = true;

        // UA: Запобіжник від перенесення файлу в самого себе: якщо майже всі ключі цілі
        //     є в донорі, це та сама гра, а не доповнення (в доповнення входять нові ключі).
        // EN: A guard against transferring a file into itself: when almost all of the
        //     target's keys are in the donor, it is the same game, not the expansion
        //     (an expansion brings new keys).
        bool sameGame = a.TargetRows > 0 && a.ByKey >= a.TargetRows * 0.95;

        StatDonorKeys.Text = a.DonorKeys.ToString("N0");
        StatDonorTexts.Text = a.DonorTexts.ToString("N0");
        StatTargetRows.Text = a.TargetRows.ToString("N0");
        StatByKey.Text = a.ByKey.ToString("N0");
        StatByText.Text = a.ByText.ToString("N0");
        StatConflicts.Text = a.Conflicts.ToString("N0");
        StatTextChanged.Text = a.TextChanged.ToString("N0");
        StatNoMatch.Text = a.NoMatch.ToString("N0");
        StatDonorOnly.Text = a.DonorOnlyKeys.ToString("N0");
        StatKeyTextDiffer.Text = a.KeyTextDiffer.ToString("N0");

        var rejectedVisibility = a.DonorRejected > 0 ? Visibility.Visible : Visibility.Collapsed;
        StatRejectedLabel.Visibility = rejectedVisibility;
        StatRejected.Visibility = rejectedVisibility;
        StatRejected.Text = a.DonorRejected.ToString("N0");

        TransferRunButton.IsEnabled = a.Transferable > 0 && !sameGame;

        if (sameGame)
        {
            HintText.Text =
                "UA: Майже всі ключі цільового файлу збігаються з основною грою — схоже, це та сама гра, а не доповнення. Перенесення заблоковано. / " +
                "EN: Almost all of the target's keys match the base game — this looks like the same game, not the expansion. The transfer is blocked.";
        }
        else if (a.Transferable == 0)
        {
            HintText.Text =
                "UA: Збігів немає: перевірте, що перші два файли — оригінал і переклад основної гри, а цільовий — доповнення. / " +
                "EN: No matches: check that the first two files are the base game's original and translation, and the target is the expansion.";
        }
        else
        {
            ReadyText.Text =
                $"UA: Буде розглянуто {a.Transferable:N0} рядків / EN: {a.Transferable:N0} rows will be considered";
        }
    }

    private void ClearStats()
    {
        foreach (var block in new[]
                 {
                     StatDonorKeys, StatDonorTexts, StatTargetRows, StatByKey, StatByText,
                     StatConflicts, StatTextChanged, StatNoMatch, StatDonorOnly, StatKeyTextDiffer
                 })
            block.Text = "—";

        StatRejectedLabel.Visibility = Visibility.Collapsed;
        StatRejected.Visibility = Visibility.Collapsed;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ЗАВЕРШЕННЯ / COMPLETION
    // ══════════════════════════════════════════════════════════════════════════

    private void TransferRunButton_Click(object sender, RoutedEventArgs e)
    {
        if (Index is null) return;

        _lastDonorOriginal = _donorOriginalPath;
        _lastDonorTranslation = _donorTranslationPath;
        _lastAddonOriginal = _addonOriginalPath;
        _lastAddonTranslation = _addonTranslationPath;

        DialogResult = true;
    }

    /// <summary>
    /// UA: Будує звіт зіставлення, зберігає його в transfer\transfer_report {мітка}.tsv
    ///     (для кожного нетехнічного рядка доповнення: вид збігу, переклад за ключем,
    ///     варіанти за англійським текстом) і відкриває його в програмі.
    /// EN: Builds the matching report, saves it to transfer\transfer_report {stamp}.tsv
    ///     (for every non-technical expansion row: the match kind, the translation by
    ///     key, the variants by English text) and opens it in the application.
    /// </summary>
    private void ReportButton_Click(object sender, RoutedEventArgs e)
    {
        if (Index is null || _currentRows is null) return;

        try
        {
            Directory.CreateDirectory(AppFolders.TransferRoot);
            string path = Path.Combine(AppFolders.TransferRoot,
                $"transfer_report {AppFolders.Stamp()}.tsv");

            var rows = DatTranslationTransfer.BuildReport(Index, _currentRows);
            DatTranslationTransfer.WriteReportTsv(path, rows);
            ReadyText.Text =
                $"UA: Звіт збережено: {rows.Count:N0} рядків / EN: Report saved: {rows.Count:N0} rows";

            new TransferReportWindow(rows, path) { Owner = this }.ShowDialog();
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "TransferWindow.ReportButton_Click");
            MessageBox.Show(
                $"UA: Помилка запису / EN: Write error:\n\n{ex.Message}",
                "Помилка / Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
