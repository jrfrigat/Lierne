using System.Diagnostics;
using Flare.Components;
using Flare.Components.IDE;
using Flare.Icons;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Lierne;

/// <summary>Hosts the dockable documents and tool panels of a Lierne workspace.</summary>
public partial class LierneWorkspace : IDisposable, IAsyncDisposable
{
    private readonly Dictionary<LierneToolRegion, LiernePanelState> _panels = new()
    {
        [LierneToolRegion.Left] = new(270),
        [LierneToolRegion.Right] = new(280),
        [LierneToolRegion.Bottom] = new(150)
    };
    private readonly Dictionary<LierneDockLocation, string?> _selection = new()
    {
        [LierneDockLocation.Main] = null,
        [LierneDockLocation.Left] = null,
        [LierneDockLocation.Right] = null
    };
    private readonly string _workspaceId = $"lierne-workspace-{Guid.NewGuid():N}";

    private static readonly FlareSvgIcon PreviewDot = new()
    {
        Data = "M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z"
    };

    // Older Flare packages do not expose the group selector.
    private const string TabCompressionParameter = "AdaptiveTabCompressionForGroup";
    private static readonly bool SupportsTabCompression =
        typeof(FlareDockWorkspace).GetProperty(TabCompressionParameter) is not null;
    private const string TabContextMenuParameter = "UseTabContextMenu";
    private static readonly bool SupportsTabContextMenu =
        typeof(FlareDockWorkspace).GetProperty(TabContextMenuParameter) is not null;
    private Dictionary<string, object>? _dockTabParameters;

    private DockNode _layout = new DockGroup("group:main", []);
    private DockItem[] _items = [];
    private Dictionary<string, LierneDockEntry> _entries = new(StringComparer.Ordinal);
    private DockNode? _canonicalLayout;
    private RenderFragment<DockItem> _dockContentTemplate = default!;
    private RenderFragment? _pluginStartSurface;
    private RenderFragment? _pluginBottomSurface;
    private bool? _showStartPage;
    private bool _hasBottomSurface;
    private string? _lastActiveId;
    private LierneDockLocation? _lastActiveLocation;
    private LierneStatePersistence? _persistence;
    private ISettingsStore? _settings;
    private Func<LierneWorkspaceLayout>? _captureWorkspace;
    private Action<Exception>? _reportWorkspaceError;
    private IJSObjectReference? _sizeModule;
    private DotNetObjectReference<LierneWorkspace>? _sizeReference;
    private double _viewportWidth;
    private double _viewportHeight;
    private IReadOnlyDictionary<string, double>? _restoredFractions;
    private HashSet<string> _visibleTools = new(StringComparer.Ordinal);
    private bool _workspaceReady = true;
    private bool _applying;
    private bool _detailedDocumentChanges;
    private bool _disposed;

    [Inject] private ILierneDocumentService Documents { get; set; } = default!;
    [Inject] private IPanelService Panels { get; set; } = default!;
    [Inject] private ILierneToolWindowService ToolWindows { get; set; } = default!;
    [Inject] private ILierneCommandService Commands { get; set; } = default!;
    [Inject] private ILierneShortcutService Shortcuts { get; set; } = default!;
    [Inject] private ILierneLocalizer Localizer { get; set; } = default!;
    [Inject] private ILierneContext State { get; set; } = default!;
    [Inject] private LiernePluginCatalog Catalog { get; set; } = default!;
    [Inject] private LierneNotificationService Notifications { get; set; } = default!;
    [Inject] private ILierneIconResolver Icons { get; set; } = default!;
    [Inject] private IServiceProvider Services { get; set; } = default!;

    /// <summary>Gets or sets the content shown when no document is open.</summary>
    [Parameter]
    public RenderFragment? StartContent { get; set; }

    /// <summary>Gets or sets the host bottom surface. A shell contribution takes precedence.</summary>
    [Parameter]
    public RenderFragment? BottomPanel { get; set; }

    /// <summary>Replaces the visible title of each document tab.</summary>
    [Parameter]
    public RenderFragment<LierneDocumentHeaderContext>? DocumentHeaderContent { get; set; }

    /// <summary>Gets or sets whether the start surface is offered when the center is empty.</summary>
    [Parameter]
    public bool ShowStartPage { get; set; } = true;

