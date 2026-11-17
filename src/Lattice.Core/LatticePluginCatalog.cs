using Microsoft.Extensions.DependencyInjection;

namespace Lattice;

/// <summary>
/// The frozen result of composing the configured plugins: plugins in dependency order and the
/// contributions they declared, together with the diagnostics found while composing them.
/// </summary>
public sealed class LatticePluginCatalog
{
    private LatticePluginCatalog(
        IReadOnlyList<LatticePluginInfo> plugins,
        IReadOnlyList<DocumentDescriptor> documents,
        IReadOnlyList<ToolWindowDescriptor> toolWindows,
        IReadOnlyList<CommandDescriptor> commands,
        IReadOnlyList<LatticeCommandRegistration> commandRegistrations,
        IReadOnlyList<RibbonGroupDescriptor> ribbonGroups,
        IReadOnlyList<RibbonItemDescriptor> ribbonItems,
        IReadOnlyList<SettingsPageDescriptor> settingsPages,
        IReadOnlyList<SettingDescriptor> settings,
        IReadOnlyList<MenuGroupDescriptor> menuGroups,
        IReadOnlyList<MenuItemDescriptor> menuItems,
        IReadOnlyList<LatticeStringCatalog> stringCatalogs,
        IReadOnlyList<LatticeDiagnostic> diagnostics)
    {
        Plugins = plugins;
        Documents = documents;
        ToolWindows = toolWindows;
        Commands = commands;
        CommandRegistrations = commandRegistrations;
        RibbonGroups = ribbonGroups;
        RibbonItems = ribbonItems;
        SettingsPages = settingsPages;
        Settings = settings;
        MenuGroups = menuGroups;
        MenuItems = menuItems;
        StringCatalogs = stringCatalogs;
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the composed plugins in dependency order.</summary>
    public IReadOnlyList<LatticePluginInfo> Plugins { get; }

    /// <summary>Gets the document types in plugin order.</summary>
    public IReadOnlyList<DocumentDescriptor> Documents { get; }

    /// <summary>Gets the tool windows in plugin order.</summary>
    public IReadOnlyList<ToolWindowDescriptor> ToolWindows { get; }

    /// <summary>Gets the commands in plugin order.</summary>
    public IReadOnlyList<CommandDescriptor> Commands { get; }

    /// <summary>Gets the command registrations with their handlers in plugin order.</summary>
    public IReadOnlyList<LatticeCommandRegistration> CommandRegistrations { get; }

    /// <summary>Gets the ribbon groups in plugin order.</summary>
    public IReadOnlyList<RibbonGroupDescriptor> RibbonGroups { get; }

    /// <summary>Gets the ribbon items in plugin order.</summary>
    public IReadOnlyList<RibbonItemDescriptor> RibbonItems { get; }

    /// <summary>Gets the settings pages in plugin order.</summary>
    public IReadOnlyList<SettingsPageDescriptor> SettingsPages { get; }

    /// <summary>Gets the settings in plugin order.</summary>
    public IReadOnlyList<SettingDescriptor> Settings { get; }

    /// <summary>Gets the top-level menu groups in plugin order.</summary>
    public IReadOnlyList<MenuGroupDescriptor> MenuGroups { get; }

    /// <summary>Gets the top-level menu items in plugin order.</summary>
    public IReadOnlyList<MenuItemDescriptor> MenuItems { get; }

    /// <summary>Gets the localized string catalogs in plugin order.</summary>
    public IReadOnlyList<LatticeStringCatalog> StringCatalogs { get; }

    /// <summary>Gets the diagnostics found while composing the plugins.</summary>
    public IReadOnlyList<LatticeDiagnostic> Diagnostics { get; }

    /// <summary>Gets a value indicating whether a diagnostic blocks the composition.</summary>
    public bool HasErrors =>
        Diagnostics.Any(diagnostic => diagnostic.Severity == LatticeDiagnosticSeverity.Error);

    /// <summary>Composes the plugins and validates the contributions they declare.</summary>
    /// <param name="plugins">The plugins in declaration order.</param>
    /// <param name="services">
    /// The service collection the plugins register into, or <see langword="null"/> to collect the
    /// registrations into a throw-away collection.
    /// </param>
    /// <returns>The composed catalog; inspect <see cref="Diagnostics"/> before starting the host.</returns>
    public static LatticePluginCatalog Build(
        IReadOnlyList<ILatticePlugin> plugins,
        IServiceCollection? services = null)
    {
        ArgumentNullException.ThrowIfNull(plugins);
        var diagnostics = new List<LatticeDiagnostic>();
        var ordered = OrderPlugins(plugins, diagnostics);
        var builder = new LatticePluginBuilder(services ?? new ServiceCollection());
        foreach (var plugin in ordered)
        {
            builder.BeginPlugin(plugin.Id);
            plugin.Configure(builder);
        }

        ValidateContributions(builder, diagnostics);

        return new LatticePluginCatalog(
            ordered
                .Select((plugin, order) => new LatticePluginInfo(plugin.Id, plugin.DisplayName, order))
                .ToArray(),
            builder.OwnedDocuments.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedToolWindows.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedCommands
                .Select(contribution => contribution.Descriptor.Descriptor)
                .ToArray(),
            builder.OwnedCommands.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedRibbonGroups.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedRibbonItems.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedSettingsPages.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedSettings.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedMenuGroups.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedMenuItems.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedStringCatalogs.Select(contribution => contribution.Descriptor).ToArray(),
            diagnostics);
    }

    private static List<ILatticePlugin> OrderPlugins(
        IReadOnlyList<ILatticePlugin> plugins,
        List<LatticeDiagnostic> diagnostics)
    {
        var byId = new Dictionary<PluginId, ILatticePlugin>();
        foreach (var plugin in plugins)
        {
            if (!byId.TryAdd(plugin.Id, plugin))
            {
                diagnostics.Add(LatticeDiagnostic.Error(
                    "lattice.plugin.duplicate",
                    $"Plugin id '{plugin.Id}' is registered more than once."));
            }
        }

        var state = new Dictionary<PluginId, VisitState>();
        var ordered = new List<ILatticePlugin>(byId.Count);
        var started = new HashSet<PluginId>();

        void Visit(ILatticePlugin plugin)
        {
            var current = state.GetValueOrDefault(plugin.Id);
            if (current == VisitState.Visited)
            {
                return;
            }

            if (current == VisitState.Visiting)
            {
                diagnostics.Add(LatticeDiagnostic.Error(
                    "lattice.plugin.cycle",
                    $"The plugin dependency cycle includes '{plugin.Id}'."));
                return;
            }

            state[plugin.Id] = VisitState.Visiting;
            foreach (var dependencyId in plugin.DependsOn)
            {
                if (byId.TryGetValue(dependencyId, out var dependency))
                {
                    Visit(dependency);
                }
                else
                {
                    diagnostics.Add(LatticeDiagnostic.Error(
                        "lattice.plugin.missing_dependency",
                        $"Plugin '{plugin.Id}' depends on '{dependencyId}', which is not registered."));
                }
            }

            state[plugin.Id] = VisitState.Visited;
            ordered.Add(plugin);
        }

        foreach (var plugin in plugins)
        {
            if (started.Add(plugin.Id))
            {
                Visit(plugin);
            }
        }

        return ordered;
    }

    private static void ValidateContributions(
        LatticePluginBuilder builder,
        List<LatticeDiagnostic> diagnostics)
    {
        ValidateDuplicateIds(
            builder.OwnedDocuments,
            contribution => contribution.Id.Value,
            "Document type",
            diagnostics);
        ValidateDuplicateIds(
            builder.OwnedToolWindows,
            contribution => contribution.Id.Value,
            "Tool window",
            diagnostics);
        ValidateDuplicateIds(
            builder.OwnedCommands,
            contribution => contribution.Descriptor.Id.Value,
            "Command",
            diagnostics);
        ValidateDuplicateIds(
            builder.OwnedRibbonGroups,
            contribution => contribution.Id.Value,
            "Ribbon group",
            diagnostics);
        ValidateDuplicateIds(
            builder.OwnedRibbonItems,
            contribution => contribution.Id.Value,
            "Ribbon item",
            diagnostics);
        ValidateDuplicateIds(
            builder.OwnedSettingsPages,
            contribution => contribution.Id.Value,
            "Settings page",
            diagnostics);
        ValidateDuplicateIds(
            builder.OwnedSettings,
            contribution => contribution.Key.Value,
            "Setting",
            diagnostics);
        ValidateSettingsPages(builder, diagnostics);
        ValidateMenus(builder, diagnostics);

        var commands = new HashSet<string>(
            builder.OwnedCommands.Select(contribution => contribution.Descriptor.Descriptor.Id.Value),
            StringComparer.Ordinal);
        var groups = new HashSet<string>(
            builder.OwnedRibbonGroups.Select(contribution => contribution.Descriptor.Id.Value),
            StringComparer.Ordinal);
        foreach (var contribution in builder.OwnedRibbonItems)
        {
            var item = contribution.Descriptor;
            if (!commands.Contains(item.Command.Value))
            {
                diagnostics.Add(LatticeDiagnostic.Error(
                    "lattice.ribbon.unknown_command",
                    $"Ribbon item '{item.Id}' of plugin '{contribution.Plugin}' invokes unknown command '{item.Command}'."));
            }

            if (!groups.Contains(item.Group.Value))
            {
                diagnostics.Add(LatticeDiagnostic.Error(
                    "lattice.ribbon.unknown_group",
                    $"Ribbon item '{item.Id}' of plugin '{contribution.Plugin}' belongs to unknown group '{item.Group}'."));
            }
        }
    }

    private static void ValidateSettingsPages(
        LatticePluginBuilder builder,
        List<LatticeDiagnostic> diagnostics)
    {
        var pages = new HashSet<string>(
            builder.OwnedSettingsPages.Select(contribution => contribution.Descriptor.Id.Value),
            StringComparer.Ordinal);
        foreach (var contribution in builder.OwnedSettings)
        {
            if (contribution.Descriptor.Page is { } page && !pages.Contains(page.Value))
            {
                diagnostics.Add(LatticeDiagnostic.Error(
                    "lattice.settings.unknown_page",
                    $"Setting '{contribution.Descriptor.Key}' of plugin '{contribution.Plugin}' belongs to unknown page '{page}'."));
            }
        }
    }

    private static void ValidateMenus(
        LatticePluginBuilder builder,
        List<LatticeDiagnostic> diagnostics)
    {
        ValidateDuplicateIds(
            builder.OwnedMenuGroups,
            contribution => contribution.Id.Value,
            "Menu group",
            diagnostics);
        var groups = new HashSet<string>(
            builder.OwnedMenuGroups.Select(contribution => contribution.Descriptor.Id.Value),
            StringComparer.Ordinal);
        var commands = new HashSet<string>(
            builder.OwnedCommands.Select(contribution => contribution.Descriptor.Descriptor.Id.Value),
            StringComparer.Ordinal);
        var items = new List<OwnedMenuEntry>();
        foreach (var contribution in builder.OwnedMenuItems)
        {
            FlattenMenu(contribution.Plugin, contribution.Descriptor, items);
        }

        var owners = new Dictionary<string, PluginId>(StringComparer.Ordinal);
        foreach (var entry in items)
        {
            if (owners.TryGetValue(entry.Item.Id.Value, out var owner))
            {
                diagnostics.Add(LatticeDiagnostic.Error(
                    "lattice.contribution.duplicate",
                    $"Menu item '{entry.Item.Id}' is contributed by both '{owner}' and '{entry.Plugin}'."));
                continue;
            }

            owners[entry.Item.Id.Value] = entry.Plugin;
        }

        foreach (var entry in items)
        {
            var item = entry.Item;
            if (item.IsSeparator)
            {
                continue;
            }

            if (!groups.Contains(item.Group.Value))
            {
                diagnostics.Add(LatticeDiagnostic.Error(
                    "lattice.menu.unknown_group",
                    $"Menu item '{item.Id}' of plugin '{entry.Plugin}' belongs to unknown group '{item.Group}'."));
            }

            if (item.Command is { } command)
            {
                if (!commands.Contains(command.Value))
                {
                    diagnostics.Add(LatticeDiagnostic.Error(
                        "lattice.menu.unknown_command",
                        $"Menu item '{item.Id}' of plugin '{entry.Plugin}' invokes unknown command '{command}'."));
                }
            }
            else if (item.Children is null || item.Children.Count == 0)
            {
                diagnostics.Add(LatticeDiagnostic.Error(
                    "lattice.menu.invalid_item",
                    $"Menu item '{item.Id}' of plugin '{entry.Plugin}' has no command and no children."));
            }
        }
    }

    private static void FlattenMenu(
        PluginId plugin,
        MenuItemDescriptor item,
        List<OwnedMenuEntry> items)
    {
        items.Add(new OwnedMenuEntry(plugin, item));
        if (item.Children is null)
        {
            return;
        }

        foreach (var child in item.Children)
        {
            FlattenMenu(plugin, child, items);
        }
    }

    private readonly record struct OwnedMenuEntry(PluginId Plugin, MenuItemDescriptor Item);

    private static void ValidateDuplicateIds<T>(
        IReadOnlyList<OwnedContribution<T>> contributions,
        Func<T, string> id,
        string kind,
        List<LatticeDiagnostic> diagnostics)
    {
        var owners = new Dictionary<string, PluginId>(StringComparer.Ordinal);
        foreach (var contribution in contributions)
        {
            var identifier = id(contribution.Descriptor);
            if (owners.TryGetValue(identifier, out var owner))
            {
                diagnostics.Add(LatticeDiagnostic.Error(
                    "lattice.contribution.duplicate",
                    $"{kind} '{identifier}' is contributed by both '{owner}' and '{contribution.Plugin}'."));
                continue;
            }

            owners[identifier] = contribution.Plugin;
        }
    }

    private enum VisitState
    {
        Unvisited,
        Visiting,
        Visited
    }
}
