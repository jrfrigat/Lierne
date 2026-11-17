using Flare.Components.IDE;
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

    /// <summary>Gets or sets the title of the ribbon tab.</summary>
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

    private bool HasLeftRegion =>
        Documents.Documents.Any(document => document.Location == LatticeDockLocation.Left) ||
        HasVisibleTool(LatticeToolRegion.Left);

    private bool HasRightRegion =>
        Documents.Documents.Any(document => document.Location == LatticeDockLocation.Right) ||
        HasVisibleTool(LatticeToolRegion.Right);

    private RenderFragment LeftTools => ToolWindowsFor(LatticeToolRegion.Left);

    private RenderFragment RightTools => ToolWindowsFor(LatticeToolRegion.Right);

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

    private RenderFragment ToolWindowsFor(LatticeToolRegion region) => builder =>
    {
        var descriptors = Catalog.ToolWindows
            .Where(descriptor =>
                descriptor.DefaultRegion == region && ToolWindows.IsVisible(descriptor.Id))
            .OrderBy(descriptor => descriptor.Title.ToString(), StringComparer.Ordinal)
            .ThenBy(descriptor => descriptor.Id.Value, StringComparer.Ordinal)
            .ToArray();
        if (descriptors.Length == 0)
        {
            return;
        }

        builder.OpenComponent<FlareToolPanelGroup>(0);
        builder.AddAttribute(
            1,
            nameof(FlareToolPanelGroup.Position),
            region == LatticeToolRegion.Left ? ToolPanelPosition.Left : ToolPanelPosition.Right);
        builder.AddAttribute(2, nameof(FlareToolPanelGroup.ChildContent), (RenderFragment)(content =>
        {
            var sequence = 0;
            foreach (var descriptor in descriptors)
            {
                content.OpenComponent<FlareToolPanel>(sequence++);
                content.AddAttribute(sequence++, nameof(FlareToolPanel.Key), descriptor.Id.Value);
                content.AddAttribute(sequence++, nameof(FlareToolPanel.Title), Localizer.Get(descriptor.Title));
                if (Icons.Resolve(descriptor.IconKey) is { } icon)
                {
                    content.AddAttribute(sequence++, nameof(FlareToolPanel.Icon), icon);
                }

                content.AddAttribute(sequence++, nameof(FlareToolPanel.ChildContent), (RenderFragment)(body =>
                {
                    body.OpenComponent<DynamicComponent>(0);
                    body.AddAttribute(1, nameof(DynamicComponent.Type), descriptor.Component);
                    body.AddAttribute(2, nameof(DynamicComponent.Parameters), ToolWindowParameters(descriptor.Id));
                    body.CloseComponent();
                }));
                content.CloseComponent();
            }
        }));
        builder.CloseComponent();
    };

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
