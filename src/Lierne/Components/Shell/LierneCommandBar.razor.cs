using Flare.Components.IDE;
using Microsoft.AspNetCore.Components;

namespace Lierne;

/// <summary>Projects command placements onto the menu, ribbon and toolbars of the shell.</summary>
public partial class LierneCommandBar
{
    private IReadOnlyList<LierneMenuGroupView>? _menuGroups;
    private IReadOnlyList<LierneRibbonGroupView>? _ribbonGroups;
    private IReadOnlyList<IdeToolbarGroup>? _toolbarGroups;

    [Inject] private LiernePluginCatalog Catalog { get; set; } = default!;
    [Inject] private ICommandPlacementService Placements { get; set; } = default!;
    [Inject] private ILierneCommandService Commands { get; set; } = default!;
    [Inject] private ILierneLocalizer Localizer { get; set; } = default!;
    [Inject] private LierneNotificationService Notifications { get; set; } = default!;
    [Inject] private LiernePaletteService Palette { get; set; } = default!;
    [Inject] private IServiceProvider Services { get; set; } = default!;

    /// <summary>Gets or sets the title of the ribbon tab.</summary>
    [Parameter]
    public string RibbonTitle { get; set; } = "Home";

    /// <summary>Gets or sets the host menu bar, which replaces the generated menu.</summary>
    [Parameter]
    public RenderFragment? MenuBar { get; set; }

    /// <summary>Gets or sets whether the generated menu bar is shown.</summary>
    [Parameter]
    public bool ShowMenuBar { get; set; } = true;

    /// <summary>Gets or sets whether the ribbon is shown when no toolbar takes its place.</summary>
    [Parameter]
    public bool ShowRibbon { get; set; } = true;

    /// <summary>Gets or sets whether contributed toolbars are shown.</summary>
    [Parameter]
    public bool ShowToolbars { get; set; } = true;

    /// <summary>Gets or sets whether the command-palette button is shown.</summary>
    [Parameter]
    public bool ShowPaletteButton { get; set; } = true;

    private IReadOnlyList<LierneMenuGroupView> MenuGroups =>
        _menuGroups ??= BuildMenuGroups();

    private IReadOnlyList<LierneRibbonGroupView> RibbonGroups =>
        _ribbonGroups ??= BuildRibbonGroups();

    private IReadOnlyList<ToolbarDescriptor> Toolbars =>
        Catalog.Toolbars
            .OrderBy(toolbar => toolbar.Order)
            .ThenBy(toolbar => toolbar.Id.Value, StringComparer.Ordinal)
            .ToArray();

    private IReadOnlyList<IdeToolbarGroup> ToolbarGroups => _toolbarGroups ??= BuildToolbarGroups();

    private LierneToolbarContext ToolbarContext => new(Commands, Localizer, Services);

