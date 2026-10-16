using System.Security.Principal;

namespace Cryptwire.App;

/// <summary>
/// Una sola Cryptwire por usuario: dos a la vez abrirían WhatsApp dos veces con la misma sesión, y la web
/// echaría a una de las dos. Si ya hay una abierta (en la bandeja, por ejemplo), la segunda le pide que se
/// enseñe, o que se cierre con <c>--salir</c>, y termina.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle showSignal;
    private readonly EventWaitHandle exitSignal;
    private readonly bool owned;
    private readonly List<RegisteredWaitHandle> waiting = [];

    public SingleInstance()
    {
        // Por usuario y por sesión de Windows (Local\): otro usuario del mismo PC tiene la suya.
        var name = $@"Local\Cryptwire-{WindowsIdentity.GetCurrent().User?.Value ?? "usuario"}";
        mutex = new Mutex(initiallyOwned: true, name, out owned);
        showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, name + "-mostrar");
        exitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, name + "-salir");
    }

    /// <summary>Esta es la primera: la que se queda.</summary>
    public bool IsFirst => owned;

    /// <summary>Desde la segunda: pide a la primera que se enseñe.</summary>
    public void AskFirstToShow() => showSignal.Set();

    /// <summary>Desde la segunda: pide a la primera que se cierre del todo.</summary>
    public void AskFirstToExit() => exitSignal.Set();

    /// <summary>En la primera: qué hacer cuando otra pide que se enseñe o que salga (fuera del hilo de la ventana).</summary>
    public void Listen(Action show, Action exit)
    {
        waiting.Add(ThreadPool.RegisterWaitForSingleObject(showSignal, (_, _) => show(), null, Timeout.Infinite, executeOnlyOnce: false));
        waiting.Add(ThreadPool.RegisterWaitForSingleObject(exitSignal, (_, _) => exit(), null, Timeout.Infinite, executeOnlyOnce: false));
    }

    public void Dispose()
    {
        foreach (var wait in waiting)
        {
            wait.Unregister(null);
        }

        if (owned)
        {
            mutex.ReleaseMutex();
        }

        mutex.Dispose();
        showSignal.Dispose();
        exitSignal.Dispose();
    }
}
