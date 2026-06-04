namespace EaWLocalizationTool.Core.Models;

/// <summary>
/// UA: Один запис DAT файлу з сирими байтами для безпечного запису.
///     Використовується і консоллю, і GUI.
/// EN: Single DAT file record with raw bytes for safe writing.
///     Used by both console and GUI.
/// </summary>
public class DatEntry
{
    // UA: Позиція в оригінальному файлі — зберігає порядок записів
    // EN: Position in original file — preserves record order
    public int    OriginalIndex { get; init; }
    public string Key          { get; init; } = "";
    public string OriginalText { get; init; } = "";

    // UA: Сирі байти CRC32 та довжини ключа — копіюються без змін при записі
    // EN: Raw CRC32 and key-length bytes — copied as-is when writing
    public byte[] RawCrc32     { get; init; } = new byte[4];
    public byte[] RawKeyLength { get; init; } = new byte[4];
    public uint   KeyLength    { get; init; }
}
