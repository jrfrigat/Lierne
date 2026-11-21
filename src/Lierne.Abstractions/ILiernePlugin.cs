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

    /// <summary>Gets the plugins that are ordered before this one when they are present.</summary>
    IReadOnlyList<PluginId> OptionalDependsOn => [];

    /// <summary>Gets the plugin version, or <c>0.0</c> when the plugin does not declare one.</summary>
    Version Version => new(0, 0);

    /// <summary>Gets whether the application cannot start without the plugin.</summary>
    bool Essential => false;

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

    /// <summary>Gets the document views declared so far.</summary>
    IReadOnlyList<DocumentViewDescriptor> DocumentViews { get; }

    /// <summary>Gets the tool windows declared so far.</summary>
    IReadOnlyList<ToolWindowDescriptor> ToolWindows { get; }

    /// <summary>Gets the commands declared so far.</summary>
    IReadOnlyList<CommandDescriptor> Commands { get; }

    /// <summary>Gets the command placements declared so far.</summary>
    IReadOnlyList<CommandPlacementDescriptor> CommandPlacements { get; }

    /// <summary>Gets the command update handlers declared so far.</summary>
    IReadOnlyDictionary<CommandId, ICommandUpdateHandler> CommandUpdateHandlers { get; }

    /// <summary>Gets the ids of the extension points declared so far.</summary>
    IReadOnlyList<ExtensionPointId> ExtensionPoints { get; }

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

    /// <summary>Declares a view of a document type.</summary>
    /// <param name="descriptor">The document view descriptor.</param>
    /// <remarks>
    /// The document type may be declared by another plugin; the catalog validates that some plugin
    /// declares it and that the view id is unique among the views of that type.
    /// </remarks>
    void AddDocumentView(DocumentViewDescriptor descriptor);

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

    /// <summary>Declares where a command appears on a surface.</summary>
    /// <param name="descriptor">The placement descriptor.</param>
    void AddCommandPlacement(CommandPlacementDescriptor descriptor);

    /// <summary>Declares the handler that computes the live presentation of a command.</summary>
    /// <param name="command">The command id.</param>
    /// <param name="updateHandler">The update handler.</param>
    void AddCommandUpdateHandler(CommandId command, ICommandUpdateHandler updateHandler);

    /// <summary>Declares a typed extension point.</summary>
    /// <typeparam name="T">The extension contract.</typeparam>
    /// <param name="id">The stable extension point id.</param>
    /// <returns>The declared extension point; a duplicate declaration keeps the first instance.</returns>
    IExtensionPoint<T> AddExtensionPoint<T>(ExtensionPointId id);

    /// <summary>Gets a declared extension point.</summary>
    /// <typeparam name="T">The extension contract.</typeparam>
    /// <param name="id">The stable extension point id.</param>
    /// <returns>The extension point.</returns>
    /// <exception cref="InvalidOperationException">The point is missing or has another contract.</exception>
    IExtensionPoint<T> GetExtensionPoint<T>(ExtensionPointId id);

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
