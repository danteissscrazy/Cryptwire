namespace Cryptwire.Core;

/// <summary>
/// Leer un fichero solo si está de verdad dentro de una carpeta. Que la ruta lo diga no basta: Windows
/// sigue uniones y enlaces simbólicos, y uno dentro de la carpeta puede apuntar a cualquier parte del disco.
/// </summary>
public static class ContainedFiles
{
    /// <summary>
    /// Los bytes del fichero si está dentro de <paramref name="root"/>, sin pasar por ningún enlace desde
    /// ella, y no pasa de <paramref name="maxBytes"/>. Null en cualquier otro caso, también si no se puede leer.
    /// </summary>
    public static byte[]? TryRead(string root, string? path, long maxBytes)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !Path.IsPathFullyQualified(root))
        {
            return null;
        }

        try
        {
            var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var full = Path.GetFullPath(path);
            if (!IsInside(fullRoot, full))
            {
                return null;
            }

            var info = new FileInfo(full);
            return info.Exists && info.Length <= maxBytes && !CrossesLink(fullRoot, full)
                ? File.ReadAllBytes(full)
                : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Si la ruta, ya completa, queda por debajo de la carpeta (y no es la carpeta misma).</summary>
    public static bool IsInside(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative != "."
            && relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }

    private static bool CrossesLink(string root, string path)
    {
        for (var current = path; current is not null && current.Length > root.Length; current = Path.GetDirectoryName(current))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }

        return false;
    }
}
