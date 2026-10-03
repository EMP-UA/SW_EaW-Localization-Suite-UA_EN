// =============================================================================
// EaWLocalizationTool.Core — CaseAdapter.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Перенесення регістру між схожими рядками. Англійські оригінали, які
//     відрізняються лише регістром («argh!», «Argh!», «ARGH!»), мають
//     перекладатися одним словом у відповідному регістрі. Модуль визначає
//     «візерунок» регістру тексту та перетворює переклад з візерунка одного
//     оригіналу на візерунок іншого.
//
//     Візерунки: Lower (усе малими), Upper (усе великими), Sentence (перша літера
//     велика, решта малі), Title (кожне слово з великої), Mixed (будь-що інше),
//     None (літер немає). Для Mixed і None перетворення не виконується: механічно
//     правильного результату в них немає.
// EN: Case transfer between similar rows. English originals that differ only in
//     case ("argh!", "Argh!", "ARGH!") should be translated with the same word in the
//     matching case. The module detects a text's case "pattern" and converts a
//     translation from one original's pattern to another's.
//
//     Patterns: Lower (all small), Upper (all capitals), Sentence (first letter
//     capital, the rest small), Title (every word capitalized), Mixed (anything
//     else), None (no letters). Mixed and None are not converted: there is no
//     mechanically correct result for them.
// =============================================================================

namespace EaWLocalizationTool.Core;

/// <summary>
/// UA: Візерунок регістру тексту.
/// EN: The case pattern of a text.
/// </summary>
public enum CasePattern { None, Lower, Upper, Sentence, Title, Mixed }

public static class CaseAdapter
{
    /// <summary>
    /// UA: Визначає візерунок регістру тексту.
    /// EN: Detects a text's case pattern.
    /// </summary>
    public static CasePattern Detect(string text)
    {
        int letters = 0, upper = 0, lower = 0;
        foreach (char c in text)
        {
            if (!char.IsLetter(c)) continue;
            letters++;
            if (char.IsUpper(c)) upper++;
            else if (char.IsLower(c)) lower++;
        }

        if (letters == 0) return CasePattern.None;
        if (upper == 0) return CasePattern.Lower;
        if (lower == 0) return letters >= 2 ? CasePattern.Upper : CasePattern.Sentence;

        // UA: Перша літера тексту велика, інших великих немає — Sentence.
        // EN: The text's first letter is capital and there are no other capitals — Sentence.
        char first = text.First(char.IsLetter);
        if (char.IsUpper(first) && upper == 1) return CasePattern.Sentence;

        // UA: Кожне слово з літерами починається з великої, а решта його літер малі — Title.
        // EN: Every word with letters starts with a capital and the rest of its letters are small — Title.
        int words = 0;
        foreach (var token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var wordLetters = token.Where(char.IsLetter).ToList();
            if (wordLetters.Count == 0) continue;

            words++;
            if (!char.IsUpper(wordLetters[0]) || wordLetters.Skip(1).Any(char.IsUpper))
                return CasePattern.Mixed;
        }

        return words >= 2 ? CasePattern.Title : CasePattern.Mixed;
    }

