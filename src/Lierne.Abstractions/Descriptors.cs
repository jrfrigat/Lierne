namespace Lierne;

/// <summary>Describes where a document opens by default.</summary>
public enum LierneDockLocation
{
    /// <summary>The central document area.</summary>
    Main,

    /// <summary>The left document dock.</summary>
    Left,

    /// <summary>The right document dock.</summary>
    Right
}

/// <summary>Describes the default region of a tool window.</summary>
public enum LierneToolRegion
{
    /// <summary>The left tool region.</summary>
    Left,

    /// <summary>The right tool region.</summary>
    Right,

    /// <summary>The bottom tool region.</summary>
    Bottom
}

/// <summary>Describes the editor a setting uses.</summary>
public enum LierneSettingKind
{
    /// <summary>A free-text field.</summary>
    Text,

    /// <summary>An integer field.</summary>
    Integer,

    /// <summary>A Boolean checkbox.</summary>
    Boolean,

    /// <summary>A fixed list of choices.</summary>
    Select
}

/// <summary>Describes a document type contributed by a plugin.</summary>
/// <param name="Id">The stable document type id.</param>
/// <param name="Title">The default title of a document instance.</param>
/// <param name="IconKey">The host icon key, or <see langword="null"/> for the default icon.</param>
/// <param name="Component">The component type that renders the document.</param>
/// <param name="Persistent">Whether instances of this type are stored in the workspace snapshot.</param>
/// <param name="DefaultLocation">Where a new instance opens by default.</param>
public sealed record DocumentDescriptor(
    DocumentTypeId Id,
    StringKey Title,
    string? IconKey,
    Type Component,
    bool Persistent = true,
    LierneDockLocation DefaultLocation = LierneDockLocation.Main)
{
    /// <summary>
    /// Gets or sets the payload codec. With no codec, only documents with a null payload can be restored.
    /// </summary>
    public LierneDocumentStateCodec? StateCodec { get; init; }
}

/// <summary>
/// Describes one view that a plugin contributes to a document type declared by any plugin. The host
/// shows the views as tabs of the document and only needs the ids to associate them.
/// </summary>
/// <param name="DocumentType">The document type whose instances show the view.</param>
/// <param name="Id">
/// The stable view id, unique among the views of the same document type.
/// </param>
/// <param name="Title">The localized view title.</param>
/// <param name="IconKey">The host icon key, or <see langword="null"/> for the default icon.</param>
/// <param name="Component">The component type that renders the view.</param>
/// <param name="Order">The order of the view among the views of its document type.</param>
public sealed record DocumentViewDescriptor(
    DocumentTypeId DocumentType,
    DocumentViewId Id,
    StringKey Title,
    string? IconKey,
    Type Component,
    int Order = 0);

/// <summary>Describes a tool window contributed by a plugin.</summary>
/// <param name="Id">The stable tool window id.</param>
/// <param name="Title">The panel title.</param>
/// <param name="IconKey">The host icon key, or <see langword="null"/> for the default icon.</param>
/// <param name="Component">The component type that renders the panel.</param>
/// <param name="DefaultRegion">The region the panel opens in by default.</param>
/// <param name="DefaultVisible">Whether the panel is visible on a new workspace.</param>
public sealed record ToolWindowDescriptor(
    ToolWindowId Id,
    StringKey Title,
    string? IconKey,
    Type Component,
    LierneToolRegion DefaultRegion = LierneToolRegion.Left,
    bool DefaultVisible = true);

/// <summary>Describes a shell surface a plugin can replace.</summary>
public enum LierneShellSlot
{
    /// <summary>The menu bar above the command area.</summary>
    MenuBar,

    /// <summary>The content shown when no document is active.</summary>
    StartPage,

    /// <summary>The header of the bottom panel.</summary>
    BottomHeader,

    /// <summary>The content of the bottom panel.</summary>
    BottomPanel,

    /// <summary>The status bar.</summary>
    StatusBar
}

/// <summary>Describes a component that replaces one surface of the shell.</summary>
/// <param name="Id">The stable contribution id.</param>
/// <param name="Slot">The surface the component replaces.</param>
/// <param name="Component">
/// The component that renders the surface. It owns its chrome and talks to the application services
/// directly; Lierne only mounts it in the slot.
/// </param>
/// <param name="Order">
/// The order among contributions of the same slot. The lowest order wins; ties go to the id. The
/// built-in surface is the fallback when a slot has no contribution.
/// </param>
public sealed record ShellContentDescriptor(
    ShellContentId Id,
    LierneShellSlot Slot,
    Type Component,
    int Order = 0);

/// <summary>Describes a command contributed by a plugin.</summary>
/// <param name="Id">The stable command id.</param>
/// <param name="Title">The localized command title.</param>
/// <param name="IconKey">The host icon key, or <see langword="null"/> when the command has no icon.</param>
/// <param name="DefaultShortcut">The default keyboard shortcut, or <see langword="null"/> for none.</param>
/// <param name="ArgumentType">
/// The typed argument the command accepts, or <see langword="null"/> when it takes no argument.
/// </param>
/// <param name="Description">The localized description shown in menus and the command palette.</param>
/// <param name="Keywords">Extra search terms for the command palette.</param>
public sealed record CommandDescriptor(
    CommandId Id,
    StringKey Title,
    string? IconKey = null,
    string? DefaultShortcut = null,
    Type? ArgumentType = null,
    StringKey? Description = null,
    IReadOnlyList<string>? Keywords = null);

