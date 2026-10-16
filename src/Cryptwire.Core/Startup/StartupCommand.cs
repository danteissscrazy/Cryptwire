namespace Cryptwire.Core.Startup;

/// <summary>
/// La orden que Windows ejecuta al iniciar sesión si eliges arrancar Cryptwire con Windows. Va en el
/// registro de tu usuario (<c>HKCU\...\Run</c>), sin permisos de administrador, y arranca directamente en
/// la bandeja.
/// </summary>
public static class StartupCommand
{
    /// <summary>El nombre del valor en el registro.</summary>
    public const string ValueName = "Cryptwire";

    /// <summary>Arranca sin enseñar la ventana: solo el icono de la bandeja.</summary>
    public const string TrayArgument = "--bandeja";

    /// <summary>La orden para el registro: la ruta entre comillas (puede llevar espacios) y el argumento.</summary>
    public static string Build(string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        if (!Path.IsPathFullyQualified(executable) || executable.Contains('"', StringComparison.Ordinal))
        {
            throw new ArgumentException($"La ruta del ejecutable tiene que ser absoluta y sin comillas, y ha llegado '{executable}'.", nameof(executable));
        }

        return $"\"{executable}\" {TrayArgument}";
    }

    /// <summary>
    /// Si la orden guardada arranca este ejecutable. Si apunta a otro (una copia vieja que moviste), no
    /// cuenta como activado: al activarlo se reescribe con la ruta buena.
    /// </summary>
    public static bool PointsTo(string? command, string executable)
    {
        if (string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(executable))
        {
            return false;
        }

        var text = command.Trim();
        string path;
        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            if (end < 0)
            {
                return false;
            }

            path = text[1..end];
        }
        else
        {
            var space = text.IndexOf(' ', StringComparison.Ordinal);
            path = space < 0 ? text : text[..space];
        }

        try
        {
            return Path.IsPathFullyQualified(path)
                && string.Equals(Path.GetFullPath(path), Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
