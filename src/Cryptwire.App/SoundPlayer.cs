using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using Cryptwire.Core.Notifications;

namespace Cryptwire.App;

/// <summary>
/// Reproduce tonos con el reproductor de WPF (WAV, MP3, WMA…): uno para los mensajes, que suena una
/// vez, y otro para las llamadas, que se repite hasta que se calla o pasa su tope. Al acabar suelta el
/// fichero, para que puedas editar el tono de una piel con la app abierta.
/// </summary>
internal sealed class SoundPlayer : ISoundPlayer
{
    private readonly MediaPlayer player = new() { Volume = 1.0 };
    private readonly MediaPlayer ringer = new() { Volume = 1.0 };
    private readonly DispatcherTimer ringLimit = new();
    private string? ringing;

    public SoundPlayer()
    {
        player.MediaEnded += (_, _) => player.Close();
        player.MediaFailed += (_, _) => player.Close();

        // Al acabar, vuelve a empezar: así se repite el tono de llamada sin cortes de más.
        ringer.MediaEnded += (_, _) =>
        {
            if (ringing is not null)
            {
                ringer.Position = TimeSpan.Zero;
                ringer.Play();
            }
        };
        ringer.MediaFailed += (_, _) => Silence();
        ringLimit.Tick += (_, _) => Silence();
    }

    public void Play(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        player.Open(FileUri(path));
        player.Play();
    }

    public void Ring(string key, string path, TimeSpan max)
    {
        Silence();
        if (!File.Exists(path))
        {
            return;
        }

        ringing = key;
        ringer.Open(FileUri(path));
        ringer.Play();
        ringLimit.Interval = max;
        ringLimit.Start();
    }

    public void StopRinging(string key)
    {
        if (ringing == key)
        {
            Silence();
        }
    }

    /// <summary>
    /// La dirección <c>file:</c> del fichero, escrita a mano: <c>new Uri(ruta)</c> leería <c>%41</c> como
    /// «A» y cortaría en <c>#</c>, y el tono no sonaría.
    /// </summary>
    private static Uri FileUri(string path)
    {
        var escaped = path.Replace("%", "%25", StringComparison.Ordinal).Replace("#", "%23", StringComparison.Ordinal).Replace('\\', '/');
        return new Uri(path.StartsWith(@"\\", StringComparison.Ordinal) ? "file:" + escaped : "file:///" + escaped);
    }

    private void Silence()
    {
        ringLimit.Stop();
        ringing = null;
        ringer.Stop();
        ringer.Close();
    }
}
