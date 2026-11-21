using Microsoft.Extensions.DependencyInjection;

namespace Lierne;

/// <summary>One contribution together with the plugin that declared it.</summary>
/// <typeparam name="T">The descriptor type.</typeparam>
/// <param name="Plugin">The declaring plugin.</param>
/// <param name="Descriptor">The descriptor.</param>
internal readonly record struct OwnedContribution<T>(PluginId Plugin, T Descriptor);

/// <summary>One command update handler together with the command it serves.</summary>
/// <param name="Command">The command id.</param>
/// <param name="Handler">The update handler.</param>
internal sealed record CommandUpdateDescriptor(CommandId Command, ICommandUpdateHandler Handler);

/// <summary>Collects contributions while the catalog configures the plugins.</summary>
internal sealed class LiernePluginBuilder : ILiernePluginBuilder
{
    private readonly List<OwnedContribution<DocumentDescriptor>> _documents = [];
    private readonly List<OwnedContribution<DocumentViewDescriptor>> _documentViews = [];
    private readonly List<OwnedContribution<ToolWindowDescriptor>> _toolWindows = [];
    private readonly List<OwnedContribution<LierneCommandRegistration>> _commands = [];
    private readonly List<OwnedContribution<CommandPlacementDescriptor>> _commandPlacements = [];
    private readonly List<OwnedContribution<CommandUpdateDescriptor>> _commandUpdateHandlers = [];
    private readonly List<OwnedContribution<RibbonGroupDescriptor>> _ribbonGroups = [];
    private readonly List<OwnedContribution<RibbonItemDescriptor>> _ribbonItems = [];
    private readonly List<OwnedContribution<SettingsPageDescriptor>> _settingsPages = [];
    private readonly List<OwnedContribution<SettingDescriptor>> _settings = [];
    private readonly List<OwnedContribution<MenuGroupDescriptor>> _menuGroups = [];
    private readonly List<OwnedContribution<MenuItemDescriptor>> _menuItems = [];
    private readonly List<OwnedContribution<LierneStringCatalog>> _stringCatalogs = [];
    private readonly List<OwnedContribution<ToolbarDescriptor>> _toolbars = [];
    private readonly List<OwnedContribution<StatusBarItemDescriptor>> _statusBarItems = [];
    private readonly List<OwnedContribution<SettingsTransferDescriptor>> _settingsTransfers = [];
    private readonly List<OwnedContribution<ShellContentDescriptor>> _shellContents = [];

    /// <summary>Creates the builder.</summary>
    /// <param name="services">The service collection the plugins register into.</param>
    public LiernePluginBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    /// <inheritdoc />
    public IServiceCollection Services { get; }

