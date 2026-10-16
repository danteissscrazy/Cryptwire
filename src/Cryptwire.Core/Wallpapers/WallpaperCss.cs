using System.Globalization;
using Cryptwire.Core.Settings;

namespace Cryptwire.Core.Wallpapers;

/// <summary>
/// El CSS que pone tu fondo detrás de la conversación, por encima del de la piel. La imagen va
/// incrustada (la web no admite imágenes de otro sitio) y oscurecida con el color de noche de la piel,
/// para que los mensajes se lean encima. Solo se incrustan imágenes de verdad (JPEG, PNG o WebP) de la
/// carpeta de fondos de Cryptwire: un <c>settings.json</c> retocado no puede meter otro fichero en la página.
/// </summary>
public static class WallpaperCss
{
    /// <summary>Tope de la imagen ya importada. Al importarla se reduce y se recomprime muy por debajo.</summary>
    public const long MaxBytes = 8 * 1024 * 1024;

    /// <returns>El CSS, o null si no hay fondo o la imagen no vale.</returns>
    public static string? Build(WallpaperSettings? wallpaper, string wallpapersDir)
    {
        if (wallpaper is null || ContainedFiles.TryRead(wallpapersDir, wallpaper.File, MaxBytes) is not { } bytes
            || ImageMime(bytes) is not { } mime)
        {
            return null;
        }

        var dim = wallpaper.Clamped().Dim.ToString(CultureInfo.InvariantCulture);
        return $$"""

            /* Cryptwire · tu fondo */
            body #main {
              background:
                linear-gradient(color-mix(in srgb, var(--cw-bg-chrome, #02040a) {{dim}}%, transparent) 0 0),
                url("data:{{mime}};base64,{{Convert.ToBase64String(bytes)}}") center / cover no-repeat,
                var(--cw-bg-chat, #050912) !important;
            }

            body #main [data-testid^="conversation-background"] {
              opacity: 0 !important;
            }

            """;
    }

    /// <summary>El tipo de imagen por sus primeros bytes, no por la extensión. Null si no es JPEG, PNG ni WebP.</summary>
    public static string? ImageMime(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "image/png";
        }

        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }
}
