// =============================================================================
// EaWLocalizationTool.Core — TextGuard.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Перевірка набору символів у тексті перекладу.
// EN: Character set check for translation text.
// =============================================================================

using System.Buffers;

namespace EaWLocalizationTool.Core;

public static class TextGuard
{
    // UA: Недопустимі символи (коди Unicode).
    // EN: Disallowed characters (Unicode code points).
    private static readonly SearchValues<char> Blocked =
        SearchValues.Create("\u044A\u044B\u044D\u0451\u042A\u042B\u042D\u0401");

    /// <summary>
    /// UA: Повідомлення для користувача при відхиленні тексту.
    /// EN: User-facing message when a text is rejected.
    /// </summary>
    public const string RejectMessage =
        "UA: Текст містить недопустимі символи / EN: The text contains invalid characters";

    /// <summary>
    /// UA: True, якщо текст порожній або містить лише допустимі символи.
    /// EN: True when the text is empty or contains only allowed characters.
    /// </summary>
    public static bool IsClean(string? text) =>
        string.IsNullOrEmpty(text) || text.AsSpan().IndexOfAny(Blocked) < 0;
}
