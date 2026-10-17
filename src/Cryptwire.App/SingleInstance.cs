using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Cryptwire.App;

/// <summary>
/// Una sola Cryptwire por usuario: dos a la vez abrirían WhatsApp dos veces con la misma sesión, y la web
/// echaría a una de las dos. Si ya hay una abierta (en la bandeja, por ejemplo), la segunda le pide que se
/// enseñe, o que se cierre con <c>--salir</c>, y termina.
/// </summary>
/// <remarks>
/// El candado y los dos avisos solo los puede abrir tu usuario. Si la primera se encuentra los avisos ya
/// creados, los ha creado otro proceso para darle órdenes: no los escucha.
/// </remarks>
internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle showSignal;
    private readonly EventWaitHandle exitSignal;
    private readonly bool owned;
    private readonly bool signalsAreOurs;
    private readonly List<RegisteredWaitHandle> waiting = [];

    // Por usuario y por sesión de Windows (Local\): otro usuario del mismo PC tiene la suya.
    public SingleInstance()
        : this($@"Local\Cryptwire-{WindowsIdentity.GetCurrent().User?.Value ?? "usuario"}")
    {
    }

    internal SingleInstance(string name)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new InvalidOperationException("Sin usuario de Windows.");

        var mutexSecurity = new MutexSecurity();
        mutexSecurity.AddAccessRule(new MutexAccessRule(user, MutexRights.FullControl, AccessControlType.Allow));
        var signalSecurity = new EventWaitHandleSecurity();
        signalSecurity.AddAccessRule(new EventWaitHandleAccessRule(user, EventWaitHandleRights.FullControl, AccessControlType.Allow));

        mutex = MutexAcl.Create(initiallyOwned: true, name, out owned, mutexSecurity);
        showSignal = EventWaitHandleAcl.Create(false, EventResetMode.AutoReset, name + "-mostrar", out var showIsNew, signalSecurity);
        exitSignal = EventWaitHandleAcl.Create(false, EventResetMode.AutoReset, name + "-salir", out var exitIsNew, signalSecurity);
        signalsAreOurs = showIsNew && exitIsNew;
    }

    /// <summary>Esta es la primera: la que se queda.</summary>
    public bool IsFirst => owned;

    /// <summary>
    /// Desde la segunda: pide a la primera que se enseñe. Windows solo deja ponerse delante a quien acaba
    /// de recibir tu clic: esta, que es la que has abierto, le cede ese permiso a la primera (y solo a
    /// ella: a las Cryptwire de tu sesión) antes de avisarla.
    /// </summary>
    public void AskFirstToShow()
    {
        using var self = Process.GetCurrentProcess();
        foreach (var other in Process.GetProcessesByName(self.ProcessName))
        {
            using (other)
            {
                if (other.Id != self.Id && other.SessionId == self.SessionId)
                {
                    AllowSetForegroundWindow(other.Id);
                }
            }
        }

        showSignal.Set();
    }

    /// <summary>Desde la segunda: pide a la primera que se cierre del todo.</summary>
    public void AskFirstToExit() => exitSignal.Set();

    /// <summary>
    /// En la primera: qué hacer cuando otra pide que se enseñe o que salga (fuera del hilo de la ventana). Si
    /// los avisos no los ha creado ella, no se escuchan: volver a abrir Cryptwire no la traerá delante.
    /// </summary>
    public void Listen(Action show, Action exit)
    {
        if (!signalsAreOurs)
        {
            return;
        }

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

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);
}
