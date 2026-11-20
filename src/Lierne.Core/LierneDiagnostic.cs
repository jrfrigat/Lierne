namespace Lierne;

/// <summary>Describes how severe a composition diagnostic is.</summary>
public enum LierneDiagnosticSeverity
{
    /// <summary>The composition can continue, but the result is suspicious.</summary>
    Warning,

    /// <summary>The composition is invalid and must not be started.</summary>
    Error
}

/// <summary>Describes one problem found while composing the plugins.</summary>
/// <param name="Severity">Whether the problem blocks the composition.</param>
/// <param name="Code">The stable diagnostic code, for example <c>lattice.plugin.duplicate</c>.</param>
/// <param name="Message">The human-readable message, including the plugin and contribution ids.</param>
public sealed record LierneDiagnostic(
    LierneDiagnosticSeverity Severity,
    string Code,
    string Message)
{
    /// <summary>Creates an error diagnostic.</summary>
    /// <param name="code">The stable diagnostic code.</param>
    /// <param name="message">The human-readable message.</param>
    /// <returns>The diagnostic.</returns>
    public static LierneDiagnostic Error(string code, string message) =>
        new(LierneDiagnosticSeverity.Error, code, message);

    /// <summary>Creates a warning diagnostic.</summary>
    /// <param name="code">The stable diagnostic code.</param>
    /// <param name="message">The human-readable message.</param>
    /// <returns>The diagnostic.</returns>
    public static LierneDiagnostic Warning(string code, string message) =>
        new(LierneDiagnosticSeverity.Warning, code, message);
}

/// <summary>Identifies one composed plugin and its position in the composition order.</summary>
/// <param name="Id">The plugin id.</param>
/// <param name="DisplayName">The localized plugin name.</param>
/// <param name="Order">The zero-based position after dependency ordering.</param>
public sealed record LiernePluginInfo(PluginId Id, StringKey DisplayName, int Order);
