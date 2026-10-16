namespace Cryptwire.Core.Notifications;

public interface ISoundPlayer
{
    /// <summary>Reproduce el fichero de sonido una vez. Si no existe o no se puede leer, no suena nada.</summary>
    void Play(string path);

    /// <summary>
    /// Repite el tono de llamada hasta <see cref="StopRinging"/> con la misma clave o hasta que pase
    /// <paramref name="max"/>. Una llamada nueva sustituye a la que sonaba.
    /// </summary>
    void Ring(string key, string path, TimeSpan max);

    /// <summary>Calla el tono de llamada, si es el de esa clave. Si suena otro, lo deja.</summary>
    void StopRinging(string key);
}
