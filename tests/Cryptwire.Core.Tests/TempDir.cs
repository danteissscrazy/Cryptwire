namespace Cryptwire.Core.Tests;

/// <summary>Una carpeta temporal propia de cada test, que se borra al terminar.</summary>
internal sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cw-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>Escribe un fichero (creando sus carpetas) y devuelve su ruta completa.</summary>
    public string Write(string relative, string content)
    {
        var full = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Un vigilante de ficheros puede tenerla abierta un instante; la limpia el sistema.
        }
    }
}
