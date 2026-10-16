namespace Cryptwire.Core.Navigation;

public enum NavigationDecision
{
    /// <summary>Se abre dentro de la ventana de Cryptwire.</summary>
    Allow,

    /// <summary>Se abre en el navegador del usuario, fuera de la app.</summary>
    OpenExternal,

    /// <summary>No se abre en ningún sitio.</summary>
    Block,
}