    /// <summary>Gets or sets whether the host bottom surface is shown.</summary>
    [Parameter]
    public bool ShowBottomPanel { get; set; } = true;

    /// <summary>Gets or sets the body regions the dock offers as drop targets.</summary>
    [Parameter]
    public DockDropZones DropZones { get; set; } = DockDropZones.All;

    /// <summary>Gets or sets whether Flare paints its base drop guide while a tab is dragged.</summary>
    [Parameter]
    public bool ShowDropGuide { get; set; } = true;

    /// <summary>Gets or sets whether the dock shows tab actions from a context menu.</summary>
    [Parameter]
    public bool UseTabContextMenu { get; set; }

    private RenderFragment? StartSurface => _pluginStartSurface ?? StartContent;

    private RenderFragment? BottomSurface => _pluginBottomSurface ?? BottomPanel;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (SupportsTabCompression)
        {
            _dockTabParameters = new Dictionary<string, object>
            {
                [TabCompressionParameter] = (Func<DockGroup, bool>)CompressDockTabs
            };
        }

        _dockContentTemplate = RenderItem;
        if (LierneShellContentResolver.Resolve(Catalog, LierneShellSlot.StartPage) is { } start)
        {
            _pluginStartSurface = RenderComponent(start.Component);
        }
        if (LierneShellContentResolver.Resolve(Catalog, LierneShellSlot.BottomPanel) is { } bottom)
        {
            _pluginBottomSurface = RenderComponent(bottom.Component);
        }

        if (Documents is LierneDocumentService service)
        {
            service.DocumentChanged += OnDocumentChanged;
            _detailedDocumentChanges = true;
        }
        else
        {
            Documents.Changed += OnDocumentsChanged;
        }

        Panels.Changed += OnPanelsChanged;
        ToolWindows.Changed += OnToolWindowsChanged;
        Localizer.Changed += OnLocalizerChanged;
        State.Changed += OnStateChanged;
        Shortcuts.Changed += OnShortcutsChanged;
        InitializePersistence();
        InitializeDocumentSelection();
        _visibleTools = [.. ToolWindows.ToolWindows.Where(tool => tool.Visible).Select(tool => tool.Id.Value)];
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (SupportsTabContextMenu)
        {
            (_dockTabParameters ??= new())[TabContextMenuParameter] = UseTabContextMenu;
        }

        var hasBottom = ShowBottomPanel && BottomSurface is not null;
        if (_showStartPage != ShowStartPage || _hasBottomSurface != hasBottom)
        {
            _showStartPage = ShowStartPage;
            _hasBottomSurface = hasBottom;
            Refresh();
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed)
        {
            return;
        }

        if (_persistence is not null && !_workspaceReady)
        {
            await RunStateOperationAsync(() => _persistence.RestorePreferencesAsync(), "stateLoadFailed");
            await RunStateOperationAsync(async () =>
            {
                var layout = await _persistence.RestoreWorkspaceAsync();
                InitializeDocumentSelection();
                if (layout is not null)
                {
                    ApplyLayout(layout);
                }
            }, "stateLoadFailed");

            if (_disposed)
            {
                return;
            }

            _workspaceReady = true;
            // Restored visibility is the baseline, not a command that should select a tool.
            _visibleTools = [.. ToolWindows.ToolWindows.Where(tool => tool.Visible).Select(tool => tool.Id.Value)];
            // The restored sizes, not the pre-restore fractions, define the first painted layout.
            Refresh(forceLayout: true, rebuildFractions: true);
            StateHasChanged();
            return;
        }

