// =============================================================================
// EaWLocalizationTool.GUI — BilingualText.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Відображення двомовного тексту «UA: … / EN: …» у два рядки: українська частина
//     зверху, англійська завжди другим рядком. Кожен рядок переноситься за шириною
//     елемента, тож жодна з мов не обрізається. Застосовується до кнопок і заголовків
//     колонок через ContentTemplateSelector (див. App.xaml).
//     Вміст, що не є рядком (панелі, зображення), показується без змін.
// EN: Rendering of bilingual text "UA: … / EN: …" on two lines: the Ukrainian part on
//     top, the English part always on the second line. Each line wraps to the element's
//     width, so neither language is truncated. Applied to buttons and column headers
//     through a ContentTemplateSelector (see App.xaml).
//     Content that is not a string (panels, images) is shown unchanged.
// =============================================================================

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace EaWLocalizationTool.GUI;

/// <summary>
/// UA: Перетворює «UA: … / EN: …» на «UA: …» + перенос рядка + «EN: …».
///     Рядки без обох позначок повертаються без змін.
/// EN: Turns "UA: … / EN: …" into "UA: …" + a line break + "EN: …".
///     Strings lacking either marker are returned unchanged.
/// </summary>
public sealed class BilingualText : IValueConverter
{
    public static readonly BilingualText Converter = new();

    private const string Separator = " / EN:";

    public static string Split(string text)
    {
        if (!text.Contains("UA:", StringComparison.Ordinal)) return text;

        int index = text.IndexOf(Separator, StringComparison.Ordinal);
        if (index < 0) return text;

        return text[..index].TrimEnd() + "\nEN:" + text[(index + Separator.Length)..];
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string text ? Split(text) : value ?? string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// UA: Повертає шаблон тексту лише для рядкового вмісту; для іншого вмісту — null
///     (елемент малює його стандартно).
/// EN: Returns the text template for string content only; null for anything else
///     (the element renders it the default way).
/// </summary>
public sealed class BilingualTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container) =>
        item is string ? TextTemplate : null;
}
