using Microsoft.Extensions.DependencyInjection;

namespace Lierne;

/// <summary>
/// A compile-time component of a Lierne application: the host references it, lists it and the
/// plugin declares the documents, tool windows, commands, ribbon items and settings it brings.
/// </summary>
public interface ILiernePlugin
{
    /// <summary>Gets the stable plugin id.</summary>
    PluginId Id { get; }

    /// <summary>Gets the localized display name of the plugin.</summary>
    StringKey DisplayName { get; }

    /// <summary>
    /// Gets the plugins that must be composed before this one. The host orders plugins by these
    /// edges and reports a missing dependency or a cycle instead of composing in an arbitrary order.
    /// </summary>
    IReadOnlyList<PluginId> DependsOn { get; }

    /// <summary>Declares the services and contributions of the plugin.</summary>
    /// <param name="builder">The builder that collects the plugin contributions.</param>
    /// <remarks>
    /// The host calls this once, at composition time, in dependency order. The method registers
    /// service descriptors and contribution descriptors; it does not resolve services or build UI.
    /// </remarks>
    void Configure(ILiernePluginBuilder builder);
}

/// <summary>Collects the services and contributions a plugin declares.</summary>
public interface ILiernePluginBuilder
{
    /// <summary>Gets the service collection the plugin registers its services into.</summary>
    IServiceCollection Services { get; }

    /// <summary>Gets the documents declared so far.</summary>
    IReadOnlyList<DocumentDescriptor> Documents { get; }

    /// <summary>Gets the tool windows declared so far.</summary>
    IReadOnlyList<ToolWindowDescriptor> ToolWindows { get; }

    /// <summary>Gets the commands declared so far.</summary>
    IReadOnlyList<CommandDescriptor> Commands { get; }

    /// <summary>Gets the ribbon groups declared so far.</summary>
    IReadOnlyList<RibbonGroupDescriptor> RibbonGroups { get; }

    /// <summary>Gets the ribbon items declared so far.</summary>
    IReadOnlyList<RibbonItemDescriptor> RibbonItems { get; }

    /// <summary>Gets the settings pages declared so far.</summary>
    IReadOnlyList<SettingsPageDescriptor> SettingsPages { get; }

    /// <summary>Gets the settings declared so far.</summary>
    IReadOnlyList<SettingDescriptor> Settings { get; }

    /// <summary>Gets the top-level menu groups declared so far.</summary>
    IReadOnlyList<MenuGroupDescriptor> MenuGroups { get; }

    /// <summary>Gets the top-level menu items declared so far.</summary>
    IReadOnlyList<MenuItemDescriptor> MenuItems { get; }

    /// <summary>Gets the localized string catalogs declared so far.</summary>
    IReadOnlyList<LierneStringCatalog> StringCatalogs { get; }

    /// <summary>Gets the toolbars declared so far.</summary>
    IReadOnlyList<ToolbarDescriptor> Toolbars { get; }

    /// <summary>Gets the status-bar items declared so far.</summary>
    IReadOnlyList<StatusBarItemDescriptor> StatusBarItems { get; }

    /// <summary>Gets the settings-transfer sections declared so far.</summary>
    IReadOnlyList<SettingsTransferDescriptor> SettingsTransfers { get; }

    /// <summary>Gets the shell-surface contributions declared so far.</summary>
    IReadOnlyList<ShellContentDescriptor> ShellContents { get; }

    /// <summary>Declares a document type.</summary>
    /// <param name="descriptor">The document descriptor.</param>
    void AddDocument(DocumentDescriptor descriptor);

    /// <summary>Declares a tool window.</summary>
    /// <param name="descriptor">The tool window descriptor.</param>
    void AddToolWindow(ToolWindowDescriptor descriptor);

    /// <summary>Declares a command with its handler.</summary>
    /// <param name="descriptor">The command metadata.</param>
    /// <param name="handler">The handler that executes the command.</param>
    /// <param name="canExecute">The optional predicate over the shared state.</param>
    void AddCommand(
        CommandDescriptor descriptor,
        LierneCommandHandler handler,
        Func<ILierneContext, bool>? canExecute = null);

    /// <summary>Declares a ribbon group.</summary>
    /// <param name="descriptor">The group descriptor.</param>
    void AddRibbonGroup(RibbonGroupDescriptor descriptor);

    /// <summary>Declares a ribbon item.</summary>
    /// <param name="descriptor">The item descriptor.</param>
    void AddRibbonItem(RibbonItemDescriptor descriptor);

    /// <summary>Declares a settings page.</summary>
    /// <param name="descriptor">The page descriptor.</param>
    void AddSettingsPage(SettingsPageDescriptor descriptor);

    /// <summary>Declares a setting.</summary>
    /// <param name="descriptor">The setting descriptor.</param>
    void AddSetting(SettingDescriptor descriptor);

    /// <summary>Declares a top-level menu group.</summary>
    /// <param name="descriptor">The group descriptor.</param>
    void AddMenuGroup(MenuGroupDescriptor descriptor);

    /// <summary>Declares a top-level menu item or submenu.</summary>
    /// <param name="descriptor">The item descriptor.</param>
    void AddMenuItem(MenuItemDescriptor descriptor);

    /// <summary>Declares a localized string catalog backed by embedded resources.</summary>
    /// <param name="catalog">The catalog and the string-key namespace it serves.</param>
    /// <remarks>
    /// The neutral <c>.resx</c> of the resource manager holds the English strings; a culture
    /// assembly such as <c>.ru.resx</c> holds the translation. The host resolves a
    /// <see cref="StringKey"/> through the catalog that serves its namespace.
    /// </remarks>
    void AddStrings(LierneStringCatalog catalog);

    /// <summary>Declares a toolbar rendered in the host command area.</summary>
    /// <param name="descriptor">The toolbar descriptor.</param>
    void AddToolbar(ToolbarDescriptor descriptor);

    /// <summary>Declares a status-bar item.</summary>
    /// <param name="descriptor">The item descriptor.</param>
    void AddStatusBarItem(StatusBarItemDescriptor descriptor);

    /// <summary>Declares a settings section the Import/Export page can save and apply.</summary>
    /// <param name="descriptor">The section descriptor.</param>
    /// <remarks>The section id must be unique among the composed plugins.</remarks>
    void AddSettingsTransfer(SettingsTransferDescriptor descriptor);

    /// <summary>Declares a component that replaces one shell surface.</summary>
    /// <param name="descriptor">The shell-content descriptor.</param>
    void AddShellContent(ShellContentDescriptor descriptor);
}
