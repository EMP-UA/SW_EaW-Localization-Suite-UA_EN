// =============================================================================
// EaWLocalizationTool.GUI — TranslationConflictWindow.xaml.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Модальний діалог ручного вирішення конфліктів перенесення перекладу
//     (MainWindow.RunTransfer → DatTranslationTransfer.CollectConflicts).
//     Один рядок таблиці — один англійський оригінал, для якого донор має
//     кілька різних перекладів. Перший варіант у списку — SkipMarker (обраний
//     типово), тож конфлікт, залишений без уваги, не переноситься мовчки.
//
//     Викликач читає Resolutions ЛИШЕ якщо ShowDialog() повернув true.
// EN: Modal dialog for manually resolving translation-transfer conflicts
//     (MainWindow.RunTransfer → DatTranslationTransfer.CollectConflicts).
//     One grid row = one English original for which the donor has several
//     distinct translations. The first option in the list is SkipMarker
//     (selected by default), so a conflict left untouched is not transferred
//     silently.
//
//     The caller reads Resolutions ONLY if ShowDialog() returned true.
// =============================================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace EaWLocalizationTool.GUI;

public partial class TranslationConflictWindow : Window
{
    public const string SkipMarker = "— UA: пропустити / EN: skip —";

    public ObservableCollection<ConflictRow> Rows { get; } = [];

    /// <summary>
    /// UA: Нормалізований оригінал → обраний переклад. Заповнюється при натисканні
    ///     «Застосувати»; пропущені конфлікти сюди не потрапляють.
    /// EN: Normalized original → chosen translation. Populated on "Apply";
    ///     skipped conflicts are not included.
    /// </summary>
    public IReadOnlyDictionary<string, string> Resolutions { get; private set; } =
        new Dictionary<string, string>();

    /// <summary>
    /// UA: conflicts — англійський оригінал рядка (як у цілі) → варіанти перекладу.
    ///     originals — англійський оригінал рядка (як у цілі) → текст для показу.
    /// EN: conflicts — the row's English original (as in the target) → translation variants.
    ///     originals — the row's English original (as in the target) → the text to display.
    /// </summary>
    public TranslationConflictWindow(
        IReadOnlyDictionary<string, IReadOnlyList<string>> conflicts,
        IReadOnlyDictionary<string, string> originals)
    {
        InitializeComponent();

        foreach (var (original, candidates) in conflicts.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var options = new List<string> { SkipMarker };
            options.AddRange(candidates);
            Rows.Add(new ConflictRow
            {
                Key      = original,
                Original = originals.TryGetValue(original, out var text) ? text : original,
                Options  = options,
                Selected = SkipMarker
            });
        }

        ConflictsGrid.ItemsSource = Rows;
        SummaryText.Text =
            $"UA: Конфліктних рядків: {Rows.Count} / EN: Conflicting strings: {Rows.Count}";
    }

    private void SkipAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in Rows)
            row.Selected = SkipMarker;
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var resolutions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in Rows)
        {
            if (row.Selected != SkipMarker)
                resolutions[row.Key] = row.Selected;
        }

        Resolutions  = resolutions;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

/// <summary>
/// UA: Один рядок таблиці конфліктів: оригінал, список варіантів (перший — SkipMarker)
///     і обраний варіант.
/// EN: One row of the conflicts grid: the original, the list of options (the first
///     is SkipMarker) and the chosen option.
/// </summary>
public class ConflictRow : INotifyPropertyChanged
{
    // UA: Нормалізований оригінал — ключ рішення / EN: Normalized original — the resolution key
    public required string Key { get; init; }
    public required string Original { get; init; }
    public required List<string> Options { get; init; }

    private string _selected = TranslationConflictWindow.SkipMarker;
    public string Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
