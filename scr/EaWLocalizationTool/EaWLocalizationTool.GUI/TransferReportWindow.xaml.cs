// =============================================================================
// EaWLocalizationTool.GUI — TransferReportWindow.xaml.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Перегляд звіту збігів перенесення прямо в програмі: таблиця всіх нетехнічних
//     рядків доповнення з видом збігу (за ключем / лише за текстом / конфлікт /
//     англійський текст інший / без відповідника), прапорцем розбіжності, перекладом
//     за ключем і варіантами за англійським текстом. Фільтри за видом збігу з
//     лічильниками та пошук за ключем, англійським текстом і перекладом.
//     Вікно лише читає дані; звіт TSV записує TransferWindow.
// EN: In-application viewer of the transfer match report: a table of all
//     non-technical expansion rows with the match kind (by key / text only /
//     conflict / English text changed / no match), the discrepancy flag, the
//     translation by key and the variants by English text. Filters by match
//     kind with counters, and a search over the key, English text and
//     translation. The window only reads the data; the TSV report is written
//     by TransferWindow.
// =============================================================================

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using EaWLocalizationTool.Core;
using EaWLocalizationTool.GUI.Services;

namespace EaWLocalizationTool.GUI;

public partial class TransferReportWindow : Window
{
    /// <summary>
    /// UA: Рядок таблиці (лише для читання).
    /// EN: A grid row (read-only).
    /// </summary>
    private sealed record ReportViewRow(
        string Key,
        TransferMatchKind Kind,
        bool Differ,
        string TargetOriginal,
        string DonorOriginal,
        string ByKey,
        string ByText,
        bool Minor)
    {
        public string KindLabel => Kind switch
        {
            TransferMatchKind.Key when Minor =>
                "UA: збіг за ключем, дрібна відмінність тексту / EN: matched by key, minor text difference",
            TransferMatchKind.Key => "UA: збіг за ключем / EN: matched by key",
            TransferMatchKind.Text when NeedsReview =>
                "UA: ключ є, текст змінено, фраза знайдена в основній грі / EN: key found, text changed, phrase found in base game",
            TransferMatchKind.Text => "UA: новий ключ, фраза є в основній грі / EN: new key, phrase exists in base game",
            TransferMatchKind.Conflict => "UA: новий ключ, кілька різних перекладів / EN: new key, several translations",
            TransferMatchKind.TextChanged => "UA: ключ є, англійський текст змінено / EN: key found, English text changed",
            _ => "UA: лише в доповненні, відповідника немає / EN: expansion only, no match"
        };

        public string DifferLabel => Differ ? "UA: так / EN: yes" : "";

        // UA: Рядок піде на підтвердження перед перенесенням (ключ є в основній грі, текст за ним інший).
        // EN: The row goes to the review before the transfer (the key exists in the base game, its text differs).
        public bool NeedsReview =>
            Kind == TransferMatchKind.TextChanged ||
            (Kind == TransferMatchKind.Text && DonorOriginal.Length > 0);
    }

    // UA: Фільтр: тег, підпис, предикат. / EN: A filter: tag, caption, predicate.
    private sealed record FilterDef(string Tag, string Caption, string Hint, Func<ReportViewRow, bool> Test);

    private readonly List<ReportViewRow> _all;
    private readonly List<FilterDef> _filters;
    private readonly Dictionary<string, Button> _buttons = new();
    private readonly ICollectionView _view;
    private readonly string _filePath;
    private string _active = "all";
    private string _search = string.Empty;

