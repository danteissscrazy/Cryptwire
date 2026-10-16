using Cryptwire.Core.Startup;
using Microsoft.Win32;

namespace Cryptwire.App;

/// <summary>
/// Arrancar con Windows: un valor en la clave <c>Run</c> de tu usuario, sin permisos de administrador. Si
/// lo desactivas desde el Administrador de tareas, Windows lo apunta aparte (<c>StartupApproved</c>) y aquí
/// también cuenta como desactivado; activarlo desde Cryptwire quita esa marca, porque es lo que pides.
/// </summary>
internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private static string Executable => Environment.ProcessPath ?? throw new InvalidOperationException("No sé dónde está el ejecutable de Cryptwire.");

    public static bool IsEnabled()
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunKey);
        return StartupCommand.PointsTo(run?.GetValue(StartupCommand.ValueName) as string, Executable) && !DisabledInTaskManager();
    }

    public static void Enable()
    {
        using (var run = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            run.SetValue(StartupCommand.ValueName, StartupCommand.Build(Executable), RegistryValueKind.String);
        }

        ForgetTaskManagerMark();
    }

    public static void Disable()
    {
        using (var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
        {
            run?.DeleteValue(StartupCommand.ValueName, throwOnMissingValue: false);
        }

        ForgetTaskManagerMark();
    }

    /// <summary>El Administrador de tareas guarda 12 bytes; el primero impar significa desactivado.</summary>
    private static bool DisabledInTaskManager()
    {
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
        return approved?.GetValue(StartupCommand.ValueName) is byte[] { Length: > 0 } state && (state[0] & 1) == 1;
    }

    private static void ForgetTaskManagerMark()
    {
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
        approved?.DeleteValue(StartupCommand.ValueName, throwOnMissingValue: false);
    }
}
