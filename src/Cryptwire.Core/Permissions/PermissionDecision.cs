namespace Cryptwire.Core.Permissions;

public enum PermissionDecision
{
    /// <summary>Se concede sin preguntar.</summary>
    Allow,

    /// <summary>Se deniega sin preguntar.</summary>
    Deny,

    /// <summary>Lo decides tú en el diálogo de WebView2.</summary>
    AskUser,
}