    private IReadOnlyList<LierneMenuGroupView> BuildMenuGroups()
    {
        var placements = Placements.ForSurface(CommandSurface.Menu)
            .GroupBy(placement => placement.Group, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<CommandPlacementDescriptor>)group.ToArray(),
                StringComparer.Ordinal);
        var groups = new List<LierneMenuGroupView>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var descriptor in Catalog.MenuGroups
                     .OrderBy(group => group.Order)
                     .ThenBy(group => group.Id.Value, StringComparer.Ordinal))
        {
            var commands = placements.GetValueOrDefault(descriptor.Id.Value) ?? [];
            var legacy = FilterLegacy(
                Catalog.MenuItems
                    .Where(item => item.Group == descriptor.Id)
                    .OrderBy(item => item.Order)
                    .ThenBy(item => item.Id.Value, StringComparer.Ordinal)
                    .ToArray(),
                commands.Select(command => command.Command).ToHashSet());
            if (commands.Count == 0 && legacy.Count == 0)
            {
                continue;
            }

            groups.Add(new LierneMenuGroupView(descriptor, commands, legacy));
            used.Add(descriptor.Id.Value);
        }

        foreach (var (group, commands) in placements)
        {
            if (used.Contains(group))
            {
                continue;
            }

            groups.Add(new LierneMenuGroupView(
                new MenuGroupDescriptor(new MenuGroupId(group), StringKey.Literal(group), 10000),
                commands,
                []));
        }

        return groups
            .OrderBy(group => group.Group.Order)
            .ThenBy(group => group.Group.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private IReadOnlyList<LierneRibbonGroupView> BuildRibbonGroups()
    {
        var placements = Placements.ForSurface(CommandSurface.Ribbon)
            .GroupBy(placement => placement.Group, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<CommandPlacementDescriptor>)group.ToArray(),
                StringComparer.Ordinal);
        var groups = new List<LierneRibbonGroupView>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var descriptor in Catalog.RibbonGroups
                     .OrderBy(group => group.Order)
                     .ThenBy(group => group.Id.Value, StringComparer.Ordinal))
        {
            var commands = placements.GetValueOrDefault(descriptor.Id.Value) ?? [];
            var placed = commands.Select(command => command.Command).ToHashSet();
            var legacy = Catalog.RibbonItems
                .Where(item => item.Group == descriptor.Id && !placed.Contains(item.Command))
                .OrderBy(item => item.Order)
                .ThenBy(item => item.Id.Value, StringComparer.Ordinal)
                .Select(item => new CommandPlacementDescriptor(
                    item.Command,
                    CommandSurface.Ribbon,
                    descriptor.Id.Value,
                    item.Order,
                    IconOverride: item.IconKey))
                .ToArray();
            if (commands.Count == 0 && legacy.Length == 0)
            {
                continue;
            }

            groups.Add(new LierneRibbonGroupView(
                descriptor,
                commands.Concat(legacy).ToArray()));
            used.Add(descriptor.Id.Value);
        }

        foreach (var (group, commands) in placements)
        {
            if (used.Contains(group))
            {
                continue;
            }

            groups.Add(new LierneRibbonGroupView(
                new RibbonGroupDescriptor(new RibbonGroupId(group), StringKey.Literal(group), 10000),
                commands));
        }

        return groups
            .OrderBy(group => group.Group.Order)
            .ThenBy(group => group.Group.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private IReadOnlyList<IdeToolbarGroup> BuildToolbarGroups()
    {
        var groups = Placements.ForSurface(CommandSurface.Toolbar)
            .GroupBy(placement => placement.Group, StringComparer.Ordinal)
            .OrderBy(group => group.Min(placement => placement.Order))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();
        if (groups.Length == 0)
        {
            return [];
        }

        return groups
            .Select(group => new IdeToolbarGroup(
                group.Key,
                group.Key,
                BuildToolbarGroupContent(group.ToArray())))
            .ToArray();
    }

    private RenderFragment BuildToolbarGroupContent(IReadOnlyList<CommandPlacementDescriptor> placements) =>
        builder =>
        {
            var sequence = 0;
            foreach (var placement in placements)
            {
                builder.OpenComponent<LierneCommandToolbarButton>(sequence++);
                builder.AddAttribute(sequence++, nameof(LierneCommandToolbarButton.Command), placement.Command);
                builder.AddAttribute(sequence++, nameof(LierneCommandToolbarButton.Placement), placement);
                builder.AddAttribute(sequence++, nameof(LierneCommandToolbarButton.ExecuteCommand),
                    EventCallback.Factory.Create<CommandId>(this, ExecuteCommandAsync));
                builder.CloseComponent();
            }
        };

    private static IReadOnlyList<MenuItemDescriptor> FilterLegacy(
        IReadOnlyList<MenuItemDescriptor> items,
        IReadOnlySet<CommandId> placed)
    {
        var result = new List<MenuItemDescriptor>();
        foreach (var item in items)
        {
            if (item.Children is { Count: > 0 } children)
            {
                var filtered = FilterLegacy(children, placed);
                if (filtered.Count > 0)
                {
                    result.Add(item with { Children = filtered });
                }
            }
            else if (item.IsSeparator)
            {
                if (result.Count > 0)
                {
                    result.Add(item);
                }
            }
            else if (item.Command is not { } command || !placed.Contains(command))
            {
                result.Add(item);
            }
        }

        while (result.Count > 0 && result[^1].IsSeparator)
        {
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }

    private Task ExecuteCommandAsync(CommandId command) => ExecuteAsync(command);

    private async Task ExecuteAsync(CommandId command)
    {
        var result = await Commands.ExecuteAsync(command);
        if (!result.Success)
        {
            Notifications.NotifyError(result.ErrorCode, result.ErrorMessage);
        }
    }

    private void OpenPalette() => Palette.Open();
}