    /// <summary>
    /// UA: rows — рядки звіту; filePath — шлях збереженого TSV (для кнопки «Відкрити теку»).
    /// EN: rows — the report rows; filePath — path of the saved TSV (for the "Open folder" button).
    /// </summary>
    public TransferReportWindow(IReadOnlyList<TransferReportRow> rows, string filePath)
    {
        InitializeComponent();
        _filePath = filePath;

        _all = rows.Select(r => new ReportViewRow(
                r.Key,
                r.Kind,
                r.KeyTextDiffer,
                r.TargetOriginal,
                r.DonorOriginalByKey ?? string.Empty,
                r.TranslationByKey ?? string.Empty,
                string.Join(" ‖ ", r.TranslationsByText),
                r.MinorDifference))
            .ToList();

        _filters =
        [
            new("all", "UA: Усі рядки / EN: All rows",
                "UA: Усі нетехнічні рядки доповнення. / EN: All non-technical expansion rows.",
                _ => true),
            new("key", "UA: Збіг за ключем / EN: Matched by key",
                "UA: Ключ є в основній грі, англійський текст однаковий — переклад переноситься за ключем. / " +
                "EN: The key exists in the base game and the English text is the same — the translation is taken by key.",
                r => r.Kind == TransferMatchKind.Key),
            new("minor", "UA: Дрібна відмінність тексту / EN: Minor text difference",
                "UA: Ключ є в основній грі, англійські тексти відрізняються лише дрібницею (одрук, літера чи розділовий знак; не більше 3 правок і 5% довжини, від 20 символів, цифри однакові) — переклад переноситься за ключем без підтвердження. / " +
                "EN: The key exists in the base game and the English texts differ only by a trifle (a typo, a letter or punctuation mark; at most 3 edits and 5% of the length, from 20 characters, the same digits) — the translation is taken by key without a confirmation.",
                r => r.Minor),
            new("text", "UA: Та сама фраза в основній грі / EN: Same phrase in base game",
                "UA: Ключа немає в основній грі (або він має там інший текст), але така англійська фраза є під іншим ключем — береться її переклад. / " +
                "EN: The key is not in the base game (or has a different text there), but the same English phrase exists under another key — its translation is taken.",
                r => r.Kind == TransferMatchKind.Text),
            new("conflict", "UA: Новий ключ, кілька перекладів / EN: New key, several translations",
                "UA: Така фраза в основній грі має кілька різних перекладів (під різними ключами) — потрібен вибір. / " +
                "EN: The phrase has several different translations in the base game (under different keys) — a choice is needed.",
                r => r.Kind == TransferMatchKind.Conflict),
            new("changed", "UA: Ключ є, текст змінено, фрази немає / EN: Key found, text changed, no phrase",
                "UA: Ключ є в основній грі, англійський текст доповнення інший, такої фрази в основній грі немає — за замовчуванням лишається поточний переклад; рядок іде на підтвердження. / " +
                "EN: The key exists in the base game, the expansion's English text differs and the phrase is absent from the base game — the current translation stays by default; the row goes to the review.",
                r => r.Kind == TransferMatchKind.TextChanged),
            new("review", "UA: Піде на підтвердження / EN: Goes to review",
                "UA: Ключ є в основній грі, але англійський текст за ним інший (з фразою в основній грі або без): перед перенесенням програма покаже ці рядки й дозволить обрати переклад або ввести свій. / " +
                "EN: The key exists in the base game, but the English text under it differs (with or without the phrase in the base game): before the transfer the program shows these rows and lets you pick a translation or type your own.",
                r => r.NeedsReview),
            new("none", "UA: Лише в доповненні / EN: Expansion only",
                "UA: Ні ключа, ні такої фрази в основній грі немає (нові рядки доповнення) — поточний переклад лишається без змін. / " +
                "EN: Neither the key nor the phrase exists in the base game (new expansion rows) — the current translation is left untouched.",
                r => r.Kind == TransferMatchKind.None),
            new("differ", "UA: Переклад за ключем ≠ за фразою / EN: Key vs phrase translation differ",
                "UA: Збіг за ключем є, але та сама англійська фраза в основній грі перекладена інакше під іншим ключем. / " +
                "EN: A key match exists, but the same English phrase is translated differently in the base game under another key.",
                r => r.Differ)
        ];

        foreach (var f in _filters)
        {
            var button = new Button
            {
                Tag = f.Tag,
                Content = $"{f.Caption} ({_all.Count(f.Test):N0})",
                Margin = new Thickness(0, 0, 4, 4)
            };
            button.SetResourceReference(StyleProperty, "FilterBtn");
            button.Click += FilterButton_Click;
            _buttons[f.Tag] = button;
            FilterPanel.Children.Add(button);
        }

        int review = _all.Count(r => r.NeedsReview);
        NoteText.Text =
            "UA: Це лише перегляд звіту порівняння: він уже збережений у теці transfer і нічого не змінює. " +
            "Щоб виконати перенесення, закрийте це вікно та натисніть «Перенести» у вікні перенесення. " +
            $"Рядків, які програма покаже на підтвердження перед записом: {review:N0}. " +
            "Результат зберігається автоматично в новий DAT у теці translated.\n" +
            "EN: This is only a viewer of the comparison report: it is already saved in the transfer folder and changes nothing. " +
            "To run the transfer, close this window and press \"Transfer\" in the transfer window. " +
            $"Rows the program will show for confirmation before writing: {review:N0}. " +
            "The result is saved automatically to a new DAT in the translated folder.";

        PathText.Text = filePath;
        PathText.ToolTip = filePath;

        _view = CollectionViewSource.GetDefaultView(_all);
        _view.Filter = o => o is ReportViewRow r && Passes(r);
        ReportGrid.ItemsSource = _view;

        Highlight();
        UpdateCount();
        HintText.Text = _filters.First(f => f.Tag == _active).Hint;
    }

