using System.Globalization;
using System.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace Lierne;

/// <summary>
/// The frozen result of composing the configured plugins: plugins in dependency order and the
/// contributions they declared, together with the diagnostics found while composing them.
/// </summary>
public sealed class LiernePluginCatalog
{
    private readonly IReadOnlyDictionary<DocumentTypeId, IReadOnlyList<DocumentViewDescriptor>>
        _viewsByType;

    private LiernePluginCatalog(
        IReadOnlyList<LiernePluginInfo> plugins,
        IReadOnlyList<DocumentDescriptor> documents,
        IReadOnlyList<DocumentViewDescriptor> documentViews,
        IReadOnlyList<ToolWindowDescriptor> toolWindows,
        IReadOnlyList<CommandDescriptor> commands,
        IReadOnlyList<LierneCommandRegistration> commandRegistrations,
        IReadOnlyList<CommandPlacementDescriptor> commandPlacements,
        IReadOnlyDictionary<CommandId, ICommandUpdateHandler> commandUpdateHandlers,
        IReadOnlyList<RibbonGroupDescriptor> ribbonGroups,
        IReadOnlyList<RibbonItemDescriptor> ribbonItems,
        IReadOnlyList<SettingsPageDescriptor> settingsPages,
        IReadOnlyList<SettingDescriptor> settings,
        IReadOnlyList<MenuGroupDescriptor> menuGroups,
        IReadOnlyList<MenuItemDescriptor> menuItems,
        IReadOnlyList<LierneStringCatalog> stringCatalogs,
        IReadOnlyList<ToolbarDescriptor> toolbars,
        IReadOnlyList<StatusBarItemDescriptor> statusBarItems,
        IReadOnlyList<SettingsTransferDescriptor> settingsTransfers,
        IReadOnlyList<ShellContentDescriptor> shellContents,
        IReadOnlyList<LierneDiagnostic> diagnostics)
    {
        Plugins = plugins;
        Documents = documents;
        DocumentViews = documentViews;
        _viewsByType = documentViews
            .GroupBy(view => view.DocumentType)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DocumentViewDescriptor>)group
                    .OrderBy(view => view.Order)
                    .ThenBy(view => view.Id.Value, StringComparer.Ordinal)
                    .ToArray());
        ToolWindows = toolWindows;
        Commands = commands;
        CommandRegistrations = commandRegistrations;
        CommandPlacements = commandPlacements;
        CommandUpdateHandlers = commandUpdateHandlers;
        RibbonGroups = ribbonGroups;
        RibbonItems = ribbonItems;
        SettingsPages = settingsPages;
        Settings = settings;
        MenuGroups = menuGroups;
        MenuItems = menuItems;
        StringCatalogs = stringCatalogs;
        Toolbars = toolbars;
        StatusBarItems = statusBarItems;
        SettingsTransfers = settingsTransfers;
        ShellContents = shellContents;
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the composed plugins in dependency order.</summary>
    public IReadOnlyList<LiernePluginInfo> Plugins { get; }

    /// <summary>Gets the document types in plugin order.</summary>
    public IReadOnlyList<DocumentDescriptor> Documents { get; }

    /// <summary>Gets the document views in plugin order.</summary>
    public IReadOnlyList<DocumentViewDescriptor> DocumentViews { get; }

    /// <summary>Gets the tool windows in plugin order.</summary>
    public IReadOnlyList<ToolWindowDescriptor> ToolWindows { get; }

    /// <summary>Gets the commands in plugin order.</summary>
    public IReadOnlyList<CommandDescriptor> Commands { get; }

    /// <summary>Gets the command registrations with their handlers in plugin order.</summary>
    public IReadOnlyList<LierneCommandRegistration> CommandRegistrations { get; }

    /// <summary>Gets the command placements in plugin order.</summary>
    public IReadOnlyList<CommandPlacementDescriptor> CommandPlacements { get; }

    /// <summary>Gets the command update handlers by command.</summary>
    public IReadOnlyDictionary<CommandId, ICommandUpdateHandler> CommandUpdateHandlers { get; }

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
    public IReadOnlyList<LierneStringCatalog> StringCatalogs { get; }

    /// <summary>Gets the toolbars in plugin order.</summary>
    public IReadOnlyList<ToolbarDescriptor> Toolbars { get; }

    /// <summary>Gets the status-bar items in plugin order.</summary>
    public IReadOnlyList<StatusBarItemDescriptor> StatusBarItems { get; }

    /// <summary>Gets the settings-transfer sections in plugin order.</summary>
    public IReadOnlyList<SettingsTransferDescriptor> SettingsTransfers { get; }

    /// <summary>Gets the shell-surface contributions in plugin order.</summary>
    public IReadOnlyList<ShellContentDescriptor> ShellContents { get; }

    /// <summary>Gets the diagnostics found while composing the plugins.</summary>
    public IReadOnlyList<LierneDiagnostic> Diagnostics { get; }

    /// <summary>Gets a value indicating whether a diagnostic blocks the composition.</summary>
    public bool HasErrors =>
        Diagnostics.Any(diagnostic => diagnostic.Severity == LierneDiagnosticSeverity.Error);

    /// <summary>Gets the document views contributed to one document type.</summary>
    /// <param name="typeId">The document type id.</param>
    /// <returns>
    /// The views of the type ordered by <see cref="DocumentViewDescriptor.Order"/> and then by
    /// <see cref="DocumentViewId.Value"/> using ordinal comparison, or an empty list when no plugin
    /// contributes a view to the type.
    /// </returns>
    public IReadOnlyList<DocumentViewDescriptor> ViewsFor(DocumentTypeId typeId) =>
        _viewsByType.TryGetValue(typeId, out var views)
            ? views
            : Array.Empty<DocumentViewDescriptor>();

    /// <summary>Composes the plugins and validates the contributions they declare.</summary>
    /// <param name="plugins">The plugins in declaration order.</param>
    /// <param name="services">
    /// The service collection the plugins register into, or <see langword="null"/> to collect the
    /// registrations into a throw-away collection.
    /// </param>
    /// <returns>The composed catalog; inspect <see cref="Diagnostics"/> before starting the host.</returns>
    public static LiernePluginCatalog Build(
        IReadOnlyList<ILiernePlugin> plugins,
        IServiceCollection? services = null)
    {
        ArgumentNullException.ThrowIfNull(plugins);
        var diagnostics = new List<LierneDiagnostic>();
        var ordered = OrderPlugins(plugins, diagnostics);
        var builder = new LiernePluginBuilder(services ?? new ServiceCollection());
        foreach (var plugin in ordered)
        {
            builder.BeginPlugin(plugin.Id);
            plugin.Configure(builder);
        }

        ValidateContributions(builder, diagnostics);

        return new LiernePluginCatalog(
            ordered
                .Select((plugin, order) => new LiernePluginInfo(plugin.Id, plugin.DisplayName, order))
                .ToArray(),
            builder.OwnedDocuments.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedDocumentViews.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedToolWindows.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedCommands
                .Select(contribution => contribution.Descriptor.Descriptor)
                .ToArray(),
            builder.OwnedCommands.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedCommandPlacements.Select(contribution => contribution.Descriptor).ToArray(),
            builder.CommandUpdateHandlers,
            builder.OwnedRibbonGroups.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedRibbonItems.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedSettingsPages.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedSettings.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedMenuGroups.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedMenuItems.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedStringCatalogs.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedToolbars.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedStatusBarItems.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedSettingsTransfers.Select(contribution => contribution.Descriptor).ToArray(),
            builder.OwnedShellContents.Select(contribution => contribution.Descriptor).ToArray(),
            diagnostics);
    }

    private static List<ILiernePlugin> OrderPlugins(
        IReadOnlyList<ILiernePlugin> plugins,
        List<LierneDiagnostic> diagnostics)
    {
        var byId = new Dictionary<PluginId, ILiernePlugin>();
        foreach (var plugin in plugins)
        {
            if (!byId.TryAdd(plugin.Id, plugin))
            {
                diagnostics.Add(LierneDiagnostic.Error(
                    "lattice.plugin.duplicate",
                    $"Plugin id '{plugin.Id}' is registered more than once."));
            }
        }

        var state = new Dictionary<PluginId, VisitState>();
        var ordered = new List<ILiernePlugin>(byId.Count);
        var started = new HashSet<PluginId>();

        void Visit(ILiernePlugin plugin)
        {
            var current = state.GetValueOrDefault(plugin.Id);
            if (current == VisitState.Visited)
            {
                return;
            }

            if (current == VisitState.Visiting)
            {
                diagnostics.Add(LierneDiagnostic.Error(
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
                    diagnostics.Add(LierneDiagnostic.Error(
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
        LiernePluginBuilder builder,
        List<LierneDiagnostic> diagnostics)
    {
        ValidateDuplicateIds(
            builder.OwnedDocuments,
            contribution => contribution.Id.Value,
            "Document type",
            diagnostics);
        foreach (var contribution in builder.OwnedDocuments)
        {
            if (contribution.Descriptor.StateCodec is { } codec &&
                (codec.Version < 1 || codec.Write is null || codec.Read is null))
            {
                diagnostics.Add(LierneDiagnostic.Error(
                    "lattice.document.invalid_state_codec",
                    $"The state codec for '{contribution.Descriptor.Id}' of plugin '{contribution.Plugin}' needs a positive version, a writer and a reader."));
            }
        }
        ValidateDocumentViews(builder, diagnostics);
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
        ValidateSettings(builder, diagnostics);
        ValidateStringCatalogs(builder, diagnostics);
        ValidateDuplicateIds(
            builder.OwnedToolbars,
            contribution => contribution.Id.Value,
            "Toolbar",
            diagnostics);
        ValidateDuplicateIds(
            builder.OwnedStatusBarItems,
            contribution => contribution.Id.Value,
            "Status-bar item",
            diagnostics);
        ValidateDuplicateIds(
            builder.OwnedSettingsTransfers,
            contribution => contribution.Section,
            "Settings section",
            diagnostics);
        ValidateDuplicateIds(
            builder.OwnedShellContents,
            contribution => contribution.Id.Value,
            "Shell content",
            diagnostics);

        var commands = new HashSet<string>(
            builder.OwnedCommands.Select(contribution => contribution.Descriptor.Descriptor.Id.Value),
            StringComparer.Ordinal);
        var commandsWithArguments = builder.OwnedCommands
            .Select(contribution => contribution.Descriptor.Descriptor)
            .Where(command => command.ArgumentType is not null)
            .Select(command => command.Id)
            .ToHashSet();
        ValidateCommandSurfaces(builder, commands, diagnostics);
        ValidateStatusBarItems(builder, commands, commandsWithArguments, diagnostics);
        ValidateMenus(builder, commands, commandsWithArguments, diagnostics);
        var groups = new HashSet<string>(
            builder.OwnedRibbonGroups.Select(contribution => contribution.Descriptor.Id.Value),
            StringComparer.Ordinal);
        foreach (var contribution in builder.OwnedRibbonItems)
        {
            var item = contribution.Descriptor;
            if (!commands.Contains(item.Command.Value))
            {
                diagnostics.Add(LierneDiagnostic.Error(
                    "lattice.ribbon.unknown_command",
                    $"Ribbon item '{item.Id}' of plugin '{contribution.Plugin}' invokes unknown command '{item.Command}'."));
            }
            else if (commandsWithArguments.Contains(item.Command))
            {
                diagnostics.Add(ArgumentRequired("Ribbon item", item.Id.Value, contribution.Plugin, item.Command));
            }

            if (!groups.Contains(item.Group.Value))
            {
                diagnostics.Add(LierneDiagnostic.Error(
                    "lattice.ribbon.unknown_group",
                    $"Ribbon item '{item.Id}' of plugin '{contribution.Plugin}' belongs to unknown group '{item.Group}'."));
            }
        }
    }

    private static void ValidateDocumentViews(
        LiernePluginBuilder builder,
        List<LierneDiagnostic> diagnostics)
    {
        var documents = new HashSet<string>(
            builder.OwnedDocuments.Select(contribution => contribution.Descriptor.Id.Value),
            StringComparer.Ordinal);
        var owners = new Dictionary<(string DocumentType, string View), PluginId>();
        foreach (var contribution in builder.OwnedDocumentViews)
        {
            var view = contribution.Descriptor;
            if (!documents.Contains(view.DocumentType.Value))
            {
                diagnostics.Add(LierneDiagnostic.Error(
                    "lattice.documentview.unknown_type",
                    $"Document view '{view.Id}' of plugin '{contribution.Plugin}' belongs to unknown document type '{view.DocumentType}'."));
            }

            var key = (view.DocumentType.Value, view.Id.Value);
            if (owners.TryGetValue(key, out var owner))
            {
                diagnostics.Add(LierneDiagnostic.Error(
                    "lattice.contribution.duplicate",
                    $"Document view '{view.Id}' of document type '{view.DocumentType}' is contributed by both '{owner}' and '{contribution.Plugin}'."));
                continue;
            }

            owners[key] = contribution.Plugin;
        }
    }

    private static void ValidateSettings(
        LiernePluginBuilder builder,
        List<LierneDiagnostic> diagnostics)
    {
        var pages = new HashSet<string>(
            builder.OwnedSettingsPages.Select(contribution => contribution.Descriptor.Id.Value),
            StringComparer.Ordinal);
        foreach (var contribution in builder.OwnedSettings)
        {
            var setting = contribution.Descriptor;
            if (setting.Page is { } page && !pages.Contains(page.Value))
            {
                diagnostics.Add(LierneDiagnostic.Error(
                    "lattice.settings.unknown_page",
                    $"Setting '{setting.Key}' of plugin '{contribution.Plugin}' belongs to unknown page '{page}'."));
            }

            if (!LierneSettingsStore.IsValid(setting, setting.DefaultValue, out var error))
            {
                diagnostics.Add(LierneDiagnostic.Error(
                    "lattice.settings.invalid_default",
                    $"Invalid default for '{setting.Key}' of plugin '{contribution.Plugin}'. {error}"));
            }
        }
    }

    private static void ValidateStringCatalogs(
        LiernePluginBuilder builder,
        List<LierneDiagnostic> diagnostics)
    {
        var owners = new Dictionary<string, PluginId>(StringComparer.Ordinal);
        foreach (var contribution in builder.OwnedStringCatalogs)
        {
            var catalog = contribution.Descriptor;
            if (!owners.TryAdd(catalog.Namespace, contribution.Plugin))
            {
                diagnostics.Add(LierneDiagnostic.Error(
                    "lattice.strings.duplicate_namespace",
                    $"String namespace '{catalog.Namespace}' is served by both '{owners[catalog.Namespace]}' and '{contribution.Plugin}'."));
                continue;
            }

            try
            {
                var resources = catalog.Resources.GetResourceSet(
                    CultureInfo.InvariantCulture,
                    createIfNotExists: true,
                    tryParents: false);
                if (resources is null)
                {
                    diagnostics.Add(LierneDiagnostic.Error(
                        "lattice.strings.missing_manifest",
                        $"String namespace '{catalog.Namespace}' of plugin '{contribution.Plugin}' has no neutral resource manifest."));
                }
            }
            catch (MissingManifestResourceException)
            {
                diagnostics.Add(LierneDiagnostic.Error(
                    "lattice.strings.missing_manifest",
                    $"String namespace '{catalog.Namespace}' of plugin '{contribution.Plugin}' has no neutral resource manifest."));
            }
        }
    }

    private static void ValidateStatusBarItems(
        LiernePluginBuilder builder,
        HashSet<string> commands,
        HashSet<CommandId> commandsWithArguments,
        List<LierneDiagnostic> diagnostics)
    {
        foreach (var contribution in builder.OwnedStatusBarItems)
        {
            var item = contribution.Descriptor;
            if (item.Command is { } command && !commands.Contains(command.Value))
            {
                diagnostics.Add(LierneDiagnostic.Error(
                    "lattice.statusbar.unknown_command",
                    $"Status-bar item '{item.Id}' of plugin '{contribution.Plugin}' invokes unknown command '{command}'."));
            }
            else if (item.Component is null && item.Command is { } generatedCommand &&
                     commandsWithArguments.Contains(generatedCommand))
            {
                diagnostics.Add(ArgumentRequired("Status-bar item", item.Id.Value, contribution.Plugin, generatedCommand));
            }
        }
    }

    private static void ValidateMenus(
        LiernePluginBuilder builder,
        HashSet<string> commands,
        HashSet<CommandId> commandsWithArguments,
        List<LierneDiagnostic> diagnostics)
    {
        ValidateDuplicateIds(
            builder.OwnedMenuGroups,
            contribution => contribution.Id.Value,
            "Menu group",
            diagnostics);
        var groups = new HashSet<string>(
            builder.OwnedMenuGroups.Select(contribution => contribution.Descriptor.Id.Value),
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
                diagnostics.Add(LierneDiagnostic.Error(
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
                diagnostics.Add(LierneDiagnostic.Error(
                    "lattice.menu.unknown_group",
                    $"Menu item '{item.Id}' of plugin '{entry.Plugin}' belongs to unknown group '{item.Group}'."));
            }

            if (item.Command is { } command)
            {
                if (!commands.Contains(command.Value))
                {
                    diagnostics.Add(LierneDiagnostic.Error(
                        "lattice.menu.unknown_command",
                        $"Menu item '{item.Id}' of plugin '{entry.Plugin}' invokes unknown command '{command}'."));
                }
                else if (item.Children is not { Count: > 0 } && commandsWithArguments.Contains(command))
                {
                    diagnostics.Add(ArgumentRequired("Menu item", item.Id.Value, entry.Plugin, command));
                }
            }
            else if (item.Children is null || item.Children.Count == 0)
            {
                diagnostics.Add(LierneDiagnostic.Error(
                    "lattice.menu.invalid_item",
                    $"Menu item '{item.Id}' of plugin '{entry.Plugin}' has no command and no children."));
            }
        }
    }

    private static void ValidateCommandSurfaces(
        LiernePluginBuilder builder,
        HashSet<string> commands,
        List<LierneDiagnostic> diagnostics)
    {
        foreach (var contribution in builder.OwnedCommandPlacements)
        {
            var placement = contribution.Descriptor;
            if (!commands.Contains(placement.Command.Value))
            {
                diagnostics.Add(LierneDiagnostic.Error(
                    "lattice.commandplacement.unknown_command",
                    $"Command placement of plugin '{contribution.Plugin}' references unknown command '{placement.Command}'."));
            }

            if (placement.Anchor is CommandPlacementAnchor.Before or CommandPlacementAnchor.After)
            {
                if (placement.RelativeTo is not { } relativeTo)
                {
                    diagnostics.Add(LierneDiagnostic.Error(
                        "lattice.commandplacement.missing_relative",
                        $"Command placement of plugin '{contribution.Plugin}' uses {placement.Anchor} without a relative command."));
                }
                else if (!commands.Contains(relativeTo.Value))
                {
                    diagnostics.Add(LierneDiagnostic.Error(
                        "lattice.commandplacement.unknown_relative",
                        $"Command placement of plugin '{contribution.Plugin}' references unknown relative command '{relativeTo}'."));
                }
            }
        }

        var handled = new Dictionary<CommandId, PluginId>();
        foreach (var contribution in builder.OwnedCommandUpdateHandlers)
        {
            var command = contribution.Descriptor.Command;
            if (!commands.Contains(command.Value))
            {
                diagnostics.Add(LierneDiagnostic.Error(
                    "lattice.commandupdate.unknown_command",
                    $"Command update handler of plugin '{contribution.Plugin}' references unknown command '{command}'."));
            }
            else if (handled.TryGetValue(command, out var existing))
            {
                diagnostics.Add(LierneDiagnostic.Error(
                    "lattice.commandupdate.duplicate",
                    $"Command '{command}' has update handlers in plugins '{existing}' and '{contribution.Plugin}'."));
            }
            else
            {
                handled.Add(command, contribution.Plugin);
            }
        }
    }

    private static LierneDiagnostic ArgumentRequired(string kind, string id, PluginId plugin, CommandId command) =>
        LierneDiagnostic.Error(
            "lattice.command.argument_required",
            $"{kind} '{id}' of plugin '{plugin}' cannot supply the required argument for command '{command}'.");

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
        List<LierneDiagnostic> diagnostics)
    {
        var owners = new Dictionary<string, PluginId>(StringComparer.Ordinal);
        foreach (var contribution in contributions)
        {
            var identifier = id(contribution.Descriptor);
            if (owners.TryGetValue(identifier, out var owner))
            {
                diagnostics.Add(LierneDiagnostic.Error(
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
