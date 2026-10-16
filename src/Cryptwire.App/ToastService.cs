using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Cryptwire.Core.Notifications;

namespace Cryptwire.App;

/// <summary>
/// Fase 1: avisa haciendo parpadear el botón de Cryptwire en la barra de tareas hasta que vuelves a
/// la ventana. El aviso de Windows con la estética de Cryptwire llega en la Fase 3.
/// </summary>
internal sealed class ToastService(Window window) : IToastService
{
    private const uint FlashTray = 0x2;
    private const uint FlashUntilForeground = 0xC;

    public void Show(IncomingNotification notification)
    {
        if (window.IsActive)
        {
            return;
        }

        var info = new FlashWindowInfo
        {
            Size = (uint)Marshal.SizeOf<FlashWindowInfo>(),
            Window = new WindowInteropHelper(window).Handle,
            Flags = FlashTray | FlashUntilForeground,
        };
        FlashWindowEx(ref info);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashWindowInfo
    {
        public uint Size;
        public IntPtr Window;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FlashWindowInfo info);
}
