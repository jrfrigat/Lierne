using Flare.Components.IDE;
using Flare.Icons;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Lattice;

/// <summary>Composes the Flare IDE layout from the Lattice plugin catalog.</summary>
public partial class LatticeShell : IDisposable
{
    private readonly Dictionary<IdeDocumentRegion, string?> _selection = new()
    {
        [IdeDocumentRegion.Center] = null,
        [IdeDocumentRegion.Left] = null,
        [IdeDocumentRegion.Right] = null
    };

    private string? _lastActiveId;
    private bool _leftPinned = true;
    private bool _rightPinned = true;

    /// <summary>Gets or sets the title of the ribbon tab used when no toolbar is contributed.</summary>
    [Parameter]
    public string RibbonTitle { get; set; } = "Home";

    /// <summary>Gets or sets the host menu bar content.</summary>
    [Parameter]
    public RenderFragment? MenuBar { get; set; }

    /// <summary>Gets or sets the host status bar content.</summary>
    [Parameter]
    public RenderFragment? StatusBar { get; set; }

    /// <summary>Gets or sets the host bottom panel content.</summary>
    [Parameter]
    public RenderFragment? BottomPanel { get; set; }

    /// <summary>Gets or sets the content shown when no document is active.</summary>
    [Parameter]
    public RenderFragment? StartContent { get; set; }

    private IReadOnlyList<IdeDocument> IdeDocuments =>
        Documents.Documents
            .Select(document => new IdeDocument(
                document.Id,
                Localizer.Get(document.Title),
                ToRegion(document.Location),
                Closeable: true))
            .ToArray();

    private IReadOnlyDictionary<IdeDocumentRegion, string?> ActiveDocuments =>
        new Dictionary<IdeDocumentRegion, string?>(_selection);

    private IReadOnlyList<RibbonGroupDescriptor> RibbonGroups =>
        Catalog.RibbonGroups
            .OrderBy(group => group.Order)
            .ThenBy(group => group.Id.Value, StringComparer.Ordinal)
            .ToArray();

    private IReadOnlyList<MenuGroupDescriptor> MenuGroups =>
        Catalog.MenuGroups
            .OrderBy(group => group.Order)
            .ThenBy(group => group.Id.Value, StringComparer.Ordinal)
            .ToArray();

    private bool HasLeftRegion =>
        Documents.Documents.Any(document => document.Location == LatticeDockLocation.Left) ||
        HasVisibleTool(LatticeToolRegion.Left);

