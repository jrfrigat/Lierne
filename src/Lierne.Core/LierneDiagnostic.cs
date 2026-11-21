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
/// <param name="Plugin">The plugin the diagnostic belongs to, when known.</param>
/// <param name="Cause">The underlying reason or a link to the causing diagnostic, when known.</param>
public sealed record LierneDiagnostic(
    LierneDiagnosticSeverity Severity,
    string Code,
    string Message,
    PluginId? Plugin = null,
    string? Cause = null)
{
    /// <summary>Creates an error diagnostic.</summary>
    /// <param name="code">The stable diagnostic code.</param>
    /// <param name="message">The human-readable message.</param>
    /// <param name="plugin">The plugin the diagnostic belongs to, when known.</param>
    /// <param name="cause">The underlying reason, when known.</param>
    /// <returns>The diagnostic.</returns>
    public static LierneDiagnostic Error(
        string code,
        string message,
        PluginId? plugin = null,
        string? cause = null) =>
        new(LierneDiagnosticSeverity.Error, code, message, plugin, cause);

    /// <summary>Creates a warning diagnostic.</summary>
    /// <param name="code">The stable diagnostic code.</param>
    /// <param name="message">The human-readable message.</param>
    /// <param name="plugin">The plugin the diagnostic belongs to, when known.</param>
    /// <param name="cause">The underlying reason, when known.</param>
    /// <returns>The diagnostic.</returns>
    public static LierneDiagnostic Warning(
        string code,
        string message,
        PluginId? plugin = null,
        string? cause = null) =>
        new(LierneDiagnosticSeverity.Warning, code, message, plugin, cause);
}

/// <summary>Identifies one composed plugin and its position in the composition order.</summary>
/// <param name="Id">The plugin id.</param>
/// <param name="DisplayName">The localized plugin name.</param>
/// <param name="Order">The zero-based position after dependency ordering.</param>
/// <param name="Version">The plugin version, when it declares one.</param>
/// <param name="Essential">Whether the application cannot start without the plugin.</param>
/// <param name="OptionalDependsOn">The optional dependencies that were ordered before the plugin.</param>
public sealed record LiernePluginInfo(
    PluginId Id,
    StringKey DisplayName,
    int Order,
    Version? Version = null,
    bool Essential = false,
    IReadOnlyList<PluginId>? OptionalDependsOn = null);