    private bool Passes(ReportViewRow r)
    {
        var def = _filters.First(f => f.Tag == _active);
        if (!def.Test(r)) return false;
        if (_search.Length == 0) return true;

        return Contains(r.Key) || Contains(r.TargetOriginal) || Contains(r.DonorOriginal) ||
               Contains(r.ByKey) || Contains(r.ByText);

        bool Contains(string s) => s.Contains(_search, StringComparison.OrdinalIgnoreCase);
    }

    private void Highlight()
    {
        foreach (var (tag, button) in _buttons)
        {
            bool on = tag == _active;
            button.SetResourceReference(BackgroundProperty, on ? "AccentDark" : "BgCard");
            button.SetResourceReference(Control.BorderBrushProperty, on ? "Accent" : "BdAcc");
            // UA: Світлий текст на темному фоні активної кнопки в обох темах.
            // EN: Light text on the active button's dark background in both themes.
            if (on) button.Foreground = Brushes.White;
            else button.SetResourceReference(ForegroundProperty, "TextPrim");
            button.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private void UpdateCount()
    {
        int shown = _all.Count(Passes);
        CountText.Text = $"UA: Показано / EN: Shown: {shown:N0} / {_all.Count:N0}";
    }

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        _active = tag;
        HintText.Text = _filters.First(f => f.Tag == _active).Hint;
        Highlight();
        _view.Refresh();
        UpdateCount();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // UA: Подія може прийти до створення представлення (під час InitializeComponent)
        // EN: The event can fire before the view exists (during InitializeComponent)
        if (_view is null) return;

        _search = SearchBox.Text.Trim();
        _view.Refresh();
        UpdateCount();
    }

    /// <summary>
    /// UA: Відкриває теку зі звітом у Провіднику й виділяє файл.
    /// EN: Opens the report's folder in Explorer with the file selected.
    /// </summary>
    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string full = Path.GetFullPath(_filePath);
            // UA: Виконуваний файл фіксований, шлях — лише з тек програми.
            // EN: The executable is fixed; the path comes only from the application's folders.
            var psi = new ProcessStartInfo("explorer.exe")
            {
                UseShellExecute = false,
                Arguments = File.Exists(full)
                    ? "/select,\"" + full + "\""
                    : "\"" + (Path.GetDirectoryName(full) ?? full) + "\""
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "TransferReportWindow.OpenFolder");
        }
    }

    /// <summary>
    /// UA: Копіює виділені комірки (Ctrl+C робить те саме стандартним способом DataGrid).
    /// EN: Copies the selected cells (Ctrl+C does the same through the DataGrid's own mechanism).
    /// </summary>
    private void CopyCells_Click(object sender, RoutedEventArgs e)
    {
        ReportGrid.ClipboardCopyMode = DataGridClipboardCopyMode.ExcludeHeader;
        System.Windows.Input.ApplicationCommands.Copy.Execute(null, ReportGrid);
    }

    /// <summary>
    /// UA: Копіює всі комірки рядків, до яких належать виділені комірки (з табуляцією між колонками).
    /// EN: Copies all cells of the rows that contain the selected cells (tab-separated).
    /// </summary>
    private void CopyRows_Click(object sender, RoutedEventArgs e)
    {
        var rows = ReportGrid.SelectedCells
            .Select(c => c.Item)
            .OfType<ReportViewRow>()
            .Distinct()
            .ToList();
        if (rows.Count == 0) return;

        var lines = rows.Select(r => string.Join('\t',
            Flat(r.Key), r.KindLabel, r.DifferLabel, Flat(r.DonorOriginal),
            Flat(r.TargetOriginal), Flat(r.ByKey), Flat(r.ByText)));
        try { Clipboard.SetText(string.Join(Environment.NewLine, lines)); }
        catch (Exception ex) { SimpleLogger.LogError(ex, "TransferReportWindow.CopyRows"); }

        // UA: Переноси й табуляції в тексті замінюються пробілами, щоб рядок лишався рядком таблиці.
        // EN: Line breaks and tabs inside the text become spaces so a row stays one table row.
        static string Flat(string t) => t.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