    /// <summary>
    /// UA: Перетворює переклад із візерунка одного оригіналу на візерунок іншого;
    ///     повертає null, якщо перетворення неможливе (Mixed або None з будь-якого боку).
    ///     Символи, окрім регістру літер, не змінюються.
    /// EN: Converts a translation from one original's pattern to another's; returns
    ///     null when the conversion is impossible (Mixed or None on either side).
    ///     Nothing but the case of letters is changed.
    /// </summary>
    public static string? Adapt(string translation, CasePattern from, CasePattern to)
    {
        if (from is CasePattern.None or CasePattern.Mixed) return null;
        if (to is CasePattern.None or CasePattern.Mixed) return null;
        if (from == to) return translation;

        switch (to)
        {
            case CasePattern.Upper:
                return translation.ToUpperInvariant();

            case CasePattern.Lower:
                return translation.ToLowerInvariant();

            case CasePattern.Sentence:
            {
                string text = from switch
                {
                    CasePattern.Upper => translation.ToLowerInvariant(),
                    CasePattern.Title => LowerLaterWordInitials(translation),
                    _ => translation
                };
                return CapitalizeAt(text, FirstLetterIndex(text));
            }

            case CasePattern.Title:
            {
                string text = from == CasePattern.Upper ? translation.ToLowerInvariant() : translation;
                return CapitalizeWordInitials(text);
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// UA: Підбирає переклад для цільового оригіналу, коли відомі оригінал і переклад
    ///     джерела. Порядок: (1) візерунки оригіналів; (2) великі/малі ПЕРШІ літери слів
    ///     за позиціями; (3) візерунок самого перекладу. Повертає null, якщо жоден
    ///     спосіб не дає однозначного результату.
    /// EN: Picks a translation for a target original, given the source original and
    ///     translation. Order: (1) the originals' patterns; (2) capital/small WORD
    ///     INITIALS by position; (3) the translation's own pattern. Returns null when
    ///     no way gives an unambiguous result.
    /// </summary>
    public static string? AdaptFor(string sourceOriginal, string sourceTranslation, string targetOriginal)
    {
        var from = Detect(sourceOriginal);
        var to = Detect(targetOriginal);

        string? byPattern = Adapt(sourceTranslation, from, to);
        if (byPattern is not null) return byPattern;

        string? byInitials = AdaptByWordInitials(sourceTranslation, targetOriginal);
        if (byInitials is not null) return byInitials;

        return from == CasePattern.Mixed ? Adapt(sourceTranslation, Detect(sourceTranslation), to) : null;
    }

    /// <summary>
    /// UA: Змінює лише першу літеру кожного слова перекладу так, як вона записана в
    ///     словах цільового оригіналу («Communications Array destroyed» → великі,
    ///     великі, мала). Працює, коли в оригіналі лише перші літери слів можуть бути
    ///     великими, а кількість слів з літерами в оригіналі та перекладі однакова;
    ///     інакше повертає null. Решта літер перекладу не змінюється.
    /// EN: Changes only the first letter of each translation word to match how it is
    ///     written in the target original's words ("Communications Array destroyed" →
    ///     capital, capital, small). Works when only word-initial letters in the original
    ///     can be capitals and the original and the translation have the same number of
    ///     words with letters; otherwise returns null. The other letters stay as they are.
    /// </summary>
    public static string? AdaptByWordInitials(string translation, string targetOriginal)
    {
        var initials = new List<bool>(); // true = capital / велика
        foreach (var token in targetOriginal.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var letters = token.Where(char.IsLetter).ToList();
            if (letters.Count == 0) continue;
            if (letters.Skip(1).Any(char.IsUpper)) return null;
            initials.Add(char.IsUpper(letters[0]));
        }
        if (initials.Count < 2) return null;

        var chars = translation.ToCharArray();
        int index = 0;
        bool atWordStart = true;
        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsWhiteSpace(chars[i])) { atWordStart = true; continue; }

            // UA: Слово може починатися з не-літери (лапка, дужка): його перша літера йде далі.
            // EN: A word may begin with a non-letter (quote, bracket): its first letter comes later.
            if (!atWordStart || !char.IsLetter(chars[i])) continue;

            if (index >= initials.Count) return null;
            chars[i] = initials[index] ? char.ToUpperInvariant(chars[i]) : char.ToLowerInvariant(chars[i]);
            index++;
            atWordStart = false;
        }

        return index == initials.Count ? new string(chars) : null;
    }

    private static int FirstLetterIndex(string text)
    {
        for (int i = 0; i < text.Length; i++)
            if (char.IsLetter(text[i])) return i;
        return -1;
    }

    private static string CapitalizeAt(string text, int index) =>
        index < 0 ? text : text[..index] + char.ToUpperInvariant(text[index]) + text[(index + 1)..];

    // UA: Велика перша літера кожного слова (слова — відрізки між пробільними символами).
    // EN: A capital first letter in every word (words are runs between whitespace).
    private static string CapitalizeWordInitials(string text)
    {
        var chars = text.ToCharArray();
        bool atWordStart = true;
        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsWhiteSpace(chars[i])) { atWordStart = true; continue; }
            if (atWordStart && char.IsLetter(chars[i]))
            {
                chars[i] = char.ToUpperInvariant(chars[i]);
                atWordStart = false;
            }
        }
        return new string(chars);
    }

    // UA: Мала перша літера слів, окрім першого, якщо решта слова не містить великих
    //     (слова з кількома великими — абревіатури, лишаються).
    // EN: A small first letter in every word but the first, unless the rest of the word
    //     contains capitals (words with several capitals are abbreviations and stay).
    private static string LowerLaterWordInitials(string text)
    {
        var chars = text.ToCharArray();
        bool firstWordSeen = false;
        int i = 0;
        while (i < chars.Length)
        {
            if (char.IsWhiteSpace(chars[i])) { i++; continue; }

            int start = i;
            while (i < chars.Length && !char.IsWhiteSpace(chars[i])) i++;

            int firstLetter = -1, capitals = 0;
            for (int k = start; k < i; k++)
            {
                if (!char.IsLetter(chars[k])) continue;
                if (firstLetter < 0) firstLetter = k;
                if (char.IsUpper(chars[k])) capitals++;
            }
            if (firstLetter < 0) continue;

            if (firstWordSeen && capitals == 1 && char.IsUpper(chars[firstLetter]))
                chars[firstLetter] = char.ToLowerInvariant(chars[firstLetter]);
            firstWordSeen = true;
        }
        return new string(chars);
    }
}
