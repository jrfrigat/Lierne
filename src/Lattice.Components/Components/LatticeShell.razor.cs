using Flare.Components;
using Flare.Components.IDE;
using Flare.Icons;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Lattice;

/// <summary>Composes the Flare IDE layout from the Lattice plugin catalog.</summary>
public partial class LatticeShell : IDisposable, IAsyncDisposable
{
    private readonly Dictionary<IdeDocumentRegion, string?> _selection = new()
    {
        [IdeDocumentRegion.Center] = null,
        [IdeDocumentRegion.Left] = null,
        [IdeDocumentRegion.Right] = null
    };

    private string? _lastActiveId;
    private LatticeDockLocation? _lastActiveLocation;
    private LatticeCommandResult? _commandFailure;

    /// <summary>Gets or sets the title of the ribbon tab used when no toolbar is contributed.</summary>
    [Parameter]
    public string RibbonTitle { get; set; } = "Home";

    /// <summary>Gets or sets the host menu bar content.</summary>
    [Parameter]
    public RenderFragment? MenuBar { get; set; }

    /// <summary>Gets or sets the host status bar content.</summary>
    [Parameter]
    public RenderFragment? StatusBar { get; set; }

    /// <summary>
    /// Gets or sets the host bottom panel content. A <see cref="LatticeShellSlot.BottomPanel"/>
    /// contribution takes precedence.
    /// </summary>
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

    private IReadOnlyList<LatticeDocumentInstance> MainDocuments =>
        Documents.Documents
            .Where(document => document.Location == LatticeDockLocation.Main)
            .ToArray();

    private string? ActiveCenterKey => _selection[IdeDocumentRegion.Center];

    private IReadOnlyList<FlareDocumentDockTarget> CenterDockTargets =>
    [
        new(nameof(IdeDocumentRegion.Left), HostLabel("dockLeft")),
        new(nameof(IdeDocumentRegion.Right), HostLabel("dockRight"))
    ];

    private FlareIcon? DocumentIcon(LatticeDocumentInstance document)
    {
        var icon = Icons.Resolve(document.IconKey ?? Catalog.Documents
            .FirstOrDefault(descriptor => descriptor.Id == document.TypeId)?
            .IconKey);
        return icon is not null && !string.IsNullOrWhiteSpace(document.IconColor)
            ? icon with { Color = FlareColor.Custom(document.IconColor) }
            : icon;
    }

