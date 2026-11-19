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
    private IReadOnlyList<IdeDocument>? _ideDocuments;
    private IReadOnlyList<LatticeDocumentInstance>? _mainDocuments;
    private IReadOnlyDictionary<IdeDocumentRegion, string?>? _activeDocuments;
    private IReadOnlyList<RibbonGroupDescriptor>? _ribbonGroups;
    private IReadOnlyList<MenuGroupDescriptor>? _menuGroups;
    private ILookup<RibbonGroupId, RibbonItemDescriptor>? _ribbonItems;
    private ILookup<MenuGroupId, MenuItemDescriptor>? _menuItems;
    private ILookup<LatticeToolRegion, ToolWindowDescriptor>? _toolDescriptors;
    private Dictionary<LatticeShellSlot, ShellContentDescriptor>? _shellContent;
    private Dictionary<CommandId, CommandDescriptor>? _commandDescriptors;
    private Dictionary<DocumentTypeId, DocumentDescriptor>? _documentDescriptors;
    private Action? _saveWorkspaceAction;

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
        _ideDocuments ??= Documents.Documents
            .Select(document => new IdeDocument(
                document.Id,
                Localizer.Get(document.Title),
                ToRegion(document.Location),
                Closeable: true))
            .ToArray();

    private IReadOnlyDictionary<IdeDocumentRegion, string?> ActiveDocuments =>
        _activeDocuments ??= new Dictionary<IdeDocumentRegion, string?>(_selection);

    private IReadOnlyList<LatticeDocumentInstance> MainDocuments =>
        _mainDocuments ??= Documents.Documents
            .Where(document => document.Location == LatticeDockLocation.Main)
            .ToArray();

    private string? ActiveCenterKey => _selection[IdeDocumentRegion.Center];

    private IReadOnlyList<(string Label, LatticeDockLocation Location)> TabDockTargets =>
    [
        (HostLabel("dockLeft"), LatticeDockLocation.Left),
        (HostLabel("dockRight"), LatticeDockLocation.Right)
    ];

    private FlareIcon? DocumentIcon(LatticeDocumentInstance document) =>
        ResolveIcon(
            document.IconKey ?? DocumentDescriptor(document.TypeId)?.IconKey,
            document.IconColor);

    private FlareIcon? DocumentStatusIcon(LatticeDocumentInstance document) =>
        ResolveIcon(document.StatusIconKey, document.StatusIconColor);

    private FlareIcon? ResolveIcon(string? iconKey, string? iconColor)
    {
        var icon = Icons.Resolve(iconKey);
        return icon is not null && !string.IsNullOrWhiteSpace(iconColor)
            ? icon with { Color = FlareColor.Custom(iconColor) }
            : icon;
    }

    private void OnTabActivated(string key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            OnActiveDocumentChanged(new IdeDocumentSelection(IdeDocumentRegion.Center, key));
        }
    }

    private void OnTabCloseRequested(string key) => Documents.Close(key);

    private void CloseTab(string key) => Documents.Close(key);

    private void CloseAllTabs()
    {
        foreach (var document in MainDocuments)
        {
            Documents.Close(document.Id);
        }
    }

    private void CloseOtherTabs(string key)
    {
        foreach (var document in MainDocuments)
        {
            if (!string.Equals(document.Id, key, StringComparison.Ordinal))
            {
                Documents.Close(document.Id);
            }
        }
    }

    private IReadOnlyList<RibbonGroupDescriptor> RibbonGroups =>
        _ribbonGroups ??= Catalog.RibbonGroups
            .OrderBy(group => group.Order)
            .ThenBy(group => group.Id.Value, StringComparer.Ordinal)
            .ToArray();

    private ShellContentDescriptor? ShellContent(LatticeShellSlot slot) =>
        (_shellContent ??= Catalog.ShellContents
            .OrderBy(content => content.Order)
            .ThenBy(content => content.Id.Value, StringComparer.Ordinal)
            .GroupBy(content => content.Slot)
            .ToDictionary(group => group.Key, group => group.First()))
            .GetValueOrDefault(slot);

    private RenderFragment? StartSurfaceContent =>
        ShellContent(LatticeShellSlot.StartPage) is { } page ? RenderComponent(page.Component) : StartContent;

    private RenderFragment? BottomHeaderSurface =>
        ShellContent(LatticeShellSlot.BottomHeader) is { } header ? RenderComponent(header.Component) : null;

    private RenderFragment? CustomBottomPanel =>
        ShellContent(LatticeShellSlot.BottomPanel) is { } panel ? RenderComponent(panel.Component) : BottomPanel;

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
        _menuGroups ??= Catalog.MenuGroups
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
        if (Documents is LatticeDocumentService service)
        {
            service.DocumentChanged += OnDocumentChanged;
        }
        else
        {
            Documents.Changed += OnDocumentsChanged;
        }
        ToolWindows.Changed += OnToolWindowsChanged;
        Localizer.Changed += OnCultureChanged;
        State.Changed += OnStateChanged;
        Shortcuts.Changed += OnShortcutsChanged;
        InitializePersistence();
        InitializeDocumentSelection();
    }

    private void InitializeDocumentSelection()
    {
        foreach (var document in Documents.Documents)
        {
            SelectDocument(ToRegion(document.Location), document.Id);
        }

        _lastActiveId = null;
        SyncSelection();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        if (Documents is LatticeDocumentService service)
        {
            service.DocumentChanged -= OnDocumentChanged;
        }
        else
        {
            Documents.Changed -= OnDocumentsChanged;
        }
        ToolWindows.Changed -= OnToolWindowsChanged;
        Localizer.Changed -= OnCultureChanged;
        State.Changed -= OnStateChanged;
        Shortcuts.Changed -= OnShortcutsChanged;
        if (_settings is not null)
        {
            _settings.Changed -= OnSettingChanged;
        }
    }

    private IEnumerable<RibbonItemDescriptor> ItemsFor(RibbonGroupId group) =>
        (_ribbonItems ??= Catalog.RibbonItems
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Id.Value, StringComparer.Ordinal)
            .ToLookup(item => item.Group))[group];

    private IEnumerable<MenuItemDescriptor> ItemsFor(MenuGroupId group) =>
        (_menuItems ??= Catalog.MenuItems
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Id.Value, StringComparer.Ordinal)
            .ToLookup(item => item.Group))[group];

    private CommandDescriptor? FindCommand(CommandId command) =>
        (_commandDescriptors ??= Catalog.Commands.ToDictionary(descriptor => descriptor.Id)).GetValueOrDefault(command);

    private DocumentDescriptor? DocumentDescriptor(DocumentTypeId type) =>
        (_documentDescriptors ??= Catalog.Documents.ToDictionary(descriptor => descriptor.Id)).GetValueOrDefault(type);

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
        if (FindDocument(document.Key) is not { } instance ||
            DocumentDescriptor(instance.TypeId) is not { } descriptor)
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

    private IEnumerable<ToolWindowDescriptor> ToolDescriptors(LatticeToolRegion region) =>
        (_toolDescriptors ??= Catalog.ToolWindows
            .OrderBy(descriptor => descriptor.Title.ToString(), StringComparer.Ordinal)
            .ThenBy(descriptor => descriptor.Id.Value, StringComparer.Ordinal)
            .ToLookup(descriptor => descriptor.DefaultRegion))[region];

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
        ToolDescriptors(region).Any(descriptor => ToolWindows.IsVisible(descriptor.Id));

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
        foreach (var descriptor in Catalog.ToolWindows)
        {
            if (descriptor.DefaultRegion == region && ToolWindows.IsVisible(descriptor.Id))
            {
                ToolWindows.Hide(descriptor.Id);
            }
        }
    }

    private void OnActiveDocumentChanged(IdeDocumentSelection selection)
    {
        var selectionChanged = SelectDocument(selection.Region, selection.Key);
        if (!string.IsNullOrWhiteSpace(selection.Key))
        {
            Documents.Activate(selection.Key);
        }

        if (selectionChanged)
        {
            SaveWorkspace();
        }
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

    private bool SelectDocument(IdeDocumentRegion region, string? key)
    {
        if (_selection[region] == key)
        {
            return false;
        }

        _selection[region] = key;
        _activeDocuments = null;
        return true;
    }

    private void OnDocumentsChanged() => OnDocumentChanged(LatticeDocumentChange.Structure);

    private void OnDocumentChanged(LatticeDocumentChange change)
    {
        if (change != LatticeDocumentChange.Workspace)
        {
            _ = RenderDocumentChangesAsync(change);
        }
        else if (_persistence is not null)
        {
            _ = InvokeAsync(_saveWorkspaceAction ??= SaveWorkspace);
        }
    }

    private Task RenderDocumentChangesAsync(LatticeDocumentChange change) => InvokeAsync(() =>
    {
        if ((change & LatticeDocumentChange.Tabs) != 0)
        {
            _ideDocuments = null;
            _mainDocuments = null;
        }
        if ((change & LatticeDocumentChange.Selection) != 0)
        {
            SyncSelection();
        }
        if ((change & LatticeDocumentChange.Workspace) != 0)
        {
            SaveWorkspace();
        }
        StateHasChanged();
    });

    private void OnCultureChanged() => _ = InvokeAsync(() =>
    {
        _ideDocuments = null;
        StateHasChanged();
    });

    private void OnStateChanged() => _ = InvokeAsync(StateHasChanged);

    private void SyncSelection()
    {
        var documents = Documents.Documents;
        foreach (var (region, key) in _selection)
        {
            var location = ToLocation(region);
            if (key is not null && FindDocument(key)?.Location != location)
            {
                string? replacement = null;
                for (var index = documents.Count - 1; index >= 0; index--)
                {
                    if (documents[index].Location == location)
                    {
                        replacement = documents[index].Id;
                        break;
                    }
                }
                SelectDocument(region, replacement);
            }
        }

        var active = Documents.Active;
        if (active is not null && (active.Id != _lastActiveId || active.Location != _lastActiveLocation))
        {
            SelectDocument(ToRegion(active.Location), active.Id);
        }

        _lastActiveId = active?.Id;
        _lastActiveLocation = active?.Location;
    }

    private LatticeDocumentInstance? FindDocument(string key)
    {
        var documents = Documents.Documents;
        for (var index = 0; index < documents.Count; index++)
        {
            var document = documents[index];
            if (document.Id == key)
            {
                return document;
            }
        }

        return null;
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
