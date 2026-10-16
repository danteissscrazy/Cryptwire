using System.Runtime.InteropServices;
using System.Windows;
using Cryptwire.Core.Notifications;
using Cryptwire.Core.Skins;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Cryptwire.App;

/// <summary>
/// El icono de Cryptwire en la bandeja del sistema: con la ventana cerrada sigue ahí, recibiendo avisos.
/// Un clic la trae delante; el menú (clic derecho) da Ajustes, el modo seguro y Salir. Con mensajes sin
/// leer, el icono lleva un punto del color de alerta de la piel y el número va en el texto al pasar el ratón.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly Forms.ContextMenuStrip menu;
    private readonly Forms.ToolStripMenuItem safeModeItem;
    private readonly Drawing.Icon plain;
    private Drawing.Icon? badge;
    private IntPtr badgeHandle;
    private int unread;
    private SkinPalette palette = SkinPalette.Default;

    public TrayIcon()
    {
        using (var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/logo.ico")).Stream)
        {
            plain = new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
        }

        menu = new Forms.ContextMenuStrip { ShowImageMargin = true, Font = new Drawing.Font("Segoe UI", 9f) };
        var open = new Forms.ToolStripMenuItem("Abrir Cryptwire") { Font = new Drawing.Font("Segoe UI", 9f, Drawing.FontStyle.Bold) };
        open.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        var settings = new Forms.ToolStripMenuItem("Ajustes…");
        settings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        safeModeItem = new Forms.ToolStripMenuItem("Modo seguro");
        safeModeItem.Click += (_, _) => SafeModeRequested?.Invoke(this, EventArgs.Empty);
        var exit = new Forms.ToolStripMenuItem("Salir");
        exit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.AddRange([open, new Forms.ToolStripSeparator(), settings, safeModeItem, new Forms.ToolStripSeparator(), exit]);

        icon = new Forms.NotifyIcon { Icon = plain, Text = "Cryptwire", ContextMenuStrip = menu, Visible = true };
        icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                OpenRequested?.Invoke(this, EventArgs.Empty);
            }
        };
        ApplyPalette(palette);
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? SafeModeRequested;

    public event EventHandler? ExitRequested;

    public void Update(bool safeMode, int unreadCount)
    {
        safeModeItem.Checked = safeMode;
        if (unreadCount == unread)
        {
            return;
        }

        unread = unreadCount;
        icon.Text = unread > 0 ? $"Cryptwire · {UnreadCount.Label(unread)} sin leer" : "Cryptwire";
        icon.Icon = unread > 0 ? Badge() : plain;
    }

    /// <summary>El menú con los colores de la piel activa.</summary>
    public void ApplyPalette(SkinPalette next)
    {
        palette = next;
        var colors = new MenuColors(next);
        menu.Renderer = new MenuRenderer(colors);
        menu.BackColor = colors.Panel;
        foreach (Forms.ToolStripItem item in menu.Items)
        {
            item.ForeColor = colors.Text;
        }

        if (badge is not null)
        {
            ReleaseBadge();
            if (unread > 0)
            {
                icon.Icon = Badge();
            }
        }
    }

    public void Dispose()
    {
        icon.Visible = false;
        icon.Dispose();
        menu.Dispose();
        ReleaseBadge();
        plain.Dispose();
    }

    /// <summary>El logo con un punto en la esquina de abajo a la derecha, como el globo de una app de mensajes.</summary>
    private Drawing.Icon Badge()
    {
        if (badge is not null)
        {
            return badge;
        }

        using var bitmap = plain.ToBitmap();
        using (var graphics = Drawing.Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var size = bitmap.Width * 0.5f;
            var x = bitmap.Width - size - 0.5f;
            var y = bitmap.Height - size - 0.5f;
            using var fill = new Drawing.SolidBrush(ToDrawing(palette.Alert));
            using var edge = new Drawing.Pen(ToDrawing(palette.Background), Math.Max(1f, bitmap.Width / 16f));
            graphics.FillEllipse(fill, x, y, size, size);
            graphics.DrawEllipse(edge, x, y, size, size);
        }

        badgeHandle = bitmap.GetHicon();
        badge = Drawing.Icon.FromHandle(badgeHandle);
        return badge;
    }

    private void ReleaseBadge()
    {
        if (badge is null)
        {
            return;
        }

        if (icon.Icon == badge)
        {
            icon.Icon = plain;
        }

        badge.Dispose();
        badge = null;
        DestroyIcon(badgeHandle);
        badgeHandle = IntPtr.Zero;
    }

    private static Drawing.Color ToDrawing(string value)
    {
        var color = PaletteResources.Parse(value);
        return Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);

    private sealed class MenuColors(SkinPalette palette) : Forms.ProfessionalColorTable
    {
        public Drawing.Color Panel { get; } = ToDrawing(palette.Panel);

        public Drawing.Color Text { get; } = ToDrawing(palette.Text);

        public Drawing.Color Accent { get; } = ToDrawing(palette.Accent);

        public Drawing.Color Hover { get; } = Blend(ToDrawing(palette.Glow), ToDrawing(palette.Panel), 0.16);

        public Drawing.Color Line { get; } = Blend(ToDrawing(palette.Text), ToDrawing(palette.Panel), 0.12);

        public override Drawing.Color ToolStripDropDownBackground => Panel;

        public override Drawing.Color ImageMarginGradientBegin => Panel;

        public override Drawing.Color ImageMarginGradientMiddle => Panel;

        public override Drawing.Color ImageMarginGradientEnd => Panel;

        public override Drawing.Color MenuBorder => Blend(Accent, Panel, 0.45);

        public override Drawing.Color MenuItemBorder => Blend(ToDrawing(palette.Glow), Panel, 0.45);

        public override Drawing.Color MenuItemSelected => Hover;

        public override Drawing.Color MenuItemSelectedGradientBegin => Hover;

        public override Drawing.Color MenuItemSelectedGradientEnd => Hover;

        public override Drawing.Color SeparatorDark => Line;

        public override Drawing.Color SeparatorLight => Panel;

        public override Drawing.Color CheckBackground => Blend(Accent, Panel, 0.25);

        public override Drawing.Color CheckSelectedBackground => Blend(Accent, Panel, 0.35);

        public override Drawing.Color CheckPressedBackground => Blend(Accent, Panel, 0.35);

        private static Drawing.Color Blend(Drawing.Color top, Drawing.Color bottom, double amount) => Drawing.Color.FromArgb(
            (int)(bottom.R + ((top.R - bottom.R) * amount)),
            (int)(bottom.G + ((top.G - bottom.G) * amount)),
            (int)(bottom.B + ((top.B - bottom.B) * amount)));
    }

    /// <summary>El dibujo por defecto del menú, con el texto y la marca de los activados en los colores de la piel.</summary>
    private sealed class MenuRenderer(MenuColors colors) : Forms.ToolStripProfessionalRenderer(colors)
    {
        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = colors.Text;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(Forms.ToolStripItemImageRenderEventArgs e)
        {
            var box = e.ImageRectangle;
            using var fill = new Drawing.SolidBrush(colors.CheckBackground);
            e.Graphics.FillRectangle(fill, box);
            using var pen = new Drawing.Pen(colors.Accent, 1.6f);
            e.Graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawLines(pen, new[]
            {
                new Drawing.PointF(box.Left + (box.Width * 0.22f), box.Top + (box.Height * 0.52f)),
                new Drawing.PointF(box.Left + (box.Width * 0.42f), box.Top + (box.Height * 0.72f)),
                new Drawing.PointF(box.Left + (box.Width * 0.78f), box.Top + (box.Height * 0.3f)),
            });
        }
    }
}
