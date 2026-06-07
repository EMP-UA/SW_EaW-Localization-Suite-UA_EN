using System.IO;
using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using BCnEncoder.Shared;
using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace EaWTextureConverter.Services;

public class DdsConverterService
{
    // UA: Читаємо DDS через Magick.NET → зберігаємо як PNG32 зі збереженням альфа-каналу
    // EN: Read DDS via Magick.NET → save as PNG32 preserving alpha channel
    public void DdsToPng(string ddsPath, string pngPath)
    {
        using var magick = new MagickImage(ddsPath);

        uint w = magick.Width;
        uint h = magick.Height;
        if (!IsPow2(w) || !IsPow2(h))
            Console.WriteLine(
                $"[!] {Path.GetFileName(ddsPath)}: {w}×{h} — не POT, але може працювати для UI");

        magick.Write(pngPath, MagickFormat.Png32);
    }

    // UA: PNG → DDS uncompressed BGRA — саме такий формат використовує оригінальний EaW
    // EN: PNG → DDS uncompressed BGRA — this is the exact format used by original EaW
    public async Task PngToDdsAsync(
        string pngPath,
        string ddsPath,
        CancellationToken ct = default)
    {
        using var image = await Image.LoadAsync<Rgba32>(pngPath, ct);

        int width = image.Width;
        int height = image.Height;

        // UA: Збираємо піксельні дані у порядку BGRA
        // EN: Collect pixel data in BGRA order
        byte[] pixels = new byte[width * height * 4];

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                int rowOffset = y * width * 4;
                for (int x = 0; x < row.Length; x++)
                {
                    int i = rowOffset + x * 4;
                    pixels[i + 0] = row[x].B; // UA: B першим — BGRA порядок
                    pixels[i + 1] = row[x].G;
                    pixels[i + 2] = row[x].R;
                    pixels[i + 3] = row[x].A;
                }
            }
        });

        await using var output = File.Create(ddsPath);
        await WriteDdsBgraAsync(output, width, height, pixels, ct);
    }

    // UA: Записуємо DDS вручну з правильним BGRA заголовком (DirectX 9 сумісний)
    // EN: Write DDS manually with correct BGRA header (DirectX 9 compatible)
    private static async Task WriteDdsBgraAsync(
        Stream output,
        int width,
        int height,
        byte[] pixels,
        CancellationToken ct)
    {
        byte[] header = new byte[128];

        // Magic
        header[0] = (byte)'D'; header[1] = (byte)'D';
        header[2] = (byte)'S'; header[3] = (byte)' ';

        // Size of header = 124
        WriteInt(header, 4, 124);

        // Flags: CAPS | HEIGHT | WIDTH | PITCH | PIXELFORMAT
        WriteInt(header, 8, 0x1 | 0x2 | 0x4 | 0x8 | 0x1000);

        // Height, Width
        WriteInt(header, 12, height);
        WriteInt(header, 16, width);

        // Pitch = width * 4 bytes per pixel
        WriteInt(header, 20, width * 4);

        // Depth, MipMapCount
        WriteInt(header, 24, 1);
        WriteInt(header, 28, 0);

        // Pixel format size = 32
        WriteInt(header, 76, 32);

        // PF flags: RGBA (0x41 = DDPF_RGB | DDPF_ALPHAPIXELS)
        WriteInt(header, 80, 0x41);

        // FourCC = 0 (uncompressed)
        WriteInt(header, 84, 0);

        // RGB bit count
        WriteInt(header, 88, 32);

        // UA: Маски у порядку BGRA — саме так читає EaW (DirectX 9)
        // EN: Masks in BGRA order — this is how EaW (DirectX 9) reads them
        WriteInt(header, 92, 0x00FF0000); // R mask
        WriteInt(header, 96, 0x0000FF00); // G mask
        WriteInt(header, 100, 0x000000FF); // B mask
        WriteInt(header, 104, unchecked((int)0xFF000000)); // A mask

        // Caps: TEXTURE
        WriteInt(header, 108, 0x1000);

        await output.WriteAsync(header, ct);
        await output.WriteAsync(pixels, ct);
    }

    private static void WriteInt(byte[] buf, int offset, int value)
    {
        buf[offset + 0] = (byte)(value);
        buf[offset + 1] = (byte)(value >> 8);
        buf[offset + 2] = (byte)(value >> 16);
        buf[offset + 3] = (byte)(value >> 24);
    }

    private static bool IsPow2(uint n) => n > 0 && (n & (n - 1)) == 0;
}