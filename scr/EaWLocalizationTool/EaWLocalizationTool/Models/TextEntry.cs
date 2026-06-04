namespace EaWLocalizationTool.Models;

/// <summary>
/// UA: Модель текстового запису з підтримкою метаданих кодування.
/// EN: Text entry model with encoding metadata support.
/// </summary>
public class TextEntry
{
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty; // TXT, XML, DAT
    public string Key { get; set; } = string.Empty;
    public string OriginalText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;

    /// <summary>
    /// UA: Кодування оригіналу. Важливо для збереження сумісності з рушієм Alamo.
    /// EN: Original encoding. Important for maintaining compatibility with the Alamo engine.
    /// </summary>
    public string OriginalEncodingName { get; set; } = "utf-16";
}