using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Cryptwire.Core.Skins;

namespace Cryptwire.App;

/// <summary>
/// Añade al menú de la ventana (clic derecho en la barra de título, o Alt+Espacio) la lista de pieles,
/// el modo seguro y los ajustes. Es un menú del propio Windows: funciona aunque la piel haya roto la página.
/// </summary>
internal sealed class SystemMenu
{
    private const int WmSysCommand = 0x0112;
    private const uint MfString = 0x0;
    private const uint MfChecked = 0x8;
    private const uint MfPopup = 0x10;
    private const uint MfSeparator = 0x800;
    private const uint RdwInvalidate = 0x1;
    private const uint RdwErase = 0x4;
    private const uint RdwAllChildren = 0x80;
    private const uint RdwFrame = 0x400;

    // Windows reserva los 4 bits bajos de cada orden y las órdenes desde 0xF000 para las suyas.
    private const int SafeModeCommand = 0x1000;
    private const int SettingsCommand = 0x1020;
    private const int FirstThemeCommand = 0x1100;
    private const int CommandStep = 0x10;
    private const int MaxThemes = 64;

    private readonly IntPtr window;
    private IReadOnlyList<Theme> themes = [];
    private string? lastShown;

    public SystemMenu(Window owner)
    {
        window = new WindowInteropHelper(owner).EnsureHandle();
        HwndSource.FromHwnd(window).AddHook(OnMessage);
    }

    public event EventHandler? SafeModeClicked;

    public event EventHandler<string>? ThemeClicked;

    public event EventHandler? SettingsClicked;

    /// <summary>
    /// Rehace los elementos de Cryptwire con el estado actual. Si nada de lo que enseña ha cambiado (una
    /// recarga en caliente del CSS, por ejemplo), no lo toca: así no se destruye un menú que esté abierto.
    /// </summary>
    public void Update(IReadOnlyList<Theme> available, Theme? active, bool safeMode)
    {
        var shown = string.Join("\n", available.Take(MaxThemes).Select(t => t.Id + "\t" + t.Name)) + $"\n{active?.Id}\n{safeMode}";
        if (shown == lastShown)
        {
            return;
        }

        lastShown = shown;
        themes = [.. available.Take(MaxThemes)];

        // Vuelve al menú de Windows de serie y añade lo nuestro encima, sin ir quitando elementos uno a uno.
        GetSystemMenu(window, revert: true);
        var menu = GetSystemMenu(window, revert: false);
        if (menu == IntPtr.Zero)
        {
            return;
        }

        var skins = CreatePopupMenu();
        for (var i = 0; i < themes.Count; i++)
        {
            var isActive = !safeMode && themes[i].Id == active?.Id;
            AppendMenu(skins, MfString | (isActive ? MfChecked : 0), (nuint)(FirstThemeCommand + i * CommandStep), themes[i].Name.Replace("&", "&&", StringComparison.Ordinal));
        }

        AppendMenu(menu, MfSeparator, 0, null);
        AppendMenu(menu, MfPopup, (nuint)skins, "Piel\tF7");
        AppendMenu(menu, MfString | (safeMode ? MfChecked : 0), SafeModeCommand, "Modo seguro\tF8");
        AppendMenu(menu, MfString, SettingsCommand, "Ajustes de Cryptwire…");

        // Al cambiar el menú, Windows repinta sus botones de la barra clásica encima de la nuestra (que,
        // con WindowChrome, es zona de la ventana): se repinta la ventana entera para borrarlos.
        RedrawWindow(window, IntPtr.Zero, IntPtr.Zero, RdwInvalidate | RdwFrame | RdwErase | RdwAllChildren);
    }

    private IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmSysCommand)
        {
            return IntPtr.Zero;
        }

        var command = (int)(wParam.ToInt64() & 0xFFF0);
        if (command == SafeModeCommand)
        {
            handled = true;
            SafeModeClicked?.Invoke(this, EventArgs.Empty);
        }
        else if (command == SettingsCommand)
        {
            handled = true;
            SettingsClicked?.Invoke(this, EventArgs.Empty);
        }
        else if (command >= FirstThemeCommand && command < FirstThemeCommand + themes.Count * CommandStep)
        {
            handled = true;
            ThemeClicked?.Invoke(this, themes[(command - FirstThemeCommand) / CommandStep].Id);
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetSystemMenu(IntPtr window, [MarshalAs(UnmanagedType.Bool)] bool revert);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RedrawWindow(IntPtr window, IntPtr updateRect, IntPtr updateRegion, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, nuint idOrSubmenu, string? text);
}
