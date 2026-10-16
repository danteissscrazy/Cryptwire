using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Cryptwire.Core.Settings;

namespace Cryptwire.App;

/// <summary>
/// Lee y aplica la posición de la ventana como la guarda Windows (<c>GetWindowPlacement</c>): el
/// rectángulo sin maximizar más si estaba maximizada. <c>SetWindowPlacement</c> se encarga de que una
/// ventana guardada en un monitor que ya no está vuelva a una pantalla visible.
/// </summary>
internal static class WindowPlacement
{
    private const int ShowHide = 0;
    private const int ShowMinimized = 2;
    private const int ShowMaximized = 3;
    private const int RestoreToMaximized = 0x2;

    public static WindowBounds? Read(Window window)
    {
        var placement = new Placement { Length = Marshal.SizeOf<Placement>() };
        if (!GetWindowPlacement(new WindowInteropHelper(window).Handle, ref placement))
        {
            return null;
        }

        var maximized = placement.ShowCmd == ShowMaximized
            || (placement.ShowCmd == ShowMinimized && (placement.Flags & RestoreToMaximized) != 0);
        var r = placement.NormalPosition;
        return new WindowBounds(r.Left, r.Top, r.Right, r.Bottom, maximized);
    }

    /// <summary>
    /// Se llama con la ventana creada pero aún oculta. Solo fija el rectángulo (sin enseñarla); el
    /// maximizado lo pone WPF al mostrarla, para que su estado y el de Windows no se pisen.
    /// </summary>
    public static void Apply(Window window, WindowBounds bounds)
    {
        var placement = new Placement
        {
            Length = Marshal.SizeOf<Placement>(),
            ShowCmd = ShowHide,
            NormalPosition = new Rect { Left = bounds.Left, Top = bounds.Top, Right = bounds.Right, Bottom = bounds.Bottom },
        };
        SetWindowPlacement(new WindowInteropHelper(window).Handle, ref placement);
        if (bounds.Maximized)
        {
            window.WindowState = WindowState.Maximized;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Placement
    {
        public int Length;
        public int Flags;
        public int ShowCmd;
        public Point MinPosition;
        public Point MaxPosition;
        public Rect NormalPosition;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowPlacement(IntPtr window, ref Placement placement);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPlacement(IntPtr window, ref Placement placement);
}