        if (_workspaceReady && _sizeModule is null && Services.GetService<IJSRuntime>() is { } js)
        {
            try
            {
                _sizeModule = await js.InvokeAsync<IJSObjectReference>("import", "./_content/Lierne/lierne.js");
                if (_disposed)
                {
                    await _sizeModule.DisposeAsync();
                    _sizeModule = null;
                    return;
                }

                _sizeReference = DotNetObjectReference.Create(this);
                await _sizeModule.InvokeVoidAsync("watchWorkspaceSize", _workspaceId, _sizeReference);
            }
            catch (Exception exception) when (exception is JSDisconnectedException or InvalidOperationException)
            {
            }
            catch (Exception exception)
            {
                ReportStateError("stateLayoutWatchFailed", exception);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        if (_detailedDocumentChanges && Documents is LierneDocumentService service)
        {
            service.DocumentChanged -= OnDocumentChanged;
        }
        else
        {
            Documents.Changed -= OnDocumentsChanged;
        }

        Panels.Changed -= OnPanelsChanged;
        ToolWindows.Changed -= OnToolWindowsChanged;
        Localizer.Changed -= OnLocalizerChanged;
        State.Changed -= OnStateChanged;
        Shortcuts.Changed -= OnShortcutsChanged;
        if (_settings is not null)
        {
            _settings.Changed -= OnSettingChanged;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Dispose();
        try
        {
            if (_persistence is not null)
            {
                await _persistence.FlushAsync();
            }

            if (_sizeModule is not null)
            {
                await _sizeModule.InvokeVoidAsync("unwatchWorkspaceSize", _workspaceId);
                await _sizeModule.DisposeAsync();
            }
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or InvalidOperationException)
        {
        }
        finally
        {
            _sizeReference?.Dispose();
        }
    }

    /// <summary>Records the viewport measured by the browser.</summary>
    /// <param name="width">The workspace width in pixels.</param>
    /// <param name="height">The workspace height in pixels.</param>
    [JSInvokable]
    public Task OnViewportChanged(double width, double height) => InvokeAsync(() =>
    {
        if (!_disposed && double.IsFinite(width) && double.IsFinite(height) && width > 0 && height > 0)
        {
            _viewportWidth = width;
            _viewportHeight = height;
        }
    });

    private void InitializePersistence()
    {
        if (Services.GetService<LierneStatePersistence>() is not { IsEnabled: true } persistence)
        {
            return;
        }

        _persistence = persistence;
        _workspaceReady = false;
        _settings = Services.GetRequiredService<ISettingsStore>();
        _settings.Changed += OnSettingChanged;
    }

    private void InitializeDocumentSelection()
    {
        _selection[LierneDockLocation.Main] = null;
        _selection[LierneDockLocation.Left] = null;
        _selection[LierneDockLocation.Right] = null;
        foreach (var document in Documents.Documents)
        {
            _selection[document.Location] = document.Id;
        }

        _lastActiveId = null;
        _lastActiveLocation = null;
        SyncSelection();
    }

    internal void ApplyLayout(LierneWorkspaceLayout layout)
    {
        _restoredFractions = layout.Splits;
        _panels[LierneToolRegion.Left] = CoercePanel(layout.Left, _panels[LierneToolRegion.Left]);
        _panels[LierneToolRegion.Right] = CoercePanel(layout.Right, _panels[LierneToolRegion.Right]);
        _panels[LierneToolRegion.Bottom] = CoercePanel(layout.Bottom, _panels[LierneToolRegion.Bottom]);
        foreach (var (location, key) in layout.Documents ?? [])
        {
            if (Enum.IsDefined(location) && (key is null || FindDocument(key)?.Location == location))
            {
                _selection[location] = key;
            }
        }

        if (layout.Panels is { } saved)
        {
            foreach (var placement in saved)
            {
                if (!string.IsNullOrWhiteSpace(placement.Panel.Value) &&
                    Panels.Get(placement.Panel) is not null)
                {
                    Panels.Move(placement.Panel, placement);
                }
            }
        }

        SyncSelection();
    }

    private static LiernePanelState CoercePanel(LiernePanelState? saved, LiernePanelState fallback) =>
        saved is null ? fallback : saved with { Size = saved.Size is >= 120 and <= 32768 ? saved.Size : fallback.Size };

    private void Refresh(bool forceLayout = false, bool rebuildFractions = false)
    {
        var model = BuildModel(rebuildFractions);
        if (forceLayout || !LierneDockLayoutAdapter.SameLayout(_canonicalLayout, model.Layout))
        {
            _layout = model.Layout;
        }

        SetModel(model);
    }

    private void SetModel(LierneDockModel model)
    {
        _canonicalLayout = model.Layout;
        var items = new DockItem[model.Items.Count];
        for (var index = 0; index < items.Length; index++)
        {
            var item = model.Items[index];
            var entry = model.Entries[item.Key];
            entry.ItemIndex = index;
            items[index] = Decorate(item, entry);
        }

        _items = items;
        _entries = model.Entries;
    }

    private DockItem Decorate(DockItem item, LierneDockEntry entry)
    {
        var preview = entry.Document?.Preview == true;
        var icon = TabIcon(entry.Document, entry.Panel);
        if (!preview && icon is null)
        {
            return item;
        }

        return item with
        {
            Icon = icon,
            BadgeDot = preview,
            BadgeColor = preview ? FlareColor.OnSurfaceVariant : item.BadgeColor
        };
    }

    private bool UpdateDocumentTab(string? id)
    {
        if (id is null || LierneDockLayoutAdapter.FindDocumentEntry(_entries, id) is not { Document: { } previous } entry ||
            FindDocument(id) is not { } document)
        {
            return false;
        }

        var item = _items[entry.ItemIndex];
        entry.Document = document;
        var title = previous.Title == document.Title ? item.Title : Localizer.Get(document.Title);
        var icon = previous.IconKey == document.IconKey && previous.IconColor == document.IconColor
            ? item.Icon
            : TabIcon(document, null);
        if (title != item.Title || icon != item.Icon || document.Preview != item.BadgeDot)
        {
            var items = (DockItem[])_items.Clone();
            items[entry.ItemIndex] = item with
            {
                Title = title,
                Icon = icon,
                BadgeDot = document.Preview,
                BadgeColor = document.Preview ? FlareColor.OnSurfaceVariant : default
            };
            _items = items;
        }

        return true;
    }

    private bool UpdateActiveDocument()
    {
        var active = Documents.Active;
        RememberActiveDocument(active);
        if (active is null || LierneDockLayoutAdapter.FindDocumentEntry(_entries, active.Id) is not { } entry)
        {
            return false;
        }

        var key = _items[entry.ItemIndex].Key;
        var layout = LierneDockLayoutAdapter.SelectItem(_layout, key);
        var canonical = ReferenceEquals(_canonicalLayout, _layout)
            ? layout
            : LierneDockLayoutAdapter.SelectItem(_canonicalLayout, key);
        if (layout is null || canonical is null)
        {
            return false;
        }

        _layout = layout;
        _canonicalLayout = canonical;
        return true;
    }

    private LierneDockModel BuildModel(bool rebuildFractions = false)
    {
        var placements = Panels.Placements;
        List<PanelPlacement>? additional = null;
        var tools = ToolWindows.ToolWindows;
        for (var index = 0; index < tools.Count; index++)
        {
            var tool = tools[index];
            if (!tool.Visible || Panels.Get(tool.Id) is not null)
            {
                continue;
            }

            (additional ??= [.. placements]).Add(new PanelPlacement(tool.Id, tool.Region, Visible: true));
        }

        return LierneDockLayoutAdapter.Build(
            Documents.Documents,
            Documents.Active,
            additional ?? placements,
            tools,
            Catalog.ToolWindows,
            _panels,
            _selection,
            Localizer.Get,
            HostLabel("start"),
            HostLabel("bottomPanel"),
            ShowStartPage,
            _hasBottomSurface,
            rebuildFractions ? null : _layout,
            rebuildFractions ? _restoredFractions : null);
    }

    private void SyncSelection()
    {
        var documents = Documents.Documents;
        foreach (var (location, key) in _selection)
        {
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

                _selection[location] = replacement;
            }
        }

        RememberActiveDocument(Documents.Active);
    }

    private void RememberActiveDocument(LierneDocumentInstance? active)
    {
        if (active is not null && (active.Id != _lastActiveId || active.Location != _lastActiveLocation))
        {
            _selection[active.Location] = active.Id;
        }

        _lastActiveId = active?.Id;
        _lastActiveLocation = active?.Location;
    }

    private bool CompressDockTabs(DockGroup group) =>
        LierneDockLayoutAdapter.ToolRegionOf(_layout, group.Key) is LierneToolRegion.Left or LierneToolRegion.Right;

    private void SelectDocument(LierneDockLocation location, string? key)
    {
        if (_selection[location] != key)
        {
            _selection[location] = key;
        }
    }

    private LierneDocumentInstance? FindDocument(string key)
    {
        if (Documents is LierneDocumentService service)
        {
            return service.Find(key);
        }

        var documents = Documents.Documents;
        for (var index = 0; index < documents.Count; index++)
        {
            if (string.Equals(documents[index].Id, key, StringComparison.Ordinal))
            {
                return documents[index];
            }
        }

        return null;
    }

    private bool CanRequest(DockAction action, string? key, string target)
    {
        if (key is null)
        {
            return action == DockAction.Resize && FindSplit(_layout, target) is not null;
        }

        if (key is LierneDockLayoutAdapter.StartKey or LierneDockLayoutAdapter.BottomKey)
        {
            return action == DockAction.Activate;
        }

        if (Entry(key)?.Document is { } document)
        {
            return FindDocument(document.Id) is not null &&
                !(action is DockAction.Move or DockAction.Split &&
                    LierneDockLayoutAdapter.DocumentRegionOf(_layout, target) is null);
        }

        if (Entry(key)?.Panel is not null)
        {
            return !(action is DockAction.Move or DockAction.Split &&
                LierneDockLayoutAdapter.ToolRegionOf(_layout, target) is null);
        }

        return false;
    }

    private string GroupLabel(DockGroup group) =>
        group.ActiveKey is { } key && Entry(key) is { } entry
            ? _items[entry.ItemIndex].Title
            : HostLabel("emptyPanel");

    private LierneDockEntry? Entry(string key) => _entries.GetValueOrDefault(key);

    private LierneDocumentInstance? TabDocument(string key) =>
        Entry(key)?.Document is { } document ? FindDocument(document.Id) : null;

    private FlareIcon? TabIcon(LierneDocumentInstance? document, PanelPlacement? panel)
    {
        if (document is not null)
        {
            var iconKey = document.IconKey ?? FindDocumentIconKey(document.TypeId);
            return ResolveIcon(iconKey, document.IconColor);
        }

        return panel is not null ? Icons.Resolve(FindToolDescriptor(panel.Panel)?.IconKey) : null;
    }

    private string? FindDocumentIconKey(DocumentTypeId type)
    {
        var descriptors = Catalog.Documents;
        for (var index = 0; index < descriptors.Count; index++)
        {
            if (descriptors[index].Id == type)
            {
                return descriptors[index].IconKey;
            }
        }

        return null;
    }

    private ToolWindowDescriptor? FindToolDescriptor(ToolWindowId id)
    {
        var descriptors = Catalog.ToolWindows;
        for (var index = 0; index < descriptors.Count; index++)
        {
            if (descriptors[index].Id == id)
            {
                return descriptors[index];
            }
        }

        return null;
    }

    private LierneToolWindowInstance? FindTool(ToolWindowId id)
    {
        var tools = ToolWindows.ToolWindows;
        for (var index = 0; index < tools.Count; index++)
        {
            if (tools[index].Id == id)
            {
                return tools[index];
            }
        }

        return null;
    }

    private FlareIcon? ResolveIcon(string? iconKey, string? iconColor)
    {
        var icon = Icons.Resolve(iconKey);
        return icon is not null && !string.IsNullOrWhiteSpace(iconColor)
            ? icon with { Color = FlareColor.Custom(iconColor) }
            : icon;
    }

    private RenderFragment RenderItem(DockItem item) => builder =>
    {
        if (!_entries.TryGetValue(item.Key, out var entry))
        {
            return;
        }

        switch (entry.Kind)
        {
            case LierneDockEntryKind.Document when entry.Document is { } document &&
                                                   FindDocument(document.Id) is { } current:
                builder.OpenComponent<LierneDocumentHost>(0);
                builder.AddAttribute(1, nameof(LierneDocumentHost.DocumentId), current.Id);
                builder.AddAttribute(2, nameof(LierneDocumentHost.Document), current);
                builder.CloseComponent();
                break;
            case LierneDockEntryKind.Panel when entry.Panel is { } placement:
                if (FindToolDescriptor(placement.Panel) is not { } descriptor ||
                    FindTool(placement.Panel) is not { } tool)
                {
                    return;
                }

                builder.OpenComponent<DynamicComponent>(0);
                builder.AddAttribute(1, nameof(DynamicComponent.Type), descriptor.Component);
                builder.AddAttribute(2, nameof(DynamicComponent.Parameters), new Dictionary<string, object?>
                {
                    [nameof(LierneToolWindowComponent.Context)] = new LierneToolWindowContext(
                        tool,
                        Commands,
                        ToolWindows,
                        Localizer,
                        Services)
                });
                builder.CloseComponent();
                break;
            case LierneDockEntryKind.Start when StartSurface is { } start:
                builder.AddContent(0, start);
                break;
            case LierneDockEntryKind.Bottom when BottomSurface is { } bottom:
                builder.AddContent(0, bottom);
                break;
        }
    };

    internal DockNode Layout => _layout;

    internal IReadOnlyList<DockItem> Items => _items;

    internal Task ApplyChangeAsync(DockLayoutChange change)
    {
        if (_disposed || !ReferenceEquals(change.Previous, _layout) ||
            ReferenceEquals(change.Previous, change.Proposed) ||
            !CanRequest(change.Action, change.ItemKey, change.TargetKey))
        {
            return Task.CompletedTask;
        }

        _applying = true;
        var changed = false;
        try
        {
            changed = change.Action switch
            {
                DockAction.Activate => Activate(change),
                DockAction.Close => Close(change),
                DockAction.Move or DockAction.Split => Move(change),
                DockAction.Resize => Resize(change),
                _ => false
            };
        }
        finally
        {
            _applying = false;
        }

        if (!changed && !IsSessionChange(change))
        {
            return Task.CompletedTask;
        }

        if (_detailedDocumentChanges && change.Action == DockAction.Activate && change.ItemKey is { } key)
        {
            var canonical = ReferenceEquals(_canonicalLayout, change.Previous)
                ? change.Proposed
                : LierneDockLayoutAdapter.SelectItem(_canonicalLayout, key);
            if (canonical is not null)
            {
                RememberActiveDocument(Documents.Active);
                _layout = change.Proposed;
                _canonicalLayout = canonical;
                SaveWorkspace();
                return Task.CompletedTask;
            }
        }

        SyncSelection();
        _layout = change.Proposed;
        var model = BuildModel();
        if (model.Entries.ContainsKey(LierneDockLayoutAdapter.StartKey) !=
            _entries.ContainsKey(LierneDockLayoutAdapter.StartKey))
        {
            _layout = model.Layout;
        }
        SetModel(model);
        if (changed)
        {
            SaveWorkspace();
        }

        return Task.CompletedTask;
    }

    private bool IsSessionChange(DockLayoutChange change) => change.Action switch
    {
        DockAction.Resize => FindSplit(change.Proposed, change.TargetKey) is not null,
        DockAction.Move or DockAction.Split when change.ItemKey is { } key =>
            Entry(key) switch
            {
                { Document: { } document } when FindDocument(document.Id) is { } current =>
                    current.Location == LierneDockLayoutAdapter.DocumentRegionOf(change.Previous, change.TargetKey),
                { Panel: { } panel } =>
                    panel.Region == LierneDockLayoutAdapter.ToolRegionOf(change.Previous, change.TargetKey),
                _ => false
            },
        _ => false
    };

    private bool Activate(DockLayoutChange change)
    {
        if (change.ItemKey is not { } key)
        {
            return false;
        }

        if (Entry(key)?.Document is { } document && Documents.Activate(document.Id).Success)
        {
            var documentId = document.Id;
            var changed = _lastActiveId != documentId;
            if (LierneDockLayoutAdapter.DocumentRegionOf(change.Previous, change.TargetKey) is { } location &&
                _selection[location] != documentId)
            {
                SelectDocument(location, documentId);
                changed = true;
            }

            return changed;
        }

        if (Entry(key)?.Panel is { Panel: var panel })
        {
            var region = LierneDockLayoutAdapter.ToolRegionOf(change.Previous, change.TargetKey) ??
                Panels.Get(panel)?.Region;
            if (region is { } selectedRegion && _panels[selectedRegion].ToolKey != panel.Value)
            {
                _panels[selectedRegion] = _panels[selectedRegion] with { ToolKey = panel.Value };
                return true;
            }
        }

        return false;
    }

    // A tab closes whatever it represents: a document is closed, a tool window is hidden back to
    // its rail, which is what the dock's own close action does for a panel.
    private void CloseItem(DockItem item)
    {
        if (_disposed)
        {
            return;
        }

        switch (Entry(item.Key))
        {
            case { Document: { } document }:
                Documents.Close(document.Id);
                break;

            case { Panel: { } panel }:
                Panels.Hide(panel.Panel);
                break;
        }
    }

    private bool Close(DockLayoutChange change)
    {
        return change.ItemKey is { } key && (Entry(key) switch
        {
            { Document: { } document } => Documents.Close(document.Id).Success,
            { Panel: { } panel } => Panels.Hide(panel.Panel).Success,
            _ => false
        });
    }

    private bool Move(DockLayoutChange change)
    {
        if (change.ItemKey is not { } key)
        {
            return false;
        }

        var entry = Entry(key);
        if (entry?.Document is { Id: var documentId })
        {
            if (LierneDockLayoutAdapter.DocumentRegionOf(change.Previous, change.TargetKey) is not { } location ||
                FindDocument(documentId)?.Location == location || !Documents.Move(documentId, location).Success)
            {
                return false;
            }

            SelectDocument(location, documentId);
            return true;
        }

        if (entry?.Panel is not { Panel: var panel } || Panels.Get(panel) is not { } placement)
        {
            return false;
        }

        var targetRegion = LierneDockLayoutAdapter.ToolRegionOf(change.Previous, change.TargetKey);
        if (targetRegion is not { } region || region == placement.Region)
        {
            return false;
        }

        var order = Panels.Placements
            .Where(item => item.Region == region && item.Panel != panel)
            .Select(item => item.Order)
            .DefaultIfEmpty(-1)
            .Max() + 1;
        return Panels.Move(panel, placement with { Region = region, Order = order, Visible = true }).Success;
    }

    private bool Resize(DockLayoutChange change)
    {
        if (change.Proposed is not DockSplit root)
        {
            return false;
        }

        var split = FindSplit(root, change.TargetKey);
        if (split is null)
        {
            return false;
        }

        var width = _viewportWidth > 0 ? _viewportWidth : 1200;
        var height = _viewportHeight > 0 ? _viewportHeight : 800;
        return split.Key switch
        {
            "split:left" => SetSize(LierneToolRegion.Left, split.Fraction * width),
            "split:right" => SetSize(LierneToolRegion.Right, (1 - split.Fraction) * width),
            "split:bottom" => SetSize(LierneToolRegion.Bottom, (1 - split.Fraction) * height),
            _ => false
        };
    }

    private bool SetSize(LierneToolRegion region, double size)
    {
        var rounded = Math.Round(Math.Clamp(size, 120, 32768));
        if (Math.Abs(rounded - _panels[region].Size) < 1)
        {
            return false;
        }

        _panels[region] = _panels[region] with { Size = (int)rounded };
        return true;
    }

    private static DockSplit? FindSplit(DockNode? node, string key)
    {
        if (node is not DockSplit split)
        {
            return null;
        }

        return split.Key == key
            ? split
            : FindSplit(split.First, key) ?? FindSplit(split.Second, key);
    }

    private void OnDocumentChanged(LierneDocumentChange change, string? tabDocumentId)
    {
        if (_applying)
        {
            return;
        }

        _ = InvokeAsync(() =>
        {
            if (_disposed)
            {
                return;
            }

            if ((change & LierneDocumentChange.Structure) != 0 ||
                ((change & LierneDocumentChange.Tabs) != 0 && !UpdateDocumentTab(tabDocumentId)) ||
                ((change & LierneDocumentChange.Selection) != 0 && !UpdateActiveDocument()))
            {
                SyncSelection();
                Refresh();
            }
            if ((change & LierneDocumentChange.Workspace) != 0)
            {
                SaveWorkspace();
            }

            if ((change & (LierneDocumentChange.Structure | LierneDocumentChange.Tabs |
                           LierneDocumentChange.Selection | LierneDocumentChange.Content)) != 0)
            {
                StateHasChanged();
            }
        });
    }

    private void OnDocumentsChanged()
    {
        if (_applying)
        {
            return;
        }

        _ = InvokeAsync(() =>
        {
            if (_disposed)
            {
                return;
            }

            SyncSelection();
            Refresh();
            SaveWorkspace();
            StateHasChanged();
        });
    }

    private void OnPanelsChanged()
    {
        if (!_applying)
        {
            _ = InvokeAsync(() =>
            {
                if (!_disposed)
                {
                    Refresh();
                    SaveWorkspace();
                    StateHasChanged();
                }
            });
        }
    }

    private void OnToolWindowsChanged()
    {
        if (!_applying)
        {
            _ = InvokeAsync(() =>
            {
                if (!_disposed)
                {
                    ActivateNewlyShownTools();
                    Refresh();
                    SaveWorkspace();
                    StateHasChanged();
                }
            });
        }
    }

    // Showing a tool from a command brings it to the front of its region, the way a View-menu
    // command opens its tool window. Restored visibility is recorded as the baseline instead, so
    // restoring the workspace keeps the saved selection.
    private void ActivateNewlyShownTools()
    {
        var visible = ToolWindows.ToolWindows.Where(tool => tool.Visible).ToArray();
        if (_workspaceReady)
        {
            foreach (var tool in visible)
            {
                if (!_visibleTools.Add(tool.Id.Value))
                {
                    continue;
                }

                var region = Panels.Get(tool.Id)?.Region ?? tool.Region;
                _panels[region] = _panels[region] with { ToolKey = tool.Id.Value };
            }
        }

        _visibleTools = [.. visible.Select(tool => tool.Id.Value)];
    }

    private void OnLocalizerChanged() => _ = InvokeAsync(() =>
    {
        if (!_disposed)
        {
            Refresh();
            StateHasChanged();
        }
    });

    private void OnStateChanged() => _ = InvokeAsync(StateHasChanged);

    private void OnShortcutsChanged() => _ = InvokeAsync(() =>
    {
        SavePreferences();
        StateHasChanged();
    });

    private void OnSettingChanged(SettingsKey key) => _ = InvokeAsync(SavePreferences);

    private void SavePreferences()
    {
        if (_workspaceReady && !_disposed && _persistence is { } persistence)
        {
            _ = RunStateOperationAsync(() => persistence.SavePreferencesAsync(), "stateSaveFailed");
        }
    }

    private void SaveWorkspace()
    {
        if (_workspaceReady && !_disposed && _persistence is { } persistence)
        {
            persistence.QueueWorkspaceSave(
                _captureWorkspace ??= CaptureWorkspace,
                _reportWorkspaceError ??= OnWorkspaceSaveError);
        }
    }

    private LierneWorkspaceLayout CaptureWorkspace() => new()
    {
        Documents = new Dictionary<LierneDockLocation, string?>(_selection),
        Left = _panels[LierneToolRegion.Left],
        Right = _panels[LierneToolRegion.Right],
        Bottom = _panels[LierneToolRegion.Bottom],
        Panels = ToolWindows.ToolWindows
            .Select(tool => Panels.Get(tool.Id) is { } placement
                ? placement with { Visible = tool.Visible }
                : null)
            .Where(placement => placement is not null)
            .Cast<PanelPlacement>()
            .ToArray(),
        Splits = CaptureFractions(_layout)
    };

    private static Dictionary<string, double> CaptureFractions(
        DockNode? node,
        Dictionary<string, double>? fractions = null)
    {
        fractions ??= new Dictionary<string, double>(StringComparer.Ordinal);
        if (node is DockSplit split)
        {
            fractions[split.Key] = split.Fraction;
            CaptureFractions(split.First, fractions);
            CaptureFractions(split.Second, fractions);
        }

        return fractions;
    }

    private void OnWorkspaceSaveError(Exception exception)
    {
        if (exception is not JSDisconnectedException)
        {
            ReportStateError("stateSaveFailed", exception);
        }
    }

    private async Task RunStateOperationAsync(Func<Task> operation, string error)
    {
        try
        {
            await operation();
        }
        catch (JSDisconnectedException)
        {
        }
        catch (Exception exception)
        {
            ReportStateError(error, exception);
        }
    }

    private void ReportStateError(string error, Exception exception)
    {
        Trace.TraceWarning("Lierne state operation {0} failed: {1}", error, exception.GetType().Name);
        if (!_disposed)
        {
            Notifications.Notify(LierneNotification.Warning(HostKey(error)));
        }
    }

    private static RenderFragment RenderComponent(Type component) => builder =>
    {
        builder.OpenComponent<DynamicComponent>(0);
        builder.AddAttribute(1, nameof(DynamicComponent.Type), component);
        builder.CloseComponent();
    };

    private StringKey HostKey(string name) => new("lattice", name);

    private string HostLabel(string name) => Localizer.Get(HostKey(name));
}
