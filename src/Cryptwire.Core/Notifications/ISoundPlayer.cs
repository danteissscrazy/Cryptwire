namespace Cryptwire.Core.Notifications;

public interface ISoundPlayer
{
    /// <summary>Reproduce el fichero de sonido. Si no existe o no se puede leer, no suena nada.</summary>
    void Play(string path);
}
