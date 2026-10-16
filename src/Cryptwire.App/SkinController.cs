using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Cryptwire.Core.Settings;
using Cryptwire.Core.Skins;
using Cryptwire.Core.Wallpapers;
using Cryptwire.Core.Widgets;
using Microsoft.Web.WebView2.Core;

namespace Cryptwire.App;

/// <summary>
/// La piel que se ve, mantenida al día cuando cambias de piel, pulsas el modo seguro, cambias tu fondo o
/// editas sus ficheros; y el dueño de tus ajustes mientras la app está abierta, para que dos cambios
/// seguidos nunca se pisen. Las reglas están en Core (<see cref="ThemeManager"/>, <see cref="SkinSession"/>,
/// <see cref="WallpaperCss"/>); esto solo conecta el disco, los ajustes y la página.
/// </summary>
internal sealed class SkinController(ThemeManager themes, ISettingsStore store, string wallpapersDir, bool safeModeThisSession, Dispatcher dispatcher)
    : IDisposable
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(300);
    private const int ReadAttempts = 3;

    // No se libera nunca: así una recarga que llega justo al cerrar no choca con un semáforo destruido.
    private readonly SemaphoreSlim busy = new(1, 1);
    private readonly SkinSession session = new(store.Load(), safeModeThisSession);
    private SkinInjector? injector;
    private ThemeWatcher? watcher;
    private string? watchedDir;
    private volatile bool disposed;

    /// <summary>Ha cambiado la piel, la lista de pieles o el modo seguro.</summary>
    public event EventHandler? StateChanged;

    public IReadOnlyList<Theme> Available { get; private set; } = [];

    /// <summary>La piel elegida, aunque el modo seguro la tenga apagada. Su tono suena igual.</summary>
    public Theme? Active { get; private set; }

    /// <summary>Modo seguro: guardado en tus ajustes o pedido al arrancar con <c>--modo-seguro</c>.</summary>
    public bool SafeMode => session.SafeMode;

    /// <summary>Se llama antes de abrir WhatsApp, para que la piel esté desde el primer documento.</summary>
    public Task AttachAsync(CoreWebView2 core)
    {
        injector = new SkinInjector(core);
        return RefreshAsync();
    }

    /// <summary>La siguiente piel, con la lista recién leída: puede haber una nueva desde la última vez.</summary>
    public Task NextThemeAsync() => SelectAsync(ThemeManager.Next(themes.LoadAll(), Active?.Id)?.Id);

    public Task SelectAsync(string? themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId))
        {
            return Task.CompletedTask;
        }

        Save(session.Select(themeId));
        return RefreshAsync();
    }

    public Task ToggleSafeModeAsync()
    {
        Save(session.ToggleSafeMode());
        return RefreshAsync();
    }

    /// <summary>Dónde abrir la ventana: donde la dejaste, o null para centrarla.</summary>
    public WindowBounds? StartWindow => session.StartWindow;

    /// <summary>Tus ajustes vigentes, con los cambios de esta sesión.</summary>
    public AppSettings Settings => session.Settings;

    public void RememberWindow(WindowBounds? bounds) => Save(session.RememberWindow(bounds));

    /// <summary>
    /// Cambia y guarda el resto de ajustes (tonos, fondo, avisos, bandeja). Con <paramref name="refresh"/>,
    /// vuelve a poner la piel en la página: hace falta si cambia el fondo.
    /// </summary>
    public Task UpdateAsync(Func<AppSettings, AppSettings> change, bool refresh = true)
    {
        Save(session.Update(change));
        if (refresh)
        {
            return RefreshAsync();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    /// <summary>Vuelve a leer las pieles del disco y pone en la página la que toca.</summary>
    public async Task RefreshAsync()
    {
        if (disposed)
        {
            return;
        }

        await busy.WaitAsync();
        try
        {
            if (disposed)
            {
                return;
            }

            Available = themes.LoadAll();
            Active = ThemeManager.Resolve(Available, session.Settings.ThemeId);
            Watch(Active);

            if (injector is not null)
            {
                var wallpaper = WallpaperCss.Build(session.Settings.Wallpaper, wallpapersDir);
                var veil = session.Settings.Veil;
                if (SafeMode)
                {
                    await injector.RemoveAsync();
                }
                else if (Active is null)
                {
                    await injector.ApplyAsync(PageCss.Compose(null, wallpaper, veil));
                }
                else if (await TryBuildAsync(Active) is { } css)
                {
                    await injector.ApplyAsync(PageCss.Compose(css, wallpaper, veil));
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException)
        {
            // El navegador se está cerrando o se ha caído: ya no hay página que vestir.
            Trace.TraceWarning($"Cryptwire: no se ha podido poner la piel en la página ({ex.GetType().Name}).");
        }
        finally
        {
            busy.Release();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        disposed = true;
        watcher?.Dispose();
    }

    /// <summary>
    /// Un editor puede tener el fichero bloqueado justo al guardar: se reintenta un par de veces. Si
    /// aun así no se lee, la página se queda con la piel que tenía: una piel a medio guardar nunca la
    /// deja en blanco.
    /// </summary>
    private static async Task<string?> TryBuildAsync(Theme theme)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var built = ThemeManager.BuildCss(theme);
                foreach (var reference in built.Skipped)
                {
                    Trace.TraceWarning($"Cryptwire: la piel '{theme.Id}' apunta a '{reference}' y no se ha podido incrustar.");
                }

                return built.Css;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == ReadAttempts)
                {
                    Trace.TraceWarning($"Cryptwire: no se ha podido leer la piel '{theme.Id}' ({ex.GetType().Name}); se queda la que había.");
                    return null;
                }

                await Task.Delay(150);
            }
        }
    }

    /// <summary>Recarga en caliente: se vigila la carpeta de la piel activa, y solo esa.</summary>
    private void Watch(Theme? theme)
    {
        if (watcher is not null && string.Equals(watchedDir, theme?.Directory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        watcher?.Dispose();
        watcher = null;
        watchedDir = null;
        if (theme is null)
        {
            return;
        }

        try
        {
            watcher = new ThemeWatcher(theme.Directory, Quiet);
            watcher.Changed += (_, _) => dispatcher.InvokeAsync(RefreshAsync);
            watchedDir = theme.Directory;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            // Sin vigilancia la piel funciona igual; solo no se recarga sola. Se reintenta en la próxima recarga.
            Trace.TraceWarning($"Cryptwire: no se puede vigilar la piel '{theme.Id}' ({ex.GetType().Name}).");
        }
    }

    private void Save(AppSettings next)
    {
        try
        {
            store.Save(next);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // El cambio vale para esta sesión aunque no se haya podido guardar.
            Trace.TraceWarning($"Cryptwire: no se han podido guardar los ajustes ({ex.GetType().Name}).");
        }
    }
}