    private bool HasRightRegion =>
        Documents.Documents.Any(document => document.Location == LatticeDockLocation.Right) ||
        HasVisibleTool(LatticeToolRegion.Right);

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Documents.Changed += OnDocumentsChanged;
        ToolWindows.Changed += OnStateChanged;
        Localizer.Changed += OnStateChanged;
        State.Changed += OnStateChanged;
        SyncSelection();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Documents.Changed -= OnDocumentsChanged;
        ToolWindows.Changed -= OnStateChanged;
        Localizer.Changed -= OnStateChanged;
        State.Changed -= OnStateChanged;
    }

    private IEnumerable<RibbonItemDescriptor> ItemsFor(RibbonGroupId group) =>
        Catalog.RibbonItems
            .Where(item => item.Group == group)
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Id.Value, StringComparer.Ordinal);

    private IEnumerable<MenuItemDescriptor> ItemsFor(MenuGroupId group) =>
        Catalog.MenuItems
            .Where(item => item.Group == group)
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Id.Value, StringComparer.Ordinal);

    private CommandDescriptor? FindCommand(CommandId command) =>
        Catalog.Commands.FirstOrDefault(descriptor => descriptor.Id == command);

    private string CommandLabel(CommandId command) =>
        FindCommand(command) is { } descriptor ? Localizer.Get(descriptor.Title) : command.Value;

    private Task ExecuteAsync(CommandId command) => Commands.ExecuteAsync(command);

    private RenderFragment RenderDocument(IdeDocument document) => builder =>
    {
        var instance = Documents.Documents.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, document.Key, StringComparison.Ordinal));
        var descriptor = instance is null
            ? null
            : Catalog.Documents.FirstOrDefault(candidate => candidate.Id == instance.TypeId);
        if (instance is null || descriptor is null)
        {
            return;
        }

        builder.OpenComponent<DynamicComponent>(0);
        builder.AddAttribute(1, nameof(DynamicComponent.Type), descriptor.Component);
        builder.AddAttribute(2, nameof(DynamicComponent.Parameters), DocumentParameters(instance));
        builder.CloseComponent();
    };

    private Dictionary<string, object?> DocumentParameters(LatticeDocumentInstance document) => new()
    {
        [nameof(LatticeDocumentComponent.Context)] = new LatticeDocumentContext(
            document,
            Commands,
            Documents,
            Localizer,
            Services)
    };

    private IReadOnlyList<ToolWindowDescriptor> VisibleToolWindows(LatticeToolRegion region) =>
        Catalog.ToolWindows
            .Where(descriptor =>
                descriptor.DefaultRegion == region && ToolWindows.IsVisible(descriptor.Id))
            .OrderBy(descriptor => descriptor.Title.ToString(), StringComparer.Ordinal)
            .ThenBy(descriptor => descriptor.Id.Value, StringComparer.Ordinal)
            .ToArray();

    private Dictionary<string, object?> ToolWindowParameters(ToolWindowId id) => new()
    {
        [nameof(LatticeToolWindowComponent.Context)] = new LatticeToolWindowContext(
            ToolWindows.ToolWindows.First(window => window.Id == id),
            Commands,
            ToolWindows,
            Localizer,
            Services)
    };

    private bool HasVisibleTool(LatticeToolRegion region) =>
        Catalog.ToolWindows.Any(descriptor =>
            descriptor.DefaultRegion == region && ToolWindows.IsVisible(descriptor.Id));

    private bool IsPinned(LatticeToolRegion region) =>
        region == LatticeToolRegion.Right ? _rightPinned : _leftPinned;

    private FlareIcon PinIcon(LatticeToolRegion region) =>
        Icons.Resolve(IsPinned(region) ? "pin" : "unpin") ?? FlareIcons.PushPin;

    private IdePanelMode PanelMode(LatticeToolRegion region) =>
        IsPinned(region) ? IdePanelMode.Docked : IdePanelMode.AutoHide;

    private string HostLabel(string name) => Localizer.Get(new StringKey("lattice", name));

    private void TogglePin(LatticeToolRegion region)
    {
        if (region == LatticeToolRegion.Right)
        {
            _rightPinned = !_rightPinned;
        }
        else
        {
            _leftPinned = !_leftPinned;
        }
    }

    private void CloseRegion(LatticeToolRegion region)
    {
        foreach (var descriptor in Catalog.ToolWindows
                     .Where(descriptor => descriptor.DefaultRegion == region))
        {
            if (ToolWindows.IsVisible(descriptor.Id))
            {
                ToolWindows.Hide(descriptor.Id);
            }
        }
    }

    private void OnActiveDocumentChanged(IdeDocumentSelection selection)
    {
        _selection[selection.Region] = selection.Key;
        if (!string.IsNullOrWhiteSpace(selection.Key))
        {
            Documents.Activate(selection.Key);
        }
    }

    private void OnDocumentCloseRequested(IdeDocument document) => Documents.Close(document.Key);

    private void OnDocumentMove(IdeDocumentMove move) =>
        Documents.Move(move.Document.Key, ToLocation(move.Destination));

    private void OnDocumentsChanged()
    {
        if (!string.Equals(Documents.Active?.Id, _lastActiveId, StringComparison.Ordinal))
        {
            SyncSelection();
        }

        _ = InvokeAsync(StateHasChanged);
    }

    private void OnStateChanged() => _ = InvokeAsync(StateHasChanged);

    private void SyncSelection()
    {
        _selection[IdeDocumentRegion.Center] =
            Documents.Active is { Location: LatticeDockLocation.Main } center ? center.Id : null;
        _selection[IdeDocumentRegion.Left] =
            Documents.Active is { Location: LatticeDockLocation.Left } left ? left.Id : null;
        _selection[IdeDocumentRegion.Right] =
            Documents.Active is { Location: LatticeDockLocation.Right } right ? right.Id : null;
        _lastActiveId = Documents.Active?.Id;
    }

    private static IdeDocumentRegion ToRegion(LatticeDockLocation location) =>
        location switch
        {
            LatticeDockLocation.Left => IdeDocumentRegion.Left,
            LatticeDockLocation.Right => IdeDocumentRegion.Right,
            _ => IdeDocumentRegion.Center
        };

    private static LatticeDockLocation ToLocation(IdeDocumentRegion region) =>
        region switch
        {
            IdeDocumentRegion.Left => LatticeDockLocation.Left,
            IdeDocumentRegion.Right => LatticeDockLocation.Right,
            _ => LatticeDockLocation.Main
        };
}
