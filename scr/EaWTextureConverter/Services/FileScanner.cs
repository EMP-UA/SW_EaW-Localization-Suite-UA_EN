using System.IO;
using EaWTextureConverter.Models;

namespace EaWTextureConverter.Services;

public static class FileScanner
{
    // UA: Рекурсивно знаходить всі DDS файли в папці та підпапках
    // EN: Recursively finds all DDS files under a directory and its subdirectories
    public static IReadOnlyList<TextureFile> ScanDirectory(string rootPath)
    {
        if (!Directory.Exists(rootPath))
            return [];

        return Directory
            .EnumerateFiles(rootPath, "*.dds", SearchOption.AllDirectories)
            .Select(fullPath => new TextureFile
            {
                FullPath = fullPath,
                RelativePath = Path.GetRelativePath(rootPath, fullPath),
                ModLabel = OutputPathResolver.DetectModLabel(fullPath),
            })
            .OrderBy(f => f.ModLabel)
            .ThenBy(f => f.RelativePath)
            .ToList();
    }
}