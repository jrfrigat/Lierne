namespace Lierne;

/// <summary>Identifies a surface that projects command placements.</summary>
public enum CommandSurface
{
    /// <summary>The main menu bar.</summary>
    Menu,

    /// <summary>The ribbon.</summary>
    Ribbon,

    /// <summary>A toolbar.</summary>
    Toolbar,

    /// <summary>The status bar.</summary>
    StatusBar,

    /// <summary>A context menu.</summary>
    ContextMenu,

    /// <summary>The command palette.</summary>
    Palette
}

/// <summary>Describes where a placement is inserted relative to its neighbours.</summary>
public enum CommandPlacementAnchor
{
    /// <summary>Before the other placements of the group.</summary>
    First,

    /// <summary>After the other placements of the group.</summary>
    Last,

    /// <summary>Before the placement named by <see cref="CommandPlacementDescriptor.RelativeTo"/>.</summary>
    Before,

    /// <summary>After the placement named by <see cref="CommandPlacementDescriptor.RelativeTo"/>.</summary>
    After
}

/// <summary>Describes where one command appears on a surface.</summary>
/// <param name="Command">The placed command.</param>
/// <param name="Surface">The surface that renders the placement.</param>
/// <param name="Group">The stable group id on that surface.</param>
/// <param name="Order">The order inside the group.</param>
/// <param name="Anchor">The insertion rule relative to the group or the relative command.</param>
/// <param name="RelativeTo">The command used by the Before and After anchors.</param>
/// <param name="TitleOverride">A localized title that replaces the command title on this surface.</param>
/// <param name="IconOverride">An icon key that replaces the command icon on this surface.</param>
/// <param name="DescriptionOverride">A localized description that replaces the command description on this surface.</param>
public sealed record CommandPlacementDescriptor(
    CommandId Command,
    CommandSurface Surface,
    string Group,
    int Order = 0,
    CommandPlacementAnchor Anchor = CommandPlacementAnchor.Last,
    CommandId? RelativeTo = null,
    StringKey? TitleOverride = null,
    string? IconOverride = null,
    StringKey? DescriptionOverride = null);

/// <summary>Describes one command as a surface should present it.</summary>
/// <param name="Command">The command.</param>
/// <param name="Title">The localized title.</param>
/// <param name="Description">The localized description, when any.</param>
/// <param name="IconKey">The icon key, when any.</param>
/// <param name="Enabled">Whether the command can run.</param>
/// <param name="Visible">Whether the surface should show the command.</param>
/// <param name="Checked">Whether the command is checked.</param>
/// <param name="ShortcutText">The effective shortcut text, when any.</param>
/// <param name="Properties">Additional surface-specific values.</param>
public sealed record CommandPresentation(
    CommandId Command,
    StringKey Title,
    StringKey? Description = null,
    string? IconKey = null,
    bool Enabled = true,
    bool Visible = true,
    bool Checked = false,
    string? ShortcutText = null,
    IReadOnlyDictionary<string, object?>? Properties = null);

/// <summary>Computes the live presentation and availability of a command.</summary>
public interface ICommandUpdateHandler
{
    /// <summary>Computes the presentation for the current context.</summary>
    /// <param name="context">The scoped services, shared state and typed argument.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The presentation of the command.</returns>
    ValueTask<CommandPresentation> UpdateAsync(
        LierneCommandContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>Owns command presentations and command-palette search.</summary>
public interface ICommandPresentationService
{
    /// <summary>Raised after a cached presentation is invalidated or updated.</summary>
    event Action<CommandId>? Changed;

    /// <summary>Gets the last known presentation, computing a default when none is cached.</summary>
    /// <param name="command">The command.</param>
    /// <returns>The presentation.</returns>
    CommandPresentation GetPresentation(CommandId command);

    /// <summary>Computes and caches the presentation with its update handler.</summary>
    /// <param name="command">The command.</param>
    /// <param name="argument">The typed argument, when the surface has one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The computed presentation.</returns>
    ValueTask<CommandPresentation> UpdateAsync(
        CommandId command,
        object? argument = null,
        CancellationToken cancellationToken = default);

    /// <summary>Searches commands by id, title, description and keywords.</summary>
    /// <param name="query">The search text; an empty query returns every command.</param>
    /// <returns>The matching command descriptors in registration order.</returns>
    IReadOnlyList<CommandDescriptor> Search(string query);

    /// <summary>Drops the cached presentation of one command or of every command.</summary>
    /// <param name="command">The command to drop, or <see langword="null"/> for every command.</param>
    void Invalidate(CommandId? command = null);
}

/// <summary>Owns the command placements contributed by the plugins.</summary>
public interface ICommandPlacementService
{
    /// <summary>Gets every placement in plugin order.</summary>
    IReadOnlyList<CommandPlacementDescriptor> All { get; }

    /// <summary>Gets the placements of one surface ordered by order and command id.</summary>
    /// <param name="surface">The surface.</param>
    /// <returns>The placements of the surface.</returns>
    IReadOnlyList<CommandPlacementDescriptor> ForSurface(CommandSurface surface);
}