    /// <summary>Gets the plugin whose <c>Configure</c> call is running.</summary>
    public PluginId CurrentPlugin { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<DocumentDescriptor> Documents =>
        _documents.Select(contribution => contribution.Descriptor).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<DocumentViewDescriptor> DocumentViews =>
        _documentViews.Select(contribution => contribution.Descriptor).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<ToolWindowDescriptor> ToolWindows =>
        _toolWindows.Select(contribution => contribution.Descriptor).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<CommandDescriptor> Commands =>
        _commands
            .Select(contribution => contribution.Descriptor.Descriptor)
            .ToArray();

    /// <summary>Gets the command registrations with their handlers.</summary>
    public IReadOnlyList<LierneCommandRegistration> CommandRegistrations =>
        _commands.Select(contribution => contribution.Descriptor).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<CommandPlacementDescriptor> CommandPlacements =>
        _commandPlacements.Select(contribution => contribution.Descriptor).ToArray();

    /// <inheritdoc />
    public IReadOnlyDictionary<CommandId, ICommandUpdateHandler> CommandUpdateHandlers =>
        _commandUpdateHandlers
            .GroupBy(contribution => contribution.Descriptor.Command)
            .ToDictionary(
                group => group.Key,
                group => group.First().Descriptor.Handler);

    /// <inheritdoc />
    public IReadOnlyList<RibbonGroupDescriptor> RibbonGroups =>
        _ribbonGroups.Select(contribution => contribution.Descriptor).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<RibbonItemDescriptor> RibbonItems =>
        _ribbonItems.Select(contribution => contribution.Descriptor).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<SettingsPageDescriptor> SettingsPages =>
        _settingsPages.Select(contribution => contribution.Descriptor).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<SettingDescriptor> Settings =>
        _settings.Select(contribution => contribution.Descriptor).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<MenuGroupDescriptor> MenuGroups =>
        _menuGroups.Select(contribution => contribution.Descriptor).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<MenuItemDescriptor> MenuItems =>
        _menuItems.Select(contribution => contribution.Descriptor).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<LierneStringCatalog> StringCatalogs =>
        _stringCatalogs.Select(contribution => contribution.Descriptor).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<ToolbarDescriptor> Toolbars =>
        _toolbars.Select(contribution => contribution.Descriptor).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<StatusBarItemDescriptor> StatusBarItems =>
        _statusBarItems.Select(contribution => contribution.Descriptor).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<SettingsTransferDescriptor> SettingsTransfers =>
        _settingsTransfers.Select(contribution => contribution.Descriptor).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<ShellContentDescriptor> ShellContents =>
        _shellContents.Select(contribution => contribution.Descriptor).ToArray();

    /// <summary>Gets the documents with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<DocumentDescriptor>> OwnedDocuments => _documents;

    /// <summary>Gets the document views with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<DocumentViewDescriptor>> OwnedDocumentViews =>
        _documentViews;

    /// <summary>Gets the tool windows with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<ToolWindowDescriptor>> OwnedToolWindows => _toolWindows;

    /// <summary>Gets the commands with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<LierneCommandRegistration>> OwnedCommands => _commands;

    /// <summary>Gets the command placements with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<CommandPlacementDescriptor>> OwnedCommandPlacements =>
        _commandPlacements;

    /// <summary>Gets the command update handlers with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<CommandUpdateDescriptor>> OwnedCommandUpdateHandlers =>
        _commandUpdateHandlers;

    /// <summary>Gets the ribbon groups with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<RibbonGroupDescriptor>> OwnedRibbonGroups => _ribbonGroups;

    /// <summary>Gets the ribbon items with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<RibbonItemDescriptor>> OwnedRibbonItems => _ribbonItems;

    /// <summary>Gets the settings pages with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<SettingsPageDescriptor>> OwnedSettingsPages => _settingsPages;

    /// <summary>Gets the settings with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<SettingDescriptor>> OwnedSettings => _settings;

    /// <summary>Gets the menu groups with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<MenuGroupDescriptor>> OwnedMenuGroups => _menuGroups;

    /// <summary>Gets the menu items with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<MenuItemDescriptor>> OwnedMenuItems => _menuItems;

    /// <summary>Gets the string catalogs with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<LierneStringCatalog>> OwnedStringCatalogs =>
        _stringCatalogs;

    /// <summary>Gets the toolbars with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<ToolbarDescriptor>> OwnedToolbars => _toolbars;

    /// <summary>Gets the status-bar items with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<StatusBarItemDescriptor>> OwnedStatusBarItems =>
        _statusBarItems;

    /// <summary>Gets the settings-transfer sections with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<SettingsTransferDescriptor>> OwnedSettingsTransfers =>
        _settingsTransfers;

    /// <summary>Gets the shell-surface contributions with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<ShellContentDescriptor>> OwnedShellContents =>
        _shellContents;

    /// <summary>Starts collecting contributions for one plugin.</summary>
    /// <param name="plugin">The plugin whose configuration is starting.</param>
    public void BeginPlugin(PluginId plugin) => CurrentPlugin = plugin;

    /// <inheritdoc />
    public void AddDocument(DocumentDescriptor descriptor) =>
        Add(_documents, descriptor);

    /// <inheritdoc />
    public void AddDocumentView(DocumentViewDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(descriptor.Component);
        Add(_documentViews, descriptor);
    }

    /// <inheritdoc />
    public void AddToolWindow(ToolWindowDescriptor descriptor) =>
        Add(_toolWindows, descriptor);

    /// <inheritdoc />
    public void AddCommand(
        CommandDescriptor descriptor,
        LierneCommandHandler handler,
        Func<ILierneContext, bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(handler);
        _commands.Add(new OwnedContribution<LierneCommandRegistration>(
            CurrentPlugin,
            new LierneCommandRegistration(descriptor, handler, canExecute, CurrentPlugin)));
    }

    /// <inheritdoc />
    public void AddCommandPlacement(CommandPlacementDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Group);
        Add(_commandPlacements, descriptor);
    }

    /// <inheritdoc />
    public void AddCommandUpdateHandler(CommandId command, ICommandUpdateHandler updateHandler)
    {
        ArgumentNullException.ThrowIfNull(updateHandler);
        _commandUpdateHandlers.Add(new OwnedContribution<CommandUpdateDescriptor>(
            CurrentPlugin,
            new CommandUpdateDescriptor(command, updateHandler)));
    }

    /// <inheritdoc />
    public void AddRibbonGroup(RibbonGroupDescriptor descriptor) =>
        Add(_ribbonGroups, descriptor);

    /// <inheritdoc />
    public void AddRibbonItem(RibbonItemDescriptor descriptor) =>
        Add(_ribbonItems, descriptor);

    /// <inheritdoc />
    public void AddSettingsPage(SettingsPageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        _settingsPages.Add(new OwnedContribution<SettingsPageDescriptor>(
            CurrentPlugin,
            descriptor with { Plugin = descriptor.Plugin ?? CurrentPlugin }));
    }

    /// <inheritdoc />
    public void AddSetting(SettingDescriptor descriptor) =>
        Add(_settings, descriptor);

    /// <inheritdoc />
    public void AddMenuGroup(MenuGroupDescriptor descriptor) =>
        Add(_menuGroups, descriptor);

    /// <inheritdoc />
    public void AddMenuItem(MenuItemDescriptor descriptor) =>
        Add(_menuItems, descriptor);

    /// <inheritdoc />
    public void AddStrings(LierneStringCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalog.Namespace);
        ArgumentNullException.ThrowIfNull(catalog.Resources);
        _stringCatalogs.Add(new OwnedContribution<LierneStringCatalog>(
            CurrentPlugin,
            catalog));
    }

    /// <inheritdoc />
    public void AddToolbar(ToolbarDescriptor descriptor) =>
        Add(_toolbars, descriptor);

    /// <inheritdoc />
    public void AddStatusBarItem(StatusBarItemDescriptor descriptor) =>
        Add(_statusBarItems, descriptor);

    /// <inheritdoc />
    public void AddSettingsTransfer(SettingsTransferDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Section);
        ArgumentNullException.ThrowIfNull(descriptor.Export);
        ArgumentNullException.ThrowIfNull(descriptor.Import);
        Add(_settingsTransfers, descriptor);
    }

    /// <inheritdoc />
    public void AddShellContent(ShellContentDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(descriptor.Component);
        Add(_shellContents, descriptor);
    }

    private void Add<T>(List<OwnedContribution<T>> contributions, T descriptor)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        contributions.Add(new OwnedContribution<T>(CurrentPlugin, descriptor));
    }
}
