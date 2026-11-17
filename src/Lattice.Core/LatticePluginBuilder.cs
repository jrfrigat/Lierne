using Microsoft.Extensions.DependencyInjection;

namespace Lattice;

/// <summary>One contribution together with the plugin that declared it.</summary>
/// <typeparam name="T">The descriptor type.</typeparam>
/// <param name="Plugin">The declaring plugin.</param>
/// <param name="Descriptor">The descriptor.</param>
internal readonly record struct OwnedContribution<T>(PluginId Plugin, T Descriptor);

/// <summary>Collects contributions while the catalog configures the plugins.</summary>
internal sealed class LatticePluginBuilder : ILatticePluginBuilder
{
    private readonly List<OwnedContribution<DocumentDescriptor>> _documents = [];
    private readonly List<OwnedContribution<ToolWindowDescriptor>> _toolWindows = [];
    private readonly List<OwnedContribution<LatticeCommandRegistration>> _commands = [];
    private readonly List<OwnedContribution<RibbonGroupDescriptor>> _ribbonGroups = [];
    private readonly List<OwnedContribution<RibbonItemDescriptor>> _ribbonItems = [];
    private readonly List<OwnedContribution<SettingsPageDescriptor>> _settingsPages = [];
    private readonly List<OwnedContribution<SettingDescriptor>> _settings = [];
    private readonly List<OwnedContribution<MenuGroupDescriptor>> _menuGroups = [];
    private readonly List<OwnedContribution<MenuItemDescriptor>> _menuItems = [];
    private readonly List<OwnedContribution<LatticeStringCatalog>> _stringCatalogs = [];
    private readonly List<OwnedContribution<ToolbarDescriptor>> _toolbars = [];
    private readonly List<OwnedContribution<StatusBarItemDescriptor>> _statusBarItems = [];

    /// <summary>Creates the builder.</summary>
    /// <param name="services">The service collection the plugins register into.</param>
    public LatticePluginBuilder(IServiceCollection services)
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
    public IReadOnlyList<ToolWindowDescriptor> ToolWindows =>
        _toolWindows.Select(contribution => contribution.Descriptor).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<CommandDescriptor> Commands =>
        _commands
            .Select(contribution => contribution.Descriptor.Descriptor)
            .ToArray();

    /// <summary>Gets the command registrations with their handlers.</summary>
    public IReadOnlyList<LatticeCommandRegistration> CommandRegistrations =>
        _commands.Select(contribution => contribution.Descriptor).ToArray();

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
    public IReadOnlyList<LatticeStringCatalog> StringCatalogs =>
        _stringCatalogs.Select(contribution => contribution.Descriptor).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<ToolbarDescriptor> Toolbars =>
        _toolbars.Select(contribution => contribution.Descriptor).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<StatusBarItemDescriptor> StatusBarItems =>
        _statusBarItems.Select(contribution => contribution.Descriptor).ToArray();

    /// <summary>Gets the documents with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<DocumentDescriptor>> OwnedDocuments => _documents;

    /// <summary>Gets the tool windows with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<ToolWindowDescriptor>> OwnedToolWindows => _toolWindows;

    /// <summary>Gets the commands with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<LatticeCommandRegistration>> OwnedCommands => _commands;

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
    public IReadOnlyList<OwnedContribution<LatticeStringCatalog>> OwnedStringCatalogs =>
        _stringCatalogs;

    /// <summary>Gets the toolbars with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<ToolbarDescriptor>> OwnedToolbars => _toolbars;

    /// <summary>Gets the status-bar items with their declaring plugin.</summary>
    public IReadOnlyList<OwnedContribution<StatusBarItemDescriptor>> OwnedStatusBarItems =>
        _statusBarItems;

    /// <summary>Starts collecting contributions for one plugin.</summary>
    /// <param name="plugin">The plugin whose configuration is starting.</param>
    public void BeginPlugin(PluginId plugin) => CurrentPlugin = plugin;

    /// <inheritdoc />
    public void AddDocument(DocumentDescriptor descriptor) =>
        Add(_documents, descriptor);

    /// <inheritdoc />
    public void AddToolWindow(ToolWindowDescriptor descriptor) =>
        Add(_toolWindows, descriptor);

    /// <inheritdoc />
    public void AddCommand(
        CommandDescriptor descriptor,
        LatticeCommandHandler handler,
        Func<ILatticeContext, bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(handler);
        _commands.Add(new OwnedContribution<LatticeCommandRegistration>(
            CurrentPlugin,
            new LatticeCommandRegistration(descriptor, handler, canExecute)));
    }

    /// <inheritdoc />
    public void AddRibbonGroup(RibbonGroupDescriptor descriptor) =>
        Add(_ribbonGroups, descriptor);

    /// <inheritdoc />
    public void AddRibbonItem(RibbonItemDescriptor descriptor) =>
        Add(_ribbonItems, descriptor);

    /// <inheritdoc />
    public void AddSettingsPage(SettingsPageDescriptor descriptor) =>
        Add(_settingsPages, descriptor);

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
    public void AddStrings(LatticeStringCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalog.Namespace);
        ArgumentNullException.ThrowIfNull(catalog.Resources);
        _stringCatalogs.Add(new OwnedContribution<LatticeStringCatalog>(
            CurrentPlugin,
            catalog));
    }

    /// <inheritdoc />
    public void AddToolbar(ToolbarDescriptor descriptor) =>
        Add(_toolbars, descriptor);

    /// <inheritdoc />
    public void AddStatusBarItem(StatusBarItemDescriptor descriptor) =>
        Add(_statusBarItems, descriptor);

    private void Add<T>(List<OwnedContribution<T>> contributions, T descriptor)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        contributions.Add(new OwnedContribution<T>(CurrentPlugin, descriptor));
    }
}
