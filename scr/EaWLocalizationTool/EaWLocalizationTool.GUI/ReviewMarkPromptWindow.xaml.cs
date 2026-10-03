// =============================================================================
// EaWLocalizationTool.GUI — ReviewMarkPromptWindow.xaml.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Модальне вікно з одним рядком вводу для масового проставлення ДОВІЛЬНОГО
//     тексту вичитки (не готової позначки +, +/-, -) кільком виділеним рядкам —
//     пункт контекстного меню «Вичитка для виділених → Власний текст…».
//
//     Викликач читає ReviewText ЛИШЕ якщо ShowDialog() повернув true.
//     Порожній текст дозволений: викликач ставить «-» (не вичитано).
// EN: Modal single-line prompt for bulk-setting a FREE-FORM review text
//     (not a ready-made +, +/-, - mark) on several selected rows — the
//     "Review for selected → Custom text…" context-menu item.
//
//     The caller reads ReviewText ONLY if ShowDialog() returned true.
//     Empty text is allowed: the caller sets "-" (not reviewed).
// =============================================================================

using System.Windows;

namespace EaWLocalizationTool.GUI;

public partial class ReviewMarkPromptWindow : Window
{
    /// <summary>
    /// UA: Введений текст (без пробілів на краях); заповнюється лише при DialogResult == true.
    /// EN: The entered text (trimmed); populated only when DialogResult == true.
    /// </summary>
    public string ReviewText { get; private set; } = string.Empty;

    /// <summary>
    /// UA: selectedCount — кількість виділених рядків (показується в підказці);
    ///     initialText — початкове значення поля (спільна позначка виділених
    ///     рядків, якщо вона в усіх однакова).
    /// EN: selectedCount — number of selected rows (shown in the hint);
    ///     initialText — the field's starting value (the selected rows'
    ///     common mark, if they all share one).
    /// </summary>
    public ReviewMarkPromptWindow(int selectedCount, string initialText)
    {
        InitializeComponent();

        SelectionInfoText.Text =
            $"UA: Виділено рядків: {selectedCount} / EN: Selected rows: {selectedCount}";
        ReviewTextBox.Text = initialText;
        ReviewTextBox.SelectAll();
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        ReviewText = (ReviewTextBox.Text ?? string.Empty).Trim();
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
