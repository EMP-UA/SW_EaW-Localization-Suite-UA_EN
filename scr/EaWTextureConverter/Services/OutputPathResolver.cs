using System.IO;

namespace EaWTextureConverter.Services;

public static class OutputPathResolver
{
    public static string? FindModRoot(string fullPath)
    {
        string? dir = Path.GetDirectoryName(fullPath);
        string? bestRoot = null;

        while (dir is not null)
        {
            if (IsModRoot(dir))
                bestRoot = dir; // UA: не зупиняємось — йдемо далі вгору
                                // EN: don't stop — keep going up

            dir = Path.GetDirectoryName(dir);
        }

        return bestRoot;
    }

    private static bool IsModRoot(string dir)
    {
        // UA: patch2 ніколи не є коренем моду — це завжди підпапка Data
        // EN: patch2 is never a mod root — it's always a subfolder of Data
        string dirName = Path.GetFileName(dir);
        if (dirName.Equals("patch2", StringComparison.OrdinalIgnoreCase))
            return false;
        if (dirName.Equals("DATA", StringComparison.OrdinalIgnoreCase))
            return false;
        if (dirName.Equals("ART", StringComparison.OrdinalIgnoreCase))
            return false;
        if (dirName.Equals("TEXTURES", StringComparison.OrdinalIgnoreCase))
            return false;

        // UA: Корінь моду — має пряму дочірню папку Data що містить Art
        // EN: Mod root — has direct child folder Data containing Art
        string dataDir = Path.Combine(dir, "Data");
        if (!Directory.Exists(dataDir))
            return false;

        return Directory.Exists(Path.Combine(dataDir, "Art"));
    }

    public static string Resolve(
        string fullPath,
        string outputBase,
        string extension)
    {
        string? modRoot = FindModRoot(fullPath);

        string relative;
        if (modRoot is not null)
        {
            string modParent = Path.GetDirectoryName(modRoot)!;
            relative = Path.GetRelativePath(modParent, fullPath);
        }
        else
        {
            relative = Path.GetFileName(fullPath);
        }

        string withNewExt = Path.ChangeExtension(relative, extension);
        return Path.Combine(outputBase, withNewExt);
    }

    public static string DetectModLabel(string fullPath)
    {
        string? modRoot = FindModRoot(fullPath);

        if (modRoot is null)
            return "Unknown";

        return Path.GetFileName(modRoot);
    }
}