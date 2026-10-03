// =============================================================================
// EaWLocalizationTool.GUI — TransferReviewWindow.xaml.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Модальний діалог підтвердження перенесення для рядків, у яких ключ є в основній
//     грі, але англійський текст за цим ключем інший (DatTranslationTransfer.NeedsReview).
//     Для кожного рядка видно англійський текст доповнення й основної гри, поточний
//     переклад доповнення, переклад за ключем і переклад за англійською фразою.
//     Колонка «Результат» відображається й редагується, як переклад у головному вікні
//     (перенос рядків, 2× клік — багаторядкове поле); контекстне меню комірок копіює
//     значення, вносить його в результат, додає до нього або вносить усі варіанти одразу,
//     а також застосовує результат до рядків з таким самим англійським текстом. Типово: рядок, фраза якого знайдена в основній грі, отримує
//     переклад за фразою; рядок без такої фрази лишає поточний переклад.
//     Введений текст проходить перевірку набору символів (TextGuard).
//
//     Викликач читає Results ЛИШЕ якщо ShowDialog() повернув true.
// EN: A modal dialog confirming the transfer for rows whose key exists in the base game
//     but whose English text under that key differs (DatTranslationTransfer.NeedsReview).
//     Each row shows the expansion's and the base game's English text, the expansion's
//     current translation, the translation by key and the translation by English phrase.
//     The "Result" column is shown and edited like a translation in the main window
//     (wrapped text, a double-click opens a multi-line box); the cell context menu copies
//     the value, puts it into the result, appends it or inserts all variants at once,
//     and applies the result to rows with the same English text. By default a row whose phrase was found in the base game gets the translation
//     by phrase; a row without such a phrase keeps its current translation.
//     Typed text is checked against the allowed character set (TextGuard).
//
//     The caller reads Results ONLY if ShowDialog() returned true.
// =============================================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using EaWLocalizationTool.GUI.Services;
using EaWLocalizationTool.Core;

namespace EaWLocalizationTool.GUI;

public partial class TransferReviewWindow : Window
{
    public ObservableCollection<ReviewRow> Rows { get; } = [];

    /// <summary>
    /// UA: Ключ рядка → текст, який треба записати. Порожні результати не включаються.
    /// EN: Row key → the text to write. Empty results are not included.
    /// </summary>
    public IReadOnlyDictionary<string, string> Results { get; private set; } =
        new Dictionary<string, string>();

    /// <summary>
    /// UA: Вхідні дані одного рядка.
    /// EN: Input data of one row.
    /// </summary>
    public sealed record Item(
        string Key,
        string TargetOriginal,
        string DonorOriginal,
        string Current,
        string? ByKey,
        IReadOnlyList<string> ByPhrase,
        bool PhraseFound);

    public TransferReviewWindow(IEnumerable<Item> items)
    {
        InitializeComponent();

        foreach (var item in items.OrderBy(i => i.Key, StringComparer.Ordinal))
            Rows.Add(new ReviewRow(item));

        ReviewGrid.ItemsSource = Rows;
        SummaryText.Text =
            $"UA: Рядків для підтвердження: {Rows.Count} / EN: Rows to confirm: {Rows.Count}";
    }

    private IEnumerable<ReviewRow> Selected() => ReviewGrid.SelectedItems.OfType<ReviewRow>();

