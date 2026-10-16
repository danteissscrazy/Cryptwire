namespace Cryptwire.Core.Skins;

/// <summary>
/// Vigila la carpeta de una piel y avisa cuando cambia algo, para recargarla en caliente. Espera a
/// que pase un rato sin cambios antes de avisar: un editor suele escribir un fichero en varios pasos y
/// así se recarga una sola vez, con el fichero ya entero.
/// </summary>
public sealed class ThemeWatcher : IDisposable
{
    private readonly FileSystemWatcher watcher;
    private readonly Timer timer;
    private readonly TimeSpan quiet;
    private volatile bool disposed;

    /// <param name="directory">La carpeta de la piel; se vigilan también sus subcarpetas.</param>
    /// <param name="quiet">Cuánto tiempo sin cambios hace falta para avisar.</param>
    public ThemeWatcher(string directory, TimeSpan quiet)
    {
        this.quiet = quiet;
        timer = new Timer(_ => Raise());
        watcher = new FileSystemWatcher(directory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        watcher.Changed += OnChange;
        watcher.Created += OnChange;
        watcher.Deleted += OnChange;
        watcher.Renamed += OnChange;
        // Si se pierden avisos (demasiados cambios a la vez), se recarga igualmente.
        watcher.Error += (_, _) => Restart();
        watcher.EnableRaisingEvents = true;
    }

    /// <summary>La piel ha cambiado en disco. Llega desde un hilo del sistema, no desde el de la ventana.</summary>
    public event EventHandler? Changed;

    public void Dispose()
    {
        disposed = true;
        watcher.Dispose();
        timer.Dispose();
    }

    private void OnChange(object sender, FileSystemEventArgs e) => Restart();

    private void Restart()
    {
        if (disposed)
        {
            return;
        }

        try
        {
            timer.Change(quiet, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // Se ha cerrado justo ahora: ya no hay nadie a quien avisar.
        }
    }

    private void Raise()
    {
        if (!disposed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
