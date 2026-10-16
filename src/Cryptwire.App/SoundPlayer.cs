using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using Cryptwire.Core.Notifications;

namespace Cryptwire.App;

/// <summary>
/// Reproduce tonos con el reproductor de WPF (WAV, MP3, WMA…): uno para los mensajes, que suena una
/// vez, y otro para las llamadas, que se repite hasta que se calla o pasa su tope.
/// </summary>
internal sealed class SoundPlayer : ISoundPlayer
{
    private readonly MediaPlayer player = new() { Volume = 1.0 };
    private readonly MediaPlayer ringer = new() { Volume = 1.0 };
    private readonly DispatcherTimer ringLimit = new();
    private string? ringing;

    public SoundPlayer()
    {
        // Al acabar, vuelve a empezar: así se repite el tono de llamada sin cortes de más.
        ringer.MediaEnded += (_, _) =>
        {
            if (ringing is not null)
            {
                ringer.Position = TimeSpan.Zero;
                ringer.Play();
            }
        };
        ringLimit.Tick += (_, _) => Silence();
    }

    public void Play(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        player.Open(new Uri(path, UriKind.Absolute));
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
        ringer.Open(new Uri(path, UriKind.Absolute));
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

    private void Silence()
    {
        ringLimit.Stop();
        ringing = null;
        ringer.Stop();
        ringer.Close();
    }
}
