using Flare.Components;
using Flare.Components.IDE;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Lierne;

/// <summary>One item projected into a status-bar area.</summary>
internal sealed record LierneStatusItemView(
    int Order,
    LierneStatusBarAlignment Alignment,
    CommandId? Command,
    CommandPlacementDescriptor? Placement,
    StatusBarItemDescriptor? Legacy);

/// <summary>Projects command placements and legacy items onto the status bar.</summary>
public partial class LierneStatusBar
{
    private IReadOnlyList<LierneStatusItemView>? _items;

    [Inject] private LiernePluginCatalog Catalog { get; set; } = default!;
    [Inject] private ICommandPlacementService Placements { get; set; } = default!;
    [Inject] private ILierneCommandService Commands { get; set; } = default!;
    [Inject] private ILierneLocalizer Localizer { get; set; } = default!;
    [Inject] private ILierneIconResolver Icons { get; set; } = default!;
    [Inject] private LierneNotificationService Notifications { get; set; } = default!;
    [Inject] private IServiceProvider Services { get; set; } = default!;

    /// <summary>Gets or sets the host status bar, which replaces the generated one.</summary>
    [Parameter]
    public RenderFragment? Content { get; set; }

    private IReadOnlyList<LierneStatusItemView> Items => _items ??= BuildItems();

    private LierneStatusBarContext StatusContext => new(Commands, Localizer, Services);

    private IReadOnlyList<LierneStatusItemView> BuildItems()
    {
        var placements = Placements.ForSurface(CommandSurface.StatusBar);
        var placed = placements.Select(placement => placement.Command).ToHashSet();
        var items = new List<LierneStatusItemView>();
        foreach (var placement in placements)
        {
            items.Add(new LierneStatusItemView(
                placement.Order,
                Alignment(placement.Group),
                placement.Command,
                placement,
                null));
        }

        foreach (var legacy in Catalog.StatusBarItems
                     .OrderBy(item => item.Order)
                     .ThenBy(item => item.Id.Value, StringComparer.Ordinal))
        {
            if (legacy.Command is { } command && placed.Contains(command))
            {
                continue;
            }

            items.Add(new LierneStatusItemView(
                legacy.Order,
                legacy.Alignment,
                legacy.Command,
                legacy.Command is { } placementCommand
                    ? new CommandPlacementDescriptor(
                        placementCommand,
                        CommandSurface.StatusBar,
                        Alignment(legacy.Alignment).ToString(),
                        TitleOverride: legacy.Title,
                        IconOverride: legacy.IconKey)
                    : null,
                legacy));
        }

        return items;
    }

    private static LierneStatusBarAlignment Alignment(string? group) => group?.ToLowerInvariant() switch
    {
        "center" => LierneStatusBarAlignment.Center,
        "right" => LierneStatusBarAlignment.Right,
        _ => LierneStatusBarAlignment.Left
    };

    private static string Alignment(LierneStatusBarAlignment alignment) => alignment.ToString();

    private IReadOnlyList<LierneStatusItemView> For(LierneStatusBarAlignment alignment) => Items
        .Where(item => item.Alignment == alignment)
        .OrderBy(item => item.Order)
        .ToArray();

    private RenderFragment RenderArea(LierneStatusBarAlignment alignment) => builder =>
    {
        var sequence = 0;
        foreach (var item in For(alignment))
        {
            if (item.Legacy?.Component is { } component)
            {
                builder.OpenComponent<DynamicComponent>(sequence++);
                builder.AddAttribute(sequence++, nameof(DynamicComponent.Type), component);
                builder.AddAttribute(sequence++, nameof(DynamicComponent.Parameters),
                    new Dictionary<string, object?>
                    {
                        [nameof(LierneStatusBarComponent.Context)] = StatusContext
                    });
                builder.CloseComponent();
            }
            else if (item.Command is { } command)
            {
                builder.OpenComponent<LierneCommandToolbarButton>(sequence++);
                builder.AddAttribute(sequence++, nameof(LierneCommandToolbarButton.Command), command);
                builder.AddAttribute(sequence++, nameof(LierneCommandToolbarButton.Placement), item.Placement);
                builder.AddAttribute(sequence++, nameof(LierneCommandToolbarButton.ShowLabel), true);
                builder.AddAttribute(sequence++, nameof(LierneCommandToolbarButton.Size), ButtonSize.Xs);
                builder.AddAttribute(sequence++, nameof(LierneCommandToolbarButton.Class), "lierne-statusbar__item");
                builder.AddAttribute(sequence++, nameof(LierneCommandToolbarButton.ExecuteCommand),
                    EventCallback.Factory.Create<CommandId>(this, ExecuteCommandAsync));
                builder.CloseComponent();
            }
            else if (item.Legacy is { } legacy)
            {
                builder.OpenElement(sequence++, "span");
                builder.AddAttribute(sequence++, "class", "lierne-statusbar__item");
                if (Icons.Resolve(legacy.IconKey) is { } icon)
                {
                    builder.OpenComponent<FlareIconView>(sequence++);
                    builder.AddAttribute(sequence++, nameof(FlareIconView.Value), icon);
                    builder.CloseComponent();
                }

                builder.AddContent(sequence++, Localizer.Get(legacy.Title));
                builder.CloseElement();
            }
        }
    };

    private Task ExecuteCommandAsync(CommandId command) => ExecuteAsync(command);

    private async Task ExecuteAsync(CommandId command)
    {
        var result = await Commands.ExecuteAsync(command);
        if (!result.Success)
        {
            Notifications.NotifyError(result.ErrorCode, result.ErrorMessage);
        }
    }
}