    private void UseByKey_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in Selected())
            if (!string.IsNullOrEmpty(row.ByKey)) row.Result = row.ByKey;
    }

    private void UseByPhrase_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in Selected())
            if (row.PhraseVariants.Count > 0) row.Result = row.PhraseVariants[0];
    }

    // ── Контекстне меню комірок / Cell context menu ──────────────────────────

    /// <summary>
    /// UA: Комірка, з якої відкрито меню, і її рядок.
    /// EN: The cell the menu was opened from, and its row.
    /// </summary>
    private static (TextBlock Cell, ReviewRow Row)? CellOf(object sender)
    {
        if (sender is MenuItem { Parent: ContextMenu { PlacementTarget: TextBlock cell } } &&
            cell.DataContext is ReviewRow row)
            return (cell, row);
        return null;
    }

    private void CtxCopy_Click(object sender, RoutedEventArgs e)
    {
        if (CellOf(sender) is not { } c) return;
        try { Clipboard.SetText(c.Cell.Text); }
        catch (Exception ex) { SimpleLogger.LogError(ex, "TransferReviewWindow.Copy"); }
    }

    private void CtxToResult_Click(object sender, RoutedEventArgs e)
    {
        if (CellOf(sender) is { } c) c.Row.Result = c.Cell.Text;
    }

    private void CtxAppend_Click(object sender, RoutedEventArgs e)
    {
        if (CellOf(sender) is not { } c || string.IsNullOrEmpty(c.Cell.Text)) return;
        c.Row.Result = string.IsNullOrEmpty(c.Row.Result)
            ? c.Cell.Text
            : c.Row.Result + Environment.NewLine + c.Cell.Text;
    }

    private void CtxAllVariants_Click(object sender, RoutedEventArgs e)
    {
        if (CellOf(sender) is { } c) c.Row.Result = c.Row.AllVariants();
    }

    /// <summary>
    /// UA: Копіює результат рядка в усі інші рядки таблиці з точно таким самим англійським
    ///     текстом доповнення.
    /// EN: Copies the row's result into every other row of the grid with exactly the same
    ///     expansion English text.
    /// </summary>
    private void CtxResultToDuplicates_Click(object sender, RoutedEventArgs e)
    {
        if (CellOf(sender) is not { } c) return;

        int changed = 0;
        foreach (var other in Rows)
        {
            if (ReferenceEquals(other, c.Row)) continue;
            if (!string.Equals(other.TargetOriginal, c.Row.TargetOriginal, StringComparison.Ordinal)) continue;
            if (string.Equals(other.Result, c.Row.Result, StringComparison.Ordinal)) continue;

            other.Result = c.Row.Result;
            changed++;
        }

        SummaryText.Text =
            $"UA: Рядків для підтвердження: {Rows.Count}. Результат застосовано до {changed} рядків з таким самим англійським текстом. / " +
            $"EN: Rows to confirm: {Rows.Count}. The result was applied to {changed} rows with the same English text.";
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        // UA: Завершуємо редагування комірки, щоб останній введений текст потрапив у рядок
        // EN: Finish the cell edit so the last typed text reaches the row
        ReviewGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

        var results = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in Rows)
        {
            if (string.IsNullOrWhiteSpace(row.Result)) continue;

            // UA: Результат, у якому лишилося кілька готових варіантів, не записується мовчки.
            // EN: A result that still holds several ready-made variants is not written silently.
            if (row.HoldsSeveralVariants())
            {
                MessageBox.Show(
                    $"UA: У рядку {row.Key} у результаті лишилося кілька варіантів — залиште один.\n" +
                    $"EN: The result of row {row.Key} still holds several variants — keep one.",
                    "UA: Підтвердження перенесення / EN: Transfer review",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TextGuard.IsClean(row.Result))
            {
                MessageBox.Show(
                    $"UA: Текст у рядку {row.Key} містить недозволені символи.\n" +
                    $"EN: The text in row {row.Key} contains disallowed characters.",
                    "UA: Підтвердження перенесення / EN: Transfer review",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            results[row.Key] = row.Result;
        }

        Results = results;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

/// <summary>
/// UA: Один рядок таблиці підтвердження: вихідні дані, список варіантів і редагований результат.
/// EN: One row of the review grid: the source data, the list of options and the editable result.
/// </summary>
public sealed class ReviewRow : INotifyPropertyChanged
{
    public ReviewRow(TransferReviewWindow.Item item)
    {
        Key = item.Key;
        TargetOriginal = item.TargetOriginal;
        DonorOriginal = item.DonorOriginal;
        Current = item.Current;
        ByKey = item.ByKey ?? string.Empty;
        PhraseVariants = item.ByPhrase;
        ByPhrase = string.Join("  ‖  ", item.ByPhrase);

        var options = new List<string>();
        void Add(string? text)
        {
            if (!string.IsNullOrEmpty(text) && !options.Contains(text, StringComparer.Ordinal))
                options.Add(text);
        }
        Add(Current);
        Add(ByKey);
        foreach (var variant in item.ByPhrase) Add(variant);
        Options = options;

        _result = item.PhraseFound && item.ByPhrase.Count > 0 ? item.ByPhrase[0] : Current;
    }

    /// <summary>
    /// UA: Усі різні варіанти (поточний, за ключем, за фразою), кожен в окремому рядку.
    /// EN: All distinct variants (current, by key, by phrase), one per line.
    /// </summary>
    public string AllVariants() => string.Join(Environment.NewLine, Options);

    /// <summary>
    /// UA: Чи результат складається з двох або більше РІЗНИХ готових варіантів (по одному в рядку).
    /// EN: Whether the result consists of two or more DISTINCT ready-made variants (one per line).
    /// </summary>
    public bool HoldsSeveralVariants()
    {
        var lines = Result
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return lines.Count >= 2 &&
               lines.All(l => Options.Any(o => string.Equals(o.Trim(), l, StringComparison.Ordinal)));
    }

    public string Key { get; }
    public string TargetOriginal { get; }
    public string DonorOriginal { get; }
    public string Current { get; }
    public string ByKey { get; }
    public string ByPhrase { get; }
    public IReadOnlyList<string> PhraseVariants { get; }
    public IReadOnlyList<string> Options { get; }

    private string _result;
    public string Result
    {
        get => _result;
        set
        {
            if (_result == value) return;
            _result = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