/// <summary>Describes where a status-bar item is placed.</summary>
public enum LierneStatusBarAlignment
{
    /// <summary>The left status area.</summary>
    Left,

    /// <summary>The center status area.</summary>
    Center,

    /// <summary>The right status area.</summary>
    Right
}

/// <summary>Describes a toolbar contributed by a plugin.</summary>
/// <param name="Id">The stable toolbar id.</param>
/// <param name="Component">
/// The component that renders the toolbar. It owns its toolbar chrome, for example a Flare toolbar.
/// </param>
/// <param name="Order">The order of the toolbar inside the host command area.</param>
public sealed record ToolbarDescriptor(
    ToolbarId Id,
    Type Component,
    int Order = 0);

/// <summary>Describes one status-bar item contributed by a plugin.</summary>
/// <param name="Id">The stable item id.</param>
/// <param name="Title">The localized item text or accessible name.</param>
/// <param name="Alignment">The status area that hosts the item.</param>
/// <param name="Order">The order of the item inside its area.</param>
/// <param name="IconKey">The host icon key, or <see langword="null"/> when the command icon is used.</param>
/// <param name="Command">The command invoked by clicking the item, or <see langword="null"/>.</param>
/// <param name="Component">
/// A custom component that renders the item, or <see langword="null"/> for the generated item.
/// </param>
public sealed record StatusBarItemDescriptor(
    StatusBarItemId Id,
    StringKey Title,
    LierneStatusBarAlignment Alignment = LierneStatusBarAlignment.Left,
    int Order = 0,
    string? IconKey = null,
    CommandId? Command = null,
    Type? Component = null);

/// <summary>Describes a ribbon group contributed by a plugin.</summary>
/// <param name="Id">The stable group id.</param>
/// <param name="Title">The localized group title.</param>
/// <param name="Order">The order of the group inside its ribbon.</param>
public sealed record RibbonGroupDescriptor(
    RibbonGroupId Id,
    StringKey Title,
    int Order = 0);

/// <summary>Describes one item of a ribbon group.</summary>
/// <param name="Id">The stable item id.</param>
/// <param name="Group">The group that hosts the item.</param>
/// <param name="Command">The command the item invokes.</param>
/// <param name="Order">The order of the item inside its group.</param>
/// <param name="IconKey">The host icon key, or <see langword="null"/> when the command icon is used.</param>
public sealed record RibbonItemDescriptor(
    RibbonItemId Id,
    RibbonGroupId Group,
    CommandId Command,
    int Order = 0,
    string? IconKey = null);

/// <summary>Describes where a settings page appears in the settings tree.</summary>
public enum LierneSettingsGroup
{
    /// <summary>The built-in General node at the top of the tree.</summary>
    General,

    /// <summary>The node of the plugin that contributed the page.</summary>
    Plugin
}

/// <summary>Describes a settings page contributed by a plugin.</summary>
/// <param name="Id">The stable page id.</param>
/// <param name="Title">The localized page title.</param>
/// <param name="Order">The order of the page among settings pages.</param>
/// <param name="Component">
/// A custom page component, or <see langword="null"/> to generate the page from
/// <see cref="SettingDescriptor"/> entries.
/// </param>
/// <param name="Group">The settings-tree group that hosts the page.</param>
/// <param name="Plugin">The contributing plugin, stamped by the builder.</param>
public sealed record SettingsPageDescriptor(
    SettingsPageId Id,
    StringKey Title,
    int Order = 0,
    Type? Component = null,
    LierneSettingsGroup Group = LierneSettingsGroup.Plugin,
    PluginId? Plugin = null);

/// <summary>Describes one setting.</summary>
/// <param name="Key">The stable setting key.</param>
/// <param name="Label">The localized label.</param>
/// <param name="Kind">The editor used for the value.</param>
/// <param name="DefaultValue">The value used when the setting is not stored.</param>
/// <param name="Choices">The choices of a <see cref="LierneSettingKind.Select"/> setting.</param>
/// <param name="Min">The smallest accepted integer.</param>
/// <param name="Max">The largest accepted integer.</param>
/// <param name="Step">The increment of the integer editor.</param>
/// <param name="Page">The settings page that shows the setting, or <see langword="null"/> for none.</param>
public sealed record SettingDescriptor(
    SettingsKey Key,
    StringKey Label,
    LierneSettingKind Kind,
    object? DefaultValue = null,
    IReadOnlyList<SettingChoice>? Choices = null,
    int? Min = null,
    int? Max = null,
    int? Step = null,
    SettingsPageId? Page = null);

/// <summary>Describes one choice of a select setting.</summary>
/// <param name="Value">The stored value.</param>
/// <param name="Label">The localized choice label.</param>
public sealed record SettingChoice(string Value, StringKey Label);
