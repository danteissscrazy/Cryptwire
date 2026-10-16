using System.IO;
using System.Windows.Media;
using Cryptwire.Core.Notifications;

namespace Cryptwire.App;

/// <summary>Reproduce tonos con el reproductor de WPF (WAV, MP3, WMA…).</summary>
internal sealed class SoundPlayer : ISoundPlayer
{
    private readonly MediaPlayer player = new() { Volume = 1.0 };

    public void Play(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        player.Open(new Uri(path, UriKind.Absolute));
        player.Play();
    }
}