    private void OnTabActivated(string key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            OnActiveDocumentChanged(new IdeDocumentSelection(IdeDocumentRegion.Center, key));
        }
    }

    private void OnTabDockRequested(FlareDocumentDockRequest request)
    {
        var location = request.Target.Key == nameof(IdeDocumentRegion.Left)
            ? LatticeDockLocation.Left
            : LatticeDockLocation.Right;
        MoveDocument(request.DocumentKey, location);
    }

    private void OnTabCloseRequested(string key) => Documents.Close(key);

    private void CloseTab(string key) => Documents.Close(key);

    private void CloseAllTabs()
    {
        foreach (var document in MainDocuments.ToArray())
        {
            Documents.Close(document.Id);
        }
    }

    private void CloseOtherTabs(string key)
    {
        foreach (var document in MainDocuments
                     .Where(document => !string.Equals(document.Id, key, StringComparison.Ordinal))
                     .ToArray())
        {
            Documents.Close(document.Id);
        }
    }

    private IReadOnlyList<RibbonGroupDescriptor> RibbonGroups =>
        Catalog.RibbonGroups
            .OrderBy(group => group.Order)
            .ThenBy(group => group.Id.Value, StringComparer.Ordinal)
            .ToArray();

    private ShellContentDescriptor? ShellContent(LatticeShellSlot slot) =>
        Catalog.ShellContents
            .Where(content => content.Slot == slot)
            .OrderBy(content => content.Order)
            .ThenBy(content => content.Id.Value, StringComparer.Ordinal)
            .FirstOrDefault();

    private RenderFragment? StartSurfaceContent =>
        ShellContent(LatticeShellSlot.StartPage) is { } page
            ? RenderComponent(page.Component)
            : StartContent;

    private RenderFragment? BottomHeaderSurface =>
        ShellContent(LatticeShellSlot.BottomHeader) is { } header
            ? RenderComponent(header.Component)
            : null;

    private RenderFragment? CustomBottomPanel =>
        ShellContent(LatticeShellSlot.BottomPanel) is { } panel
            ? RenderComponent(panel.Component)
            : BottomPanel;

    private RenderFragment? StatusBarSurface =>
        ShellContent(LatticeShellSlot.StatusBar) is { } status
            ? RenderComponent(status.Component)
            : StatusBar ?? (Catalog.StatusBarItems.Count > 0 ? DefaultStatusBar : null);

    private static RenderFragment RenderComponent(Type component) => builder =>
    {
        builder.OpenComponent<DynamicComponent>(0);
        builder.AddAttribute(1, nameof(DynamicComponent.Type), component);
        builder.CloseComponent();
    };

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
        ToolWindows.Changed += OnToolWindowsChanged;
        Localizer.Changed += OnStateChanged;
        State.Changed += OnStateChanged;
        Shortcuts.Changed += OnShortcutsChanged;
        InitializePersistence();
        InitializeDocumentSelection();
    }

    private void InitializeDocumentSelection()
    {
        foreach (var document in Documents.Documents)
        {
            _selection[ToRegion(document.Location)] = document.Id;
        }

        _lastActiveId = null;
        SyncSelection();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        Documents.Changed -= OnDocumentsChanged;
        ToolWindows.Changed -= OnToolWindowsChanged;
        Localizer.Changed -= OnStateChanged;
        State.Changed -= OnStateChanged;
        Shortcuts.Changed -= OnShortcutsChanged;
        if (_settings is not null)
        {
            _settings.Changed -= OnSettingChanged;
        }
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

    private async Task ExecuteAsync(CommandId command)
    {
        var result = await Commands.ExecuteAsync(command);
        _commandFailure = result.Success ? null : result;
    }

    private void DismissCommandFailure() => _commandFailure = null;

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

    private bool IsPinned(LatticeToolRegion region) => _panels[region].Pinned;

    private FlareIcon PinIcon(LatticeToolRegion region) =>
        Icons.Resolve(IsPinned(region) ? "pin" : "unpin") ?? FlareIcons.PushPin;

    private IdePanelMode PanelMode(LatticeToolRegion region) =>
        IsPinned(region) ? IdePanelMode.Docked : IdePanelMode.AutoHide;

    private string HostLabel(string name) => Localizer.Get(new StringKey("lattice", name));

    private void OnBottomPinnedChanged(bool pinned) =>
        SetPanel(LatticeToolRegion.Bottom, _panels[LatticeToolRegion.Bottom] with { Pinned = pinned });

    private void TogglePin(LatticeToolRegion region) => SetPanel(region, _panels[region] with { Pinned = !IsPinned(region) });

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

        SaveWorkspace();
    }

    private void OnDocumentCloseRequested(IdeDocument document) => Documents.Close(document.Key);

    private void OnDocumentMove(IdeDocumentMove move) =>
        MoveDocument(move.Document.Key, ToLocation(move.Destination));

    private void MoveDocument(string key, LatticeDockLocation location)
    {
        if (Documents.Move(key, location).Success)
        {
            OnActiveDocumentChanged(new IdeDocumentSelection(ToRegion(location), key));
        }
    }

    private void OnDocumentsChanged() => _ = InvokeAsync(() =>
    {
        SyncSelection();
        SaveWorkspace();
        StateHasChanged();
    });

    private void OnStateChanged() => _ = InvokeAsync(StateHasChanged);

    private void SyncSelection()
    {
        var documents = Documents.Documents;
        foreach (var (region, key) in _selection)
        {
            var location = ToLocation(region);
            if (key is not null && !documents.Any(document => document.Id == key && document.Location == location))
            {
                _selection[region] = documents.LastOrDefault(document => document.Location == location)?.Id;
            }
        }

        var active = Documents.Active;
        if (active is not null && (active.Id != _lastActiveId || active.Location != _lastActiveLocation))
        {
            _selection[ToRegion(active.Location)] = active.Id;
        }

        _lastActiveId = active?.Id;
        _lastActiveLocation = active?.Location;
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
